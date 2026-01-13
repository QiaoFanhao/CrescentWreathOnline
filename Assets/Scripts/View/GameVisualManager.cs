using UnityEngine;
using System.Collections.Generic;
using CrescentWreath.Core;
using CrescentWreath.Modules;

namespace CrescentWreath.View
{
    public class GameVisualManager : MonoBehaviour
    {
        [Header("核心引用")]
        public GameObject cardPrefab;       // 卡牌预制体
        public Transform cardSpawnRoot;     // 卡牌父节点 (VisualContext)
        public TurnModule turnModule;       // 获取当前回合玩家

        // 运行时卡牌追踪字典 [关键优化]
        // Key: 简单的 GetHashCode 或 临时唯一ID (由于目前只有SO ID，我们用对象引用追踪)
        // 在正式项目中，ZoneModule 应该管理 RuntimeCard 实例而不仅是 int ID
        // 这里演示用 List 追踪当前场上的 View
        private List<CardView> _activeViews = new List<CardView>();

        private void OnEnable()
        {
            GameEvent.OnCardMoved += HandleCardMove;
        }

        private void OnDisable()
        {
            GameEvent.OnCardMoved -= HandleCardMove;
        }

        private void HandleCardMove(BaseCardSO cardData, ZoneType fromZone, ZoneType toZone, int ownerId)
        {
            // 1. 获取或生成卡牌 View
            // (简化逻辑：每次移动都视为“表现上的新生成”或“查找空闲View”，为了演示流畅性，这里直接生成)
            // *进阶提示：未来这里应该写一个查找逻辑：如果场上已经有这张牌的View（比如从手牌出），就移动它，而不是生成新的*
            CardView cardView = SpawnCard(cardData);

            // 2. 初始位置修正 (如果是由“无”变出来的)
            if (fromZone == ZoneType.Unknown || fromZone == ZoneType.Deck)
            {
                // 从对应玩家的牌库位置发出
                Transform startAnchor = GetZoneAnchor(ZoneType.Deck, ownerId);
                if (startAnchor) cardView.transform.position = startAnchor.position;
            }
            // 如果是从手牌打出，应该找到手牌区里对应的那张牌 (这里简化为瞬移到手牌区再飞)
            else if (fromZone == ZoneType.Hand)
            {
                Transform handAnchor = GetZoneAnchor(ZoneType.Hand, ownerId);
                if (handAnchor) cardView.transform.position = handAnchor.position;
            }

            // 3. 计算目标落点
            Transform targetAnchor = CalculateTargetPosition(toZone, ownerId);

            // 4. 执行飞行动画
            if (targetAnchor != null)
            {
                // 加上一点随机偏移，防止完全重叠看着像一张牌
                Vector3 offset = new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(0, 0.05f), Random.Range(-0.02f, 0.02f));

                // 如果是去手牌，需要特殊的扇形计算 (暂时先飞到 Pivot 中心)
                // TODO: 下一步我们会写 UpdateHandVisuals 来排列它们

                cardView.MoveTo(targetAnchor.position + offset, targetAnchor.rotation);
            }
        }

        /// <summary>
        /// 核心逻辑：根据 区域类型 + 拥有者 + 当前回合 决定去哪里
        /// </summary>
        private Transform CalculateTargetPosition(ZoneType zone, int ownerId)
        {
            var board = BoardView.Instance;
            if (board == null) return null;

            // 处理公共区域
            if (ownerId == -1)
            {
                if (zone == ZoneType.SummonZone) return board.GetSummonSlot(Random.Range(0, 6)); // 临时随机
                if (zone == ZoneType.AnomalyDeck) return board.anomalyDeckPos;
            }

            // 处理玩家区域
            // 防止数组越界
            if (ownerId < 0 || ownerId >= board.playerAreas.Length) return null;
            var playerArea = board.playerAreas[ownerId];

            switch (zone)
            {
                case ZoneType.Hand:
                    return playerArea.handPivot;

                case ZoneType.Deck:
                    return playerArea.deckPos;

                case ZoneType.Discard:
                    return playerArea.discardPos;

                case ZoneType.Battlefield:
                    // === [关键需求] FieldViewZone 映射逻辑 ===
                    // 规则：只有“当前回合的玩家”打出的牌，才映射到中央蓝色区域 (FieldViewZone)
                    // 其他玩家(比如响应防御)的牌，留在自己的 PlayZone

                    if (ownerId == turnModule.activePlayerId)
                    {
                        // 假设 BoardView 里 P0 的 playZoneCenter 已经被你放置在了蓝色区域
                        // 那么对于 P0 来说，这就是蓝色区域
                        // 对于 P1/P2/P3，你需要确保他们的 playZoneCenter 也是指向中央蓝色区域吗？
                        // 或者根据你的描述，蓝色区域是“公用的映射区”。

                        // 如果蓝色区域是 BoardView 上的一个独立锚点：
                        // return board.fieldViewZoneAnchor; 

                        // 如果你想复用玩家自己的锚点配置：
                        return playerArea.playZoneCenter;
                    }
                    else
                    {
                        // 非当前玩家（如防御牌），通常显示在自己面前
                        return playerArea.playZoneCenter;
                    }

                default:
                    return null;
            }
        }

        // 获取某个区域的起始锚点 (用于设置初始位置)
        private Transform GetZoneAnchor(ZoneType zone, int ownerId)
        {
            return CalculateTargetPosition(zone, ownerId);
        }

        private CardView SpawnCard(BaseCardSO data)
        {
            // 1. 实例化 (挂在父节点下)
            GameObject go = Instantiate(cardPrefab, cardSpawnRoot);

            // 2. 命名 (方便调试)
            go.name = $"Card_{data.cardName}_{go.GetInstanceID()}";

            // =================================================================
            // 【关键修复】 强制恢复预制体原本的 Scale
            // 这行代码会无视 Unity 自动计算的缩放，强制应用你 Prefab 里的 (0.63, 0.88, 0.01)
            // =================================================================
            go.transform.localScale = cardPrefab.transform.localScale;

            // 3. 初始化数据
            CardView view = go.GetComponent<CardView>();
            view.Setup(data);

            _activeViews.Add(view);
            return view;
        }
    }
}