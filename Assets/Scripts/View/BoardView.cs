using UnityEngine;

// 关键点：必须包裹在这个命名空间里，才能被 GameVisualManager 找到
namespace CrescentWreath.View 
{
    public class BoardView : MonoBehaviour
    {
        public static BoardView Instance;

        [Header("公共区域")]
        public Transform[] summonSlots; 
        public Transform anomalyDeckPos;     
        public Transform sakuraMochiDeckPos; 

        [Header("玩家区域")]
        public PlayerAreaAnchors[] playerAreas;

        private void Awake()
        {
            Instance = this;
        }

        public Transform GetSummonSlot(int index)
        {
            if (summonSlots != null && index >= 0 && index < summonSlots.Length)
                return summonSlots[index];
            return transform;
        }
    }

    [System.Serializable]
    public class PlayerAreaAnchors
    {
        public string debugName;
        public Transform handPivot;      
        public Transform playZoneCenter; 
        public Transform deckPos;        
        public Transform discardPos;     
        public Transform leaderPos;      
    }
}