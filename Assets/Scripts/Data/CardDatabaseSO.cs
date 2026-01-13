using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 必须要引用这个

namespace CrescentWreath.Data
{
    [CreateAssetMenu(fileName = "NewCardDatabase", menuName = "DBG/Configs/CardDatabase")]
    public class CardDatabaseSO : ScriptableObject
    {
        // 存储所有的卡牌数据
        public List<BaseCardSO> allCards = new List<BaseCardSO>();

        // 运行时使用的快速查找字典 (ID -> SO)
        private Dictionary<int, BaseCardSO> _lookupTable;

        /// <summary>
        /// 初始化字典 (游戏启动时自动调用)
        /// </summary>
        public void Init()
        {
            _lookupTable = new Dictionary<int, BaseCardSO>();
            foreach (var card in allCards)
            {
                if (card != null && !_lookupTable.ContainsKey(card.cardId))
                {
                    _lookupTable.Add(card.cardId, card);
                }
            }
            Debug.Log($"[CardDatabase] 已加载 {_lookupTable.Count} 张卡牌数据");
        }

        /// <summary>
        /// 核心查询接口
        /// </summary>
        public BaseCardSO GetCardById(int id)
        {
            if (_lookupTable == null) Init();

            if (_lookupTable.TryGetValue(id, out var card))
            {
                return card;
            }
            
            Debug.LogWarning($"[CardDatabase] 找不到 ID 为 {id} 的卡牌！");
            return null;
        }
#if UNITY_EDITOR
        [ContextMenu("自动加载所有卡牌SO")]
        public void LoadAllCards()
        {
            // 【安全锁】先搜索，确定有货再清空旧数据
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BaseCardSO");
            
            if (guids.Length == 0)
            {
                Debug.LogError(" [CardDatabase] 扫描失败！没有找到任何 BaseCardSO，为了保护数据，已取消本次操作。");
                return; // 直接退出，保护现有数据不被清空
            }

            // 确认找到了新数据，这才敢清空列表
            allCards.Clear();
            
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                BaseCardSO card = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseCardSO>(path);
                
                if (card != null && !allCards.Contains(card))
                {
                    allCards.Add(card);
                }
            }
            
            allCards = allCards.OrderBy(c => c.cardId).ToList();
            
            Debug.Log($"<color=green>[Editor]</color> 成功重建索引！共 {allCards.Count} 张卡牌。");
            
            // 【关键】告诉 Unity 这个文件被改“脏”了，需要存盘
            UnityEditor.EditorUtility.SetDirty(this);
            // 【双保险】强制写入磁盘
            UnityEditor.AssetDatabase.SaveAssets(); 
        }
#endif
   
    }
}