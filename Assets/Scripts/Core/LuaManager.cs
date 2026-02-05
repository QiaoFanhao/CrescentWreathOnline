using UnityEngine;
using XLua;
using System.IO;
using System.Collections;
using CrescentWreath.Core;
using CrescentWreath.Data;
using CrescentWreath.View; // 引用 TargetSelectionManager
using CrescentWreath.Lua;
public class LuaManager : MonoBehaviour
{
    public static LuaManager Instance { get; private set; }
    private LuaEnv _luaEnv;

    // 缓存 Lua 全局的 coroutine 表，优化性能
    private LuaTable _coroutineTable;
    private LuaFunction _coroutineCreate;
    private LuaFunction _coroutineResume;
    private LuaFunction _createRunner;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitLuaEnv();
        }
        else Destroy(gameObject);
    }

    private void InitLuaEnv()
    {
        _luaEnv = new LuaEnv();
        _luaEnv.AddLoader(CustomLoader);

        // 1. 【新增】定义一个 Lua 辅助函数
        // 它的作用是：创建一个协程，并返回一个“每次调用都会 resume 该协程”的闭包函数
        _luaEnv.DoString(@"
        function Global_CreateCoroutineRunner(func)
            local co = coroutine.create(func)
            return function(...)
                -- 闭包捕获了 co，C# 只需要调用这个函数，不需要接触 co 本身
                return coroutine.resume(co, ...)
            end
        end
    ");

        // 2. 获取这个辅助函数
        _createRunner = _luaEnv.Global.Get<LuaFunction>("Global_CreateCoroutineRunner");


    }

    // --- 核心调度器 ---

    public void ExecuteCardEffect(BaseCardSO cardData, int ownerId)
    {
        // 1. 加载脚本
        string category = cardData is RelicCardSO ? "Relic" : "Character"; // 简化分类逻辑，按需补全
        string scriptName = string.IsNullOrEmpty(cardData.luaScriptName) ? cardData.cardId.ToString() : cardData.luaScriptName;
        string requirePath = $"Cards/{category}/{scriptName}";

        object[] results = null;
        try
        {
            results = _luaEnv.DoString($"return require('{requirePath}')");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Lua] 加载失败或无脚本: {requirePath}. {ex.Message}");
            // 即使无脚本，也要发资源 (C# 兜底)
            GrantBasicResources(cardData);
            return;
        }

        if (results != null && results.Length > 0 && results[0] is LuaTable scriptTable)
        {
            LuaFunction onPlayFunc = scriptTable.Get<LuaFunction>("OnPlay");
            if (onPlayFunc != null)
            {
                // 3. 构造 Context
                var turnModule = FindObjectOfType<CrescentWreath.Modules.TurnModule>();
                var context = new LuaEffectContext(ownerId, -1, cardData, turnModule, turnModule.playerZoneModules);

                // ==========================================================
                // 【关键修改】不再调用 coroutine.create，而是调用我们的辅助函数
                // ==========================================================
                // runner 就是那个“闭包函数”，它内部藏着 thread
                var runner = _createRunner.Call(onPlayFunc)[0] as LuaFunction;

                // 4. 开始执行 (传入 runner)
                ResumeLuaCoroutine(runner, scriptTable, context, cardData);
            }
        }
    }

    /// <summary>
    /// 驱动协程继续运行
    /// </summary>
    private void ResumeLuaCoroutine(LuaFunction runner, LuaTable scriptTable, LuaEffectContext context, BaseCardSO cardData, object yieldResult = null)
    {
        object[] resumeResults;

        // 调用闭包函数 (它内部会自动调用 coroutine.resume)
        if (yieldResult == null)
        {
            // 第一次启动：传入 (self, context)
            resumeResults = runner.Call(scriptTable, context);
        }
        else
        {
            // 后续恢复：传入 yield 的返回值 (如 targetId)
            resumeResults = runner.Call(yieldResult);
        }

        // --- 以下逻辑保持不变 ---

        // coroutine.resume 的第一个返回值是 bool (是否成功)
        if (resumeResults == null || resumeResults.Length == 0) return; // 防御性检查

        bool success = (bool)resumeResults[0];

        if (!success)
        {
            Debug.LogError($"[Lua Error] 协程执行报错: {resumeResults[1]}");
            return;
        }

        // 检查是否有 yield 出来的指令
        if (resumeResults.Length > 1 && resumeResults[1] is LuaYieldInstruction instruction)
        {
            HandleYieldInstruction(instruction, (result) =>
            {
                // 【递归传递】继续传递 runner
                ResumeLuaCoroutine(runner, scriptTable, context, cardData, result);
            });
        }
        else
        {
            Debug.Log($"[Lua] 协程 {cardData.cardId} 执行完毕。");
            GrantBasicResources(cardData);

            // 执行完毕，释放 LuaFunction 引用（可选，减轻 GC 压力）
            runner.Dispose();
        }
    }

    // 处理各种异步指令
    // LuaManager.cs 中的 HandleYieldInstruction 方法
    private void HandleYieldInstruction(LuaYieldInstruction instruction, System.Action<object> onComplete)
    {
        // 1. 获取传入对象的真实身份
        string incomingType = instruction.GetType().FullName;
        // 2. 获取 LuaManager 认为的身份
        string expectedType = typeof(CrescentWreath.Core.WaitForDamageProcess).FullName;

        Debug.Log($"[Debug] 收到指令对象: {incomingType} | 期望匹配类型: {expectedType}");
        if (instruction is WaitForSelection request)
        {
            Debug.Log("[LuaManager] 正在处理 WaitForSelection...");
            // 根据 Zone 的类型，决定启用哪种选择器
            if (request.Zone == "Player")
            {
                // 选玩家头像 (3D)
                TargetSelectionManager.Instance.StartPlayerSelection(
                    request.Scope,
                    (id) => onComplete(id) // 把 int 传给 object 委托，这是安全的
                );
            }
            // === 新增：处理伤害请求 ===
            else if (instruction is WaitForDamageProcess dmgRequest)
            {
                Debug.Log($"[LuaManager] 匹配成功！正在处理伤害... 目标:{dmgRequest.TargetId}");
                StartCoroutine(MockDefenseSequence(dmgRequest, onComplete));
            }
            else if (request.Zone == "Battlefield" || request.Zone == "Hand")
            {
                // 选卡牌 (UI 或 3D 卡牌模型)
                // 你需要一个 CardSelectionManager 来处理高亮和点击卡牌
                // CardSelectionManager.Instance.StartCardSelection(request.Zone, request.Scope, request.FilterTag, onComplete);

                Debug.Log($"[Mock] 系统正在等待玩家在 {request.Zone} 选择一张 {request.FilterTag} 卡牌...");

                // --- 模拟测试代码 (因为还没有做选卡 UI) ---
                // 假设 2 秒后玩家选中了 ID 为 999 的卡
                StartCoroutine(MockSelectionDelay(onComplete));
            }
            // 4. 【兜底报错】如果收到了不认识的指令
            else
            {
                Debug.LogError($"[LuaManager] 类型匹配失败！\n传入的是: {incomingType}\nLuaManager 引用的是: {instruction.GetType().Namespace} 下的定义。\n请检查是否在不同文件中定义了同名类！");
            }
        }

    }

    private IEnumerator MockSelectionDelay(System.Action<object> onComplete)
    {
        yield return new WaitForSeconds(2.0f); // 模拟思考
        onComplete?.Invoke(999); // 假装选中了 999
    }

    // 模拟防御的协程
    private System.Collections.IEnumerator MockDefenseSequence(WaitForDamageProcess request, System.Action<object> onComplete)
    {
        Debug.Log("<color=yellow>[System] 进入防御响应阶段...</color>");
        yield return new WaitForSeconds(1.0f);

        Debug.Log("[System] 对手打出【香霖堂的购物券】！防御力 +2");
        yield return new WaitForSeconds(0.5f);

        int blockedAmount = 2;
        int finalDamage = Mathf.Max(0, request.Amount - blockedAmount);

        Debug.Log($"[System] 伤害结算完毕，返回 Lua: {finalDamage}");

        // 必须调用这个！否则 Lua 醒不来
        onComplete?.Invoke(finalDamage);
    }

    private void GrantBasicResources(BaseCardSO cardData)
    {
        if (cardData is RelicCardSO relic)
        {
            var turnModule = FindObjectOfType<CrescentWreath.Modules.TurnModule>();
            if (turnModule != null)
                turnModule.AddResources(relic.coinBonus, relic.magicBonus);
        }
    }

    // ... CustomLoader 保持不变 ...
    // 自定义加载逻辑：让 Lua 知道去哪里找脚本
    private byte[] CustomLoader(ref string filepath)
    {
        // 1. 路径标准化：把 Lua 的包名点号转换为文件路径斜杠
        // 例如 "Cards.Relic.23030" -> "Cards/Relic/23030"
        filepath = filepath.Replace('.', '/');

        // 2. 构建基础路径
        string basePath = Path.Combine(Application.streamingAssetsPath, "Lua", filepath);

        // 3. 优先尝试加载 .lua (匹配你现在的实际文件)
        string pathLua = basePath + ".lua";
        if (File.Exists(pathLua))
        {
            return File.ReadAllBytes(pathLua);
        }

        // 4. 备用尝试加载 .lua.txt (防止Unity识别为文本资源时的后缀)
        string pathTxt = basePath + ".lua.txt";
        if (File.Exists(pathTxt))
        {
            return File.ReadAllBytes(pathTxt);
        }

        // 5. 调试信息 (如果都找不到，可以在这里打个Log看看找的到底是啥)
        // Debug.LogWarning($"[LuaLoader] 未找到脚本: {filepath} (尝试路径: {pathLua})");

        return null;
    }
}