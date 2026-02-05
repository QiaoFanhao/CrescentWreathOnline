using UnityEngine;
using CrescentWreath.Data;
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
    [Header("Targeting")]
    public TargetType targetType = TargetType.None; // 默认为无目标
}
