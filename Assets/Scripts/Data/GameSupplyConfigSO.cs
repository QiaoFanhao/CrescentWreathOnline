using System.Collections.Generic;
using UnityEngine;

namespace CrescentWreath.Data
{
    [System.Serializable]
    public struct CardSupplyEntry
    {
        public RelicCardSO cardData; // 引用具体的宝具卡 SO
        public int quantity;         // 这张卡在牌堆里有几张 (比如红丝带写 4)
    }

    /// <summary>
    /// 游戏供应堆配置表
    /// 职责：定义一局游戏中，公共牌堆由哪些卡组成，每种卡有多少张。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSupplyConfig", menuName = "DBG/Configs/SupplyConfig")]
    public class GameSupplyConfigSO : ScriptableObject
    {
        [Header("宝具卡池配置")]
        [Tooltip("在这里配置所有可能出现在商店里的宝具及其数量")]
        public List<CardSupplyEntry> relicDeckEntries;

        [Header("异变卡池配置")]
        [Tooltip("异变卡通常各只有一张，但为了统一格式也放在这里")]
        public List<AnomalyCardSO> anomalyCards; // 异变卡一般不重复，直接用 List<SO> 即可
    }
}