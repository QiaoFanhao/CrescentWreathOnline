using System.Collections.Generic;
using UnityEngine;
using CrescentWreath.Core;
using CrescentWreath.Data;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// 个人区域模块 (Server Logic)
    /// 职责：管理单个玩家的牌库、手牌、弃牌堆、阵地区。
    /// </summary>
    public class PlayerZoneModule : MonoBehaviour
    {
        public int playerId;

        [Header("私有容器")]
        [SerializeField] private List<int> _deck = new List<int>();        // 牌库 
        [SerializeField] private List<int> _hand = new List<int>();        // 手牌
        [SerializeField] private List<int> _discard = new List<int>();     // 弃牌堆 [cite: 166]
        [SerializeField] private List<int> _battlefield = new List<int>(); // 阵地区 [cite: 169]

        public CardDatabaseSO cardDatabase;

        public int HandCount => _hand.Count;

        private void OnEnable()
        {
            // 只响应属于自己的请求 (未来在网络层过滤)
            GameEvent.Request_PlayHandCard += OnRequestPlayCard;
            GameEvent.Request_DiscardHandCard += OnRequestDiscard;
        }

        private void OnDisable()
        {
            GameEvent.Request_PlayHandCard -= OnRequestPlayCard;
            GameEvent.Request_DiscardHandCard -= OnRequestDiscard;
        }

        /// <summary>
        /// 初始化个人起始卡组 (3魔术回路 + 7购物券) [cite: 8, 57]
        /// </summary>
        public void InitializePlayerDeck()
        {
            _deck.Clear();
            _hand.Clear();
            _discard.Clear();
            _battlefield.Clear();

            for (int i = 0; i < 3; i++) _deck.Add(21002);
            for (int i = 0; i < 7; i++) _deck.Add(21001);

            ShuffleDeck();
            DrawCards(6); // 初始抽6张 [cite: 175]
        }

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_deck.Count == 0)
                {
                    if (_discard.Count == 0) break;
                    RecycleDiscard();
                }

                int id = _deck[0];
                _deck.RemoveAt(0);
                _hand.Add(id);
                BroadcastMove(id, ZoneType.Deck, ZoneType.Hand);
            }
        }

        private void RecycleDiscard()
        {
            _deck.AddRange(_discard);
            _discard.Clear();
            ShuffleDeck();
            // 可以通知UI播放洗牌动画
        }

        private void OnRequestPlayCard(int index)
        {
            // 逻辑由 TurnModule 校验权限后执行
            if (index < 0 || index >= _hand.Count) return;

            int id = _hand[index];
            _hand.RemoveAt(index);
            _battlefield.Add(id);
            BroadcastMove(id, ZoneType.Hand, ZoneType.Battlefield);
        }

        private void OnRequestDiscard(int index)
        {
            if (index < 0 || index >= _hand.Count) return;

            int id = _hand[index];
            _hand.RemoveAt(index);
            _discard.Add(id);
            BroadcastMove(id, ZoneType.Hand, ZoneType.Discard);
        }

        public void ClearBattlefield()
        {
            foreach (var id in _battlefield)
            {
                _discard.Add(id);
                BroadcastMove(id, ZoneType.Battlefield, ZoneType.Discard);
            }
            _battlefield.Clear();
        }

        private void ShuffleDeck()
        {
            for (int i = 0; i < _deck.Count; i++)
            {
                int temp = _deck[i];
                int r = Random.Range(i, _deck.Count);
                _deck[i] = _deck[r];
                _deck[r] = temp;
            }
        }

        private void BroadcastMove(int cardId, ZoneType from, ZoneType to)
        {
            // 【核心逻辑】数据隔离
            // 只有在以下情况才发送真实的卡牌数据：
            // 1. 这张牌属于本地玩家 (playerId == 0)
            // 2. 这张牌进入了公开区域 (如 Battlefield 或 SummonZone)

            bool isLocalPlayer = (playerId == 0);
            bool isPublicZone = (to == ZoneType.Battlefield || to == ZoneType.SummonZone || to == ZoneType.Discard);

            BaseCardSO cardData = null;

            if (isLocalPlayer || isPublicZone)
            {
                cardData = cardDatabase.GetCardById(cardId);
            }
            else
            {
                // 对手的手牌，我们只传一个空数据，View 层据此生成“卡背”
                cardData = null;
            }

            GameEvent.OnCardMoved?.Invoke(cardData, from, to, playerId, -1);
        }/*  */

        public int GetDeckCount(int playerId)
        {
            // 这里返回该玩家实际的牌库数量
            // return playerDecks[playerId].Count; 
            return 40; // 暂时写死测试，你需替换为真实逻辑
        }
    }
}