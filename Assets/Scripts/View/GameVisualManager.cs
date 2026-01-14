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

        public TableZoneModule tableModule;// 获取桌面数据模块, 用于查询召唤区状态

        public CrescentWreath.Data.CardDatabaseSO cardDatabase;// 卡牌数据库

        // 运行时卡牌追踪字典 [关键优化]
        // Key: 简单的 GetHashCode 或 临时唯一ID (由于目前只有SO ID，我们用对象引用追踪)
        // 在正式项目中，ZoneModule 应该管理 RuntimeCard 实例而不仅是 int ID
        // 这里演示用 List 追踪当前场上的 View
        private List<CardView> _activeViews = new List<CardView>();

        private void OnEnable()
        {
            GameEvent.OnCardMoved += HandleCardMove;
        }

        private void Start()
        {
            // 游戏一运行，直接先把牌堆摆出来
            InitializeDecks();
        }

        private void OnDisable()
        {
            GameEvent.OnCardMoved -= HandleCardMove;
        }

        public void InitializeDecks()
        {
            var board = BoardView.Instance;
            if (board == null) return;

            // 1. 布置异变牌堆 (面朝下)
            CreateFaceDownPile(board.anomalyDeckPos, "Visual_AnomalyDeck");

            // 2. 布置主牌库 (面朝下)
            CreateFaceDownPile(board.relicDeckPos, "Visual_RelicDeck");

            // 3. 布置樱花饼 (面朝上，固定 ID)
            // 请确认 22003 是樱花饼的 ID，如果不是请修改！
            if (cardDatabase != null)
            {
                var sakuraData = cardDatabase.GetCardById(22003);
                if (sakuraData != null)
                {
                    CreateFaceUpPile(board.sakuraMochiDeckPos, sakuraData, "Visual_SakuraMochi");
                }
            }

            // 4. 布置 4 个玩家的牌库 (面朝下)
            for (int i = 0; i < board.playerAreas.Length; i++)
            {
                CreateFaceDownPile(board.playerAreas[i].deckPos, $"Visual_Deck_P{i}");
            }
        }

        // === 生成面朝下的牌堆（仅模型） ===
        private void CreateFaceDownPile(Transform anchor, string name)
        {
            if (anchor == null) return;

            GameObject pile = Instantiate(cardPrefab, cardSpawnRoot);
            pile.name = name;
            
            pile.transform.position = anchor.position;
            pile.transform.rotation = anchor.rotation;
            pile.transform.localScale = cardPrefab.transform.localScale;

            // 翻面
            pile.transform.Rotate(180, 0, 0, Space.Self);
            
            // 加厚
            pile.transform.localScale += new Vector3(0, 0, 0.05f);

            // 【关键修改】不要 Destroy 碰撞体，而是禁用它！
            Destroy(pile.GetComponent<CardView>()); // 脚本还是可以删的
            
            var col = pile.GetComponent<BoxCollider>();
            if (col != null) col.enabled = false; // 只是关掉，不删，这样就不会报错了
        }

        // === 生成面朝上的牌堆（如樱花饼） ===
        private void CreateFaceUpPile(Transform anchor, BaseCardSO data, string name)
        {
            if (anchor == null) return;

            GameObject pile = Instantiate(cardPrefab, cardSpawnRoot);
            pile.name = name;
            
            pile.transform.position = anchor.position;
            pile.transform.rotation = anchor.rotation;
            pile.transform.localScale = cardPrefab.transform.localScale;

            CardView view = pile.GetComponent<CardView>();
            if (view != null)
            {
                view.Setup(data);
                view.enabled = false; // 禁用脚本逻辑
            }
            
            // 【关键修改】同样改为禁用碰撞体
            var col = pile.GetComponent<BoxCollider>();
            if (col != null) col.enabled = false;
        }

        private void HandleCardMove(BaseCardSO cardData, ZoneType fromZone, ZoneType toZone, int ownerId,int subIndex)
        {
            CardView cardView = SpawnCard(cardData);

            // 1. 初始位置修正
            if (fromZone == ZoneType.Unknown || fromZone == ZoneType.Deck)
            {
                Transform startAnchor = GetZoneAnchor(ZoneType.Deck, ownerId);
                if (startAnchor) cardView.transform.position = startAnchor.position;
            }
            else if (fromZone == ZoneType.Hand)
            {
                Transform handAnchor = GetZoneAnchor(ZoneType.Hand, ownerId);
                if (handAnchor) cardView.transform.position = handAnchor.position;
            }
            
            // 2. 【关键】计算目标落点 (传入 cardData.cardId)
            Transform targetAnchor = CalculateTargetPosition(toZone, ownerId, subIndex);
            // 3. 执行飞行
            if (targetAnchor != null)
            {
                // 微小的随机偏移只给非召唤区用，召唤区要对齐
                Vector3 offset = Vector3.zero;
                if (toZone != ZoneType.SummonZone) 
                {
                    offset = new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(0, 0.05f), Random.Range(-0.02f, 0.02f));
                }
                
                cardView.MoveTo(targetAnchor.position, targetAnchor.rotation);
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
    }
}