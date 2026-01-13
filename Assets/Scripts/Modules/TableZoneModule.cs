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

            // 1. 填充樱花饼 (固定 ID 22003, 15张) [cite: 9]
            for (int i = 0; i < 15; i++) _sakuraMochiDeck.Add(22003);

            // 2. 准备异变堆 [cite: 11]
            if (supplyConfig != null)
            {
                foreach (var anomaly in supplyConfig.anomalyCards)
                    _anomalyDeck.Add(anomaly.cardId);
                ShuffleList(_anomalyDeck);
            }

            // 3. 准备宝具池 (按配置数量填充) [cite: 9]
            if (supplyConfig != null)
            {
                foreach (var entry in supplyConfig.relicDeckEntries)
                {
                    for (int i = 0; i < entry.quantity; i++)
                        _relicPool.Add(entry.cardData.cardId);
                }
                ShuffleList(_relicPool);
            }

            // 4. 初始化召唤区 (翻开6张)
            Debug.Log("<color=orange>[Table]</color> --- 召唤区初始化 ---");
            for (int i = 0; i < 6; i++)
            {
                if (_relicPool.Count > 0)
                {
                    int id = _relicPool[0];
                    _relicPool.RemoveAt(0);
                    _summonZone.Add(id);

                    // [新增日志] 打印具体翻开的宝具名
                    var card = cardDatabase.GetCardById(id);
                    Debug.Log($"<color=orange>[Table]</color> 召唤区槽位 {i + 1}: <b>{card.cardName}</b> (ID:{id})");

                    BroadcastMove(id, ZoneType.Unknown, ZoneType.SummonZone);
                }
            }

            // 5. [新增日志] 打印当前异变堆顶
            if (_anomalyDeck.Count > 0)
            {
                var topAnomaly = cardDatabase.GetCardById(_anomalyDeck[0]);
                Debug.Log($"<color=purple>[Table]</color> 当前异变堆顶: <b>{topAnomaly.cardName}</b>");
            }
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
            if (cardSO != null) GameEvent.OnCardMoved?.Invoke(cardSO, from, to, -1);
        }
    }
}