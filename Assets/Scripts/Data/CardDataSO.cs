using System.Collections.Generic;
using UnityEngine;

// 定义防御类型：咒术、体术、通用
public enum DefenseType
{
    None,
    Magic,    // 咒术（黄色）
    Physical, // 体术（黑色）
    Universal // 通用（黄黑相间）
}

// 定义卡牌主类型
public enum CardType
{
    Character, // 角色卡
    Relic,     // 宝具卡
    Anomaly    // 异变卡
}



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

// 2. 宝具卡：牌库构筑的主体
[CreateAssetMenu(fileName = "NewRelic", menuName = "DBG/Cards/Relic")]
public class RelicCardSO : BaseCardSO
{
    [Header("费用与产出")]
    public int cost;             // 召唤费用
    public int magicBonus;       // 左侧红色：魔力值产出 (+X)
    public int coinBonus;        // 右侧蓝色：灵符值产出 (+X)

    [Header("防御属性")]
    public int defenseValue;     // 右下角数值
    public DefenseType defense;  // 咒术/体术/通用图标

    [Header("效果描述")]
    [TextArea] public string effectText;   // 使用效果
    [TextArea] public string adventEffect; // 某些宝具具备的“降临效果”
}

// 3. 角色卡：玩家的化身（不进入牌组）
[CreateAssetMenu(fileName = "NewCharacter", menuName = "DBG/Cards/Character")]
public class CharacterCardSO : BaseCardSO
{
    [Header("身份属性")]
    public string camp;          // 阵营（仅角色拥有）
    public string race;          // 种族

    [Header("技能组")]
    public string[] skills = new string[4]; // 恒定四个技能
}

// 4. 异变卡：外部环境与任务
[CreateAssetMenu(fileName = "NewAnomaly", menuName = "DBG/Cards/Anomaly")]
public class AnomalyCardSO : BaseCardSO
{
    [TextArea] public string adventEffect; // 翻开时的即时影响
    [TextArea] public string condition;    // 解决它的代价
    [TextArea] public string reward;       // 解决后的收益
}


