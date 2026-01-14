using System.Collections.Generic;
using UnityEngine;
using CrescentWreath.Core;
using CrescentWreath.Data;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// 公共区域模块 (Server Logic)
    /// 职责：管理召唤区、异变堆、樱花饼堆的数据流转。
    /// </summary>
    public class TableZoneModule : MonoBehaviour
    {
        [Header("数据容器")]
        [SerializeField] private List<int> _summonZone = new List<int>();       // 召唤区 (6格) [cite: 151]
        [SerializeField] private List<int> _anomalyDeck = new List<int>();      // 异变牌堆 [cite: 147]
        [SerializeField] private List<int> _sakuraMochiDeck = new List<int>();  // 樱花饼堆 [cite: 146]

        [Header("依赖配置")]
        public GameSupplyConfigSO supplyConfig;
        public CardDatabaseSO cardDatabase;

        private List<int> _relicPool = new List<int>(); // 隐藏的宝具主牌堆

        /// <summary>
        /// 由 GameManager 或 TurnModule 调用，初始化桌面
        /// </summary>
        public void InitializeTable()
        {
            _summonZone.Clear();
            _anomalyDeck.Clear();
            _sakuraMochiDeck.Clear();
            _relicPool.Clear();

            // 1. 填充樱花饼
            for (int i = 0; i < 15; i++) _sakuraMochiDeck.Add(22003);

            // 2. 准备异变堆
            if (supplyConfig != null)
            {
                foreach (var anomaly in supplyConfig.anomalyCards)
                    _anomalyDeck.Add(anomaly.cardId);
                ShuffleList(_anomalyDeck);
            }

            // 3. 准备宝具池
            if (supplyConfig != null)
            {
                foreach (var entry in supplyConfig.relicDeckEntries)
                {
                    for (int i = 0; i < entry.quantity; i++)
                        _relicPool.Add(entry.cardData.cardId);
                }
                ShuffleList(_relicPool);
            }

            // =========================================================
            // 【修改】注释掉下面的自动发牌！
            // 把发牌的控制权交给 GameManager 的协程去逐个调用 DrawCardToSummonSlot
            // =========================================================
            /* Debug.Log("<color=orange>[Table]</color> --- 召唤区初始化 ---");
            for (int i = 0; i < 6; i++)
            {
                // 原有的瞬间填充逻辑已注释
            }
            */
            
            Debug.Log("<color=green>[Table]</color> 数据初始化完成，等待 GameManager 发牌...");
        }
        private void ShuffleList(List<int> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int temp = list[i];
                int r = Random.Range(i, list.Count);
                list[i] = list[r];
                list[r] = temp;
            }
        }

        private void BroadcastMove(int cardId, ZoneType from, ZoneType to)
        {
            var cardSO = cardDatabase.GetCardById(cardId);
            if (cardSO != null) GameEvent.OnCardMoved?.Invoke(cardSO, from, to, -1, -1);
        }

        /// <summary>
        ///  获取某张卡在召唤区列表中的索引 (0-5)
        /// 视觉层会调用这个来决定把卡画在哪个格子里
        /// </summary>
        public int GetSummonIndex(int cardId)
        {
            // _summonZone 是你存储当前场上卡牌ID的 List<int>
            return _summonZone.IndexOf(cardId);
        }
        // =========================================================
        // 【新增】动作指令接口 (供 GameManager 流程调用)
        // =========================================================

        /// <summary>
        /// 从主牌库抽一张牌，填入指定的召唤区格子
        /// </summary>
        public void DrawCardToSummonSlot(int slotIndex)
        {
            // 1. 检查牌库是否有牌
            if (_relicPool.Count == 0) return; // 对应 line 22 defined _relicPool

            // 2. 数据层操作
            int cardId = _relicPool[0];
            _relicPool.RemoveAt(0);

            // 确保列表容量足够 (防止 IndexOutOfRangeException)
            while (_summonZone.Count <= slotIndex)
            {
                _summonZone.Add(0); // 对应 line 14 defined _summonZone
            }
            _summonZone[slotIndex] = cardId;

            // 3. 广播事件 (带上 subIndex!)
            var cardData = cardDatabase.GetCardById(cardId);
            
            // 注意：这里使用了 5 个参数的 Invoke。
            // 请确保你已经修改了 GameEvent.OnCardMoved 的定义，增加了 subIndex 参数
            GameEvent.OnCardMoved?.Invoke(cardData, ZoneType.Deck, ZoneType.SummonZone, -1, slotIndex);
            
            Debug.Log($"<color=orange>[Table]</color> 填充槽位 {slotIndex}: {cardData.cardName}");
        }

        /// <summary>
        /// 获取当前异变堆顶的 ID (只看不拿，用于检查命运长夜)
        /// </summary>
        public int PeekTopAnomaly()
        {
            if (_anomalyDeck.Count == 0) return 0; // 对应 line 15 defined _anomalyDeck
            return _anomalyDeck[0];
        }

        /// <summary>
        /// 重新洗混异变堆
        /// </summary>
        public void ShuffleAnomalyDeck()
        {
            ShuffleList(_anomalyDeck);
            Debug.Log("<color=purple>[Table]</color> 异变堆已重新洗牌");
        }

        /// <summary>
        /// 正式翻开堆顶的异变卡 (移动到场上)
        /// </summary>
        public void RevealTopAnomaly()
        {
            if (_anomalyDeck.Count == 0) return;
            
            int cardId = _anomalyDeck[0];
            _anomalyDeck.RemoveAt(0);
            
            // 这里我们假设异变翻开后也是去 Battlefield，或者是专门的 AnomalySlot
            // 目前先复用 Battlefield，subIndex 设为 0
            var cardData = cardDatabase.GetCardById(cardId);
            
            // 广播：异变堆 -> 战场(或异变区)
            GameEvent.OnCardMoved?.Invoke(cardData, ZoneType.AnomalyDeck, ZoneType.Battlefield, -1, 0);
            
            Debug.Log($"<color=purple>[Table]</color> 异变发生: {cardData.cardName}");
        }

        // 获取牌堆数量 (用于视觉层决定是否显示牌背模型)
        public int GetRelicDeckCount() => _relicPool.Count;
        public int GetAnomalyDeckCount() => _anomalyDeck.Count;
    }
}