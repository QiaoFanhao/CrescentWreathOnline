using UnityEngine;

// 1. 基础抽象类：所有卡牌的最小共性
public abstract class BaseCardSO : ScriptableObject
{
   [Header("核心识别")]
    public int cardId;
    public string cardName;

    [Header("逻辑配置")]
    [Tooltip("填写对应的 Lua 脚本文件名（不含 .lua.txt）")]
    public string luaScriptName; 

    public string GetArtKey() => cardId.ToString();
   
}

 public enum TargetType
    {
        None,           // 不需要目标 (如：赛钱箱)
        SingleEnemy,    // 需要选择一个敌人 (如：八卦炉)
        SingleAlly,     // 需要选择一个队友
        AnyPlayer       // 任意玩家
    }