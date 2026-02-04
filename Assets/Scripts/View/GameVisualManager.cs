using UnityEngine;
using System.Collections.Generic;
using CrescentWreath.Core;
using CrescentWreath.Modules;
using System.Linq;

namespace CrescentWreath.View
{
    public class GameVisualManager : MonoBehaviour
    {
        [Header("核心引用")]
        public GameObject cardPrefab;       // 卡牌预制体
        public Transform cardSpawnRoot;     // 卡牌父节点 (VisualContext)
        public TurnModule turnModule;       // 获取当前回合玩家

        public TableZoneModule tableModule;// 获取桌面数据模块, 用于查询召唤区状态

        public CrescentWreath.Data.CardDatabaseSO cardDatabase;// 卡牌数据库

        // 运行时卡牌追踪字典 [关键优化]
        // Key: 简单的 GetHashCode 或 临时唯一ID (由于目前只有SO ID，我们用对象引用追踪)
        // 在正式项目中，ZoneModule 应该管理 RuntimeCard 实例而不仅是 int ID
        // 这里演示用 List 追踪当前场上的 View
        private List<CardView> _activeViews = new List<CardView>();

        [Header(" 手牌扇形参数 (运行时可调)")]
        [Tooltip("扇形半径：决定了弧线的弯曲程度。数值越大，弧线越直；数值越小，弧线越弯。")]
        [Range(10f, 100f)]
        public float handRadius = 22.0f;
        [Tooltip("前后距离修正：负数表示让牌往后退（远离桌子中心），正数表示往前（靠近中心）。")]
        public float handForwardOffset = -4.0f; // 默认给个 -4，正好对应你 Cube 的位置

        [Tooltip("单张牌夹角：决定了牌之间的疏密。")]
        [Range(1f, 15f)]
        public float handAnglePerCard = 5.0f;

        [Tooltip("最大总展开角度：防止牌太多时扇形展得太宽超出屏幕。")]
        [Range(20f, 90f)]
        public float handMaxTotalAngle = 40.0f;

        [Tooltip("高度修正：如果觉得牌陷在桌子里或者飞太高，微调这个")]
        public float heightOffset = 0.0f;

        public HandLayoutManager uiHandManager; // 拖入场景中的 HandArea_Panel
        public GameObject uiCardPrefab;        // 拖入你的 UICardPrefab
        public GameObject dummyCardBackPrefab; // 拖入只有卡背的 3D Prefab

        private void OnEnable()
        {
            GameEvent.OnCardMoved += HandleCardMove;
        }



        private void OnDisable()
        {
            GameEvent.OnCardMoved -= HandleCardMove;
        }

        public void InitializeDecks()
        {
            var board = BoardView.Instance;
            if (board == null) return;

            CreateFaceDownPile(board.anomalyDeckPos, "Visual_AnomalyDeck", tableModule.GetAnomalyDeckCount());
            CreateFaceDownPile(board.relicDeckPos, "Visual_RelicDeck", tableModule.GetRelicDeckCount());
            // 3. 布置樱花饼 (面朝上，固定 ID)
            // 请确认 22003 是樱花饼的 ID，如果不是请修改！
            if (cardDatabase != null)
            {
                var sakuraData = cardDatabase.GetCardById(22003);
                if (sakuraData != null)
                {
                    // 假设 TableModule 有 GetSakuraCount()，如果没有请看下一步
                    int count = tableModule.GetSakuraCount();
                    CreateFaceUpPile(board.sakuraMochiDeckPos, sakuraData, "Visual_SakuraMochi", count);
                }
            }

            // 4. 布置 4 个玩家的牌库 (面朝下)
            for (int i = 0; i < board.playerAreas.Length; i++)
            {
                int count = 40;
                // int count = playerModule.GetDeckCount(i);
                CreateFaceDownPile(board.playerAreas[i].deckPos, $"Visual_Deck_P{i}", count);
            }
        }

        // === 生成面朝下的牌堆（仅模型） ===
        private void CreateFaceDownPile(Transform anchor, string name, int count)
        {
            if (anchor == null) return;

            // 1. 如果没牌了，直接不生成（或者销毁已有的）
            if (count <= 0) return;

            GameObject pile = Instantiate(cardPrefab, cardSpawnRoot);
            pile.name = name;

            pile.transform.position = anchor.position;
            pile.transform.rotation = anchor.rotation;

            // 基础缩放
            Vector3 currentScale = cardPrefab.transform.localScale;

            // 2. [核心逻辑] 根据数量计算厚度
            // 假设每张牌厚度增加 0.005f (根据你的模型单位调整)
            // 限制一个最大厚度，防止牌太多变成柱子
            float thicknessFactor = 0.005f;
            float extraThickness = Mathf.Clamp(count * thicknessFactor, 0f, 0.5f);

            // 应用厚度 (假设厚度在 Z 轴，注意之前我们翻转了 X 轴)
            // 因为翻转了 180 度，Z 轴方向可能变了，这里直接加在 LocalScale 上即可
            currentScale.z += extraThickness;
            pile.transform.localScale = currentScale;

            // 翻面 (背面朝上)
            pile.transform.Rotate(180, 0, 0, Space.Self);

            // 禁用逻辑
            Destroy(pile.GetComponent<CardView>());
            var col = pile.GetComponent<BoxCollider>();
            if (col != null) col.enabled = false;
        }

        // === 生成面朝上的牌堆（如樱花饼） ===
        private void CreateFaceUpPile(Transform anchor, BaseCardSO data, string name, int count)
        {
            if (anchor == null || count <= 0) return;

            GameObject pile = Instantiate(cardPrefab, cardSpawnRoot);
            pile.name = name;

            pile.transform.position = anchor.position;
            pile.transform.rotation = anchor.rotation;

            // === 厚度计算逻辑 (与面朝下保持一致) ===
            Vector3 currentScale = cardPrefab.transform.localScale;
            float thicknessFactor = 0.005f;
            float extraThickness = Mathf.Clamp(count * thicknessFactor, 0f, 0.5f);

            // 注意：面朝上的牌没有翻转 180 度，所以厚度通常是加在 Z 轴 (如果你的卡牌模型是平躺的 Quad)
            // 或者 Y 轴 (如果你的卡牌模型是直立的 Cube 被放平了)
            // 请根据你的 Prefab 实际情况调整，这里假设厚度是 Z
            currentScale.z += extraThickness;
            pile.transform.localScale = currentScale;
            // =====================================

            CardView view = pile.GetComponent<CardView>();
            if (view != null)
            {
                view.Setup(data);
                view.enabled = false;
            }

            var col = pile.GetComponent<BoxCollider>();
            if (col != null) col.enabled = false;
        }

        private void HandleCardMove(BaseCardSO cardData, ZoneType fromZone, ZoneType toZone, int ownerId, int subIndex)
        {
            // =============================================================
            // 场景 1：本地玩家 (P0) 的手牌操作 -> 全部交给 UI
            // =============================================================
            if (ownerId == 0 && toZone == ZoneType.Hand)
            {
                // 如果是从 3D 区域（如牌库）飞来的，我们可能需要一个从 3D 坐标转 UI 坐标的动画
                // 目前简单处理：直接在 UI 生成
                SpawnLocalUIHandCard(cardData);
                return;
            }

            // =============================================================
            // 场景 2：对手 (P1-3) 的手牌操作 -> 生成 3D 假卡背
            // =============================================================
            if (ownerId != 0 && toZone == ZoneType.Hand)
            {
                SpawnRemoteDummyCard(ownerId);
                UpdateHandLayout(ownerId); // 这里的 UpdateHandLayout 会处理 dummyCards 的 3D 扇形
                return;
            }

            // =============================================================
            // 场景 3：任何卡牌进入公开区 (战场/召唤区) -> 实体化为 3D CardView
            // =============================================================
            if (toZone == ZoneType.Battlefield || toZone == ZoneType.SummonZone || toZone == ZoneType.Discard)
            {
                // 1. 如果是从手牌打出的，先销毁来源
                if (fromZone == ZoneType.Hand)
                {
                    if (ownerId == 0) RemoveLocalUIHandCard(cardData);
                    else RemoveRemoteDummyCard(ownerId);
                }

                // 2. 生成真正的 3D 实体
                CardView cardView = SpawnCard(cardData);
                cardView.UpdateState(ownerId, toZone);

                // 3. 计算位置并飞行
                Transform targetAnchor = CalculateTargetPosition(toZone, ownerId, subIndex);
                if (targetAnchor != null)
                {
                    cardView.MoveTo(targetAnchor.position, targetAnchor.rotation);
                }
            }
        }

        // 1. 生成本地 UI 手牌
        private void SpawnLocalUIHandCard(BaseCardSO data)
        {
            if (uiHandManager == null) return;
            GameObject go = Instantiate(uiCardPrefab, uiHandManager.transform);
            UICardView uiView = go.GetComponent<UICardView>();
            uiView.Setup(data);
            // 注意：HandLayoutManager 会通过 OnTransformChildrenChanged 自动感知并排版
        }

        // 2. 生成对手 3D 假卡背
        private void SpawnRemoteDummyCard(int ownerId)
        {
            // 这里我们可以复用 _activeViews 列表，或者开一个专门的 _dummyViews 列表
            GameObject go = Instantiate(dummyCardBackPrefab, cardSpawnRoot);
            CardView dummyView = go.AddComponent<CardView>(); // 给它加个临时的 View 方便移动和追踪
            dummyView.UpdateState(ownerId, ZoneType.Hand);
            _activeViews.Add(dummyView);
        }

        // 3. 移除本地 UI 卡牌
        private void RemoveLocalUIHandCard(BaseCardSO data)
        {
            // 根据数据找到对应的 UI 物体并销毁
            foreach (Transform child in uiHandManager.transform)
            {
                var uiView = child.GetComponent<UICardView>();
                if (uiView != null && uiView._cardData == data)
                {
                    Destroy(child.gameObject);
                    break;
                }
            }
        }

        // 4. 移除对手的一个卡背
        private void RemoveRemoteDummyCard(int ownerId)
        {
            var dummy = _activeViews.FirstOrDefault(v => v.currentOwnerId == ownerId && v.currentZone == ZoneType.Hand);
            if (dummy != null)
            {
                _activeViews.Remove(dummy);
                Destroy(dummy.gameObject);
            }
        }

        private void UpdateHandLayout(int playerId)
        {
            var board = BoardView.Instance;
            if (board == null || playerId < 0 || playerId >= board.playerAreas.Length) return;

            Transform pivot = board.playerAreas[playerId].handPivot;

            // 1. 筛选 (代码不变)
            List<CardView> handCards = _activeViews
                .Where(v => v != null && v.currentOwnerId == playerId && v.currentZone == ZoneType.Hand)
                .ToList();

            int count = handCards.Count;
            if (count == 0) return;

            // 2. 参数使用 (直接使用面板上的变量！)
            float currentTotalAngle = (count - 1) * handAnglePerCard;
            if (currentTotalAngle > handMaxTotalAngle)
            {
                currentTotalAngle = handMaxTotalAngle;
                // 动态压缩间距
                // 注意：这里我们定义临时变量 dynamicAngle，不修改面板上的 handAnglePerCard
                float dynamicAngle = currentTotalAngle / (count - 1);

                // 起始角度
                float startAngle = currentTotalAngle / 2f;

                ApplyFanLayout(handCards, pivot, dynamicAngle, startAngle);
            }
            else
            {
                // 正常间距
                float startAngle = currentTotalAngle / 2f;
                ApplyFanLayout(handCards, pivot, handAnglePerCard, startAngle);
            }
        }
        /// <summary>
        /// 应用扇形排版 (修复版：使用 TransformPoint 实现本地坐标转世界坐标)
        /// </summary>
        private void ApplyFanLayout(List<CardView> cards, Transform pivot, float angleStep, float startAngle)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];

                // 1. [纯本地] 计算角度
                float angle = startAngle - (i * angleStep);

                // 2. [纯本地] 构造扇形
                Quaternion localRotation = Quaternion.Euler(0, 0, angle);

                // 3. [纯本地] 计算位置偏移
                // Vector3.up * handRadius -> 扇形高度
                // new Vector3(0, 0, i * 0.01f) -> 防穿模层级
                // 【关键修改】 new Vector3(0, 0, handForwardOffset) -> 统一往后拉的距离！
                Vector3 localPos = (localRotation * Vector3.up * handRadius)
                                   + new Vector3(0, 0, i * 0.01f + handForwardOffset);

                // 应用高度修正
                localPos.y += heightOffset;

                // 4. [关键转换] Local -> World
                Vector3 targetPos = pivot.TransformPoint(localPos);

                // 5. 计算最终旋转
                Quaternion targetRot = pivot.rotation * localRotation;

                // 6. 执行移动
                card.MoveTo(targetPos, targetRot, 0.4f);
            }
        }
        /// <summary>
        /// 核心逻辑：根据 区域 + 谁的 + 卡牌ID 决定具体位置
        /// </summary>
        private Transform CalculateTargetPosition(ZoneType zone, int ownerId, int subIndex)
        {
            var board = BoardView.Instance;
            if (board == null) return null;

            // === 公共区域 ===
            if (ownerId == -1)
            {
                if (zone == ZoneType.SummonZone)
                {
                    // 【关键修复】直接使用传进来的 subIndex
                    // 如果逻辑层传了有效的索引 (>=0)，直接用它！
                    if (subIndex >= 0)
                    {
                        return board.GetSummonSlot(subIndex);
                    }
                    else
                    {
                        // 降级方案：如果不传索引，才去查表（但这就可能导致重叠）
                        Debug.LogWarning("召唤区移动未指定 subIndex，可能会重叠！");
                        return board.GetSummonSlot(0);
                    }
                }
                if (zone == ZoneType.AnomalyDeck) return board.anomalyDeckPos;
            }

            // === 玩家区域 ===
            if (ownerId < 0 || ownerId >= board.playerAreas.Length) return null;
            var playerArea = board.playerAreas[ownerId];

            switch (zone)
            {
                case ZoneType.Hand: return playerArea.handPivot;
                case ZoneType.Deck: return playerArea.deckPos;
                case ZoneType.Discard: return playerArea.discardPos;
                case ZoneType.Battlefield:
                    if (ownerId == turnModule.activePlayerId) return playerArea.playZoneCenter;
                    else return playerArea.playZoneCenter;
                default: return null;
            }
        }

        // 获取某个区域的起始锚点 (用于设置初始位置)
        private Transform GetZoneAnchor(ZoneType zone, int ownerId)
        {
            return CalculateTargetPosition(zone, ownerId, -1);
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

        private void UpdateDeckThicknessVisual(ZoneType zone, int ownerId)
        {
            // 1. 确定要更新哪个牌堆
            string targetName = "";
            int newCount = 0;

            if (ownerId == -1) // 公共区域
            {
                if (zone == ZoneType.SakuraMochiDeck)
                {
                    targetName = "Visual_SakuraMochi";
                    newCount = tableModule.GetSakuraCount();
                }
                else if (zone == ZoneType.Deck) // 假设这是主牌库
                {
                    targetName = "Visual_RelicDeck";
                    newCount = tableModule.GetRelicDeckCount();
                }
            }
            else // 玩家区域
            {
                if (zone == ZoneType.Deck)
                {
                    targetName = $"Visual_Deck_P{ownerId}";
                    // newCount = playerModule.GetDeckCount(ownerId); // 需要 PlayerModule 支持
                    return; // 暂时跳过玩家
                }
            }

            if (string.IsNullOrEmpty(targetName)) return;

            // 2. 找到场景里的这个物体
            // 因为我们之前给物体起了固定的名字，所以可以用 Find (虽然性能一般，但在发牌这种低频事件里没问题)
            // 更好的做法是用 Dictionary<string, Transform> 缓存这些牌堆
            Transform deckTrans = cardSpawnRoot.Find(targetName);

            if (deckTrans != null)
            {
                if (newCount <= 0)
                {
                    deckTrans.gameObject.SetActive(false); // 没牌了就隐藏
                }
                else
                {
                    deckTrans.gameObject.SetActive(true);
                    // 重新计算厚度
                    Vector3 scale = cardPrefab.transform.localScale;
                    float thickness = Mathf.Clamp(newCount * 0.005f, 0f, 0.5f);
                    scale.z += thickness;
                    deckTrans.localScale = scale;
                }
            }
        }

    }
}