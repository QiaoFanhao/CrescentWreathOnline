using System.Collections.Generic;
using UnityEngine;
using CrescentWreath.Core;
using CrescentWreath.Modules;

namespace CrescentWreath.View
{
    /// <summary>
    /// Rebuilds local player (P0) battlefield UI panels from PlayerZoneModule snapshots.
    /// Uses a simple full-refresh approach for prototype stability.
    /// </summary>
    public class P0BattlefieldUIController : MonoBehaviour
    {
        [Header("Data Source")]
        public PlayerZoneModule playerZoneModule;
        public int localPlayerId = 0;

        [Header("UI Panel Roots")]
        public Transform playedCardsPanel;
        public Transform defenseCardsPanel;
        public Transform persistentCardsPanel;

        [Header("Prefab")]
        public GameObject battlefieldCardUIPrefab;

        [Header("Visual Options")]
        public bool applyCustomScale = true;
        public Vector3 cardLocalScale = new Vector3(0.006f, 0.006f, 1f);

        private void OnEnable()
        {
            GameEvent.OnCardMoved += HandleCardMoved;
            RefreshAll();
        }

        private void OnDisable()
        {
            GameEvent.OnCardMoved -= HandleCardMoved;
        }

        public void RefreshAll()
        {
            if (playerZoneModule == null)
            {
                Debug.LogWarning("[P0BattlefieldUI] Missing PlayerZoneModule reference.");
                return;
            }

            RebuildPanel(playedCardsPanel, playerZoneModule.GetBattlefieldPlayedSnapshot());
            RebuildPanel(defenseCardsPanel, playerZoneModule.GetBattlefieldDefenseSnapshot());
            RebuildPanel(persistentCardsPanel, playerZoneModule.GetBattlefieldPersistentSnapshot());
        }

        private void HandleCardMoved(BaseCardSO cardData, ZoneType fromZone, ZoneType toZone, int ownerId, int subIndex)
        {
            if (ownerId != localPlayerId) return;

            // Refresh only when battlefield-related moves happen for P0.
            if (fromZone != ZoneType.Battlefield && toZone != ZoneType.Battlefield) return;

            RefreshAll();
        }

        private void RebuildPanel(Transform panelRoot, List<BaseCardSO> cards)
        {
            if (panelRoot == null) return;

            ClearChildren(panelRoot);

            if (battlefieldCardUIPrefab == null)
            {
                Debug.LogWarning("[P0BattlefieldUI] battlefieldCardUIPrefab is not assigned.");
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                BaseCardSO card = cards[i];
                if (card == null) continue;

                GameObject go = Instantiate(battlefieldCardUIPrefab, panelRoot);
                go.name = $"BFUI_{card.cardId}_{i}";
                go.transform.SetAsLastSibling(); // Newest card appears on the right.

                if (applyCustomScale)
                {
                    go.transform.localScale = cardLocalScale;
                }

                var cardView = go.GetComponent<BattlefieldUICardView>();
                if (cardView == null)
                {
                    Debug.LogError("[P0BattlefieldUI] Prefab is missing BattlefieldUICardView component.");
                    Destroy(go);
                    continue;
                }

                cardView.Setup(card);
            }
        }

        private void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
