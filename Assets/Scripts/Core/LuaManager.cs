using UnityEngine;
using XLua;
using System.IO;

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