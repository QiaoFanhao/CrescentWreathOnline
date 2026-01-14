using UnityEngine;

// 1. 基础抽象类：所有卡牌的最小共性
public abstract class BaseCardSO : ScriptableObject
{
   [Header("核心识别")]
    public int cardId;
    public string cardName;

    [Header("逻辑配置")]
    [Tooltip("填写对应的 Lua 脚本文件名（不含 .lua.txt）")]
    public string luaScriptName; //  补全这个关键的“灵魂指针”

    public string GetArtKey() => cardId.ToString();
}