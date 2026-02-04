using UnityEngine;
using XLua;
using System.IO;
using CrescentWreath.Lua;
public class LuaManager : MonoBehaviour
{
    // 单例，方便全局访问
    public static LuaManager Instance { get; private set; }

    private LuaEnv _luaEnv;

    // 提供一个属性获取虚拟机，如果需要的话
    public LuaEnv GetLuaEnv() => _luaEnv;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景不销毁，保证虚拟机持续存在
            InitLuaEnv();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitLuaEnv()
    {
        _luaEnv = new LuaEnv();

        // 核心技巧：设置自定义加载器（Loader）
        // 这样 Lua 的 'require' 就能找到你在 StreamingAssets 或其他地方的脚本
        _luaEnv.AddLoader(CustomLoader);
    }

    // 自定义加载逻辑：让 Lua 知道去哪里找 .lua.txt 文件
    private byte[] CustomLoader(ref string filepath)
    {
        // 假设你的脚本都在 StreamingAssets/Lua/ 下，且后缀为 .lua.txt
        string path = Path.Combine(Application.streamingAssetsPath, "Lua", filepath + ".lua.txt");

        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        return null;
    }

    public void ExecuteCardEffect(BaseCardSO cardData, int targetId, int ownerId)
{
    // 1. 确定分类文件夹 (根据 Character, Relic, Anomaly 分类)
    string category = cardData is CharacterCardSO ? "Character" : 
                     (cardData is RelicCardSO ? "Relic" : "Anomaly");

    // 2. 确定脚本名称：优先使用 luaName，若为空则使用 cardId
    string scriptName = string.IsNullOrEmpty(cardData.luaScriptName) ? 
                        cardData.cardId.ToString() : cardData.luaScriptName;

    // 3. 拼接 require 路径 (CustomLoader 会自动加上 StreamingAssets/Lua/ 前缀)
    // 路径示例: "Cards/Relic/23030"
    string requirePath = $"Cards/{category}/{scriptName}";

    try
    {
        // 4. 加载 Lua 脚本并获取返回的 Table
        // 脚本结尾必须有 return Card_XXXX，否则 scriptTable 为空
        LuaTable scriptTable = _luaEnv.DoString($"return require('{requirePath}')")[0] as LuaTable;

        if (scriptTable != null)
        {
            // 5. 构造 Context 对象传递给 Lua
            // 这里假设你已经引用了场景中的 TurnModule 和 PlayerZoneModules
            var turnModule = Object.FindFirstObjectByType<CrescentWreath.Modules.TurnModule>();
            var context = new LuaEffectContext(ownerId, targetId, cardData, turnModule, turnModule.playerZoneModules);

            // 6. 获取并执行 OnPlay 函数
            LuaFunction onPlayFunc = scriptTable.Get<LuaFunction>("OnPlay");
            onPlayFunc?.Call(scriptTable, context);
            
            // 7. 逻辑回归：Lua 执行完异能后，自动结算 SO 里的基础资源
            if (cardData is RelicCardSO relic)
            {
                turnModule.AddResources(relic.coinBonus, relic.magicBonus);
            }
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[Lua] 执行脚本 {requirePath} 失败: {e.Message}");
    }
}

    public void DoString(string code)
    {
        _luaEnv?.DoString(code);
    }

    public void DoFile(string fileName)
    {
        // 在 Lua 里执行 require 'filename'
        _luaEnv?.DoString($"require('{fileName}')");
    }

    private void Update()
    {
        // 记得定时清理 Lua 内存中不再使用的垃圾对象
        if (_luaEnv != null)
        {
            _luaEnv.Tick();
        }
    }

    private void OnDestroy()
    {
        // 释放虚拟机
        _luaEnv?.Dispose();
        _luaEnv = null;
    }
}