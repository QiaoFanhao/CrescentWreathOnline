using System.Collections.Generic;
using UnityEngine;
using CrescentWreath.Core;
using CrescentWreath.Data;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// Shared table-zone state (server-side gameplay data).
    /// Owns summon zone, anomaly deck/current anomaly, sakura mochi deck, and exile containers.
    /// </summary>
    public class TableZoneModule : MonoBehaviour
    {
        [Header("Shared Zone Containers")]
        [SerializeField] private List<int> _summonZone = new List<int>();          // 6 slots
        [SerializeField] private List<int> _anomalyDeck = new List<int>();
        [SerializeField] private List<int> _sakuraMochiDeck = new List<int>();
        [SerializeField] private List<int> _exileZone = new List<int>();           // generic gap/exile zone
        [SerializeField] private List<int> _resolvedAnomalyExile = new List<int>(); // resolved anomalies (subset semantic)

        [Header("Config References")]
        public GameSupplyConfigSO supplyConfig;
        public CardDatabaseSO cardDatabase;

        private readonly List<int> _relicPool = new List<int>(); // hidden public relic supply deck

        // Explicit current-anomaly state (not just visual reveal)
        [SerializeField] private int _currentAnomalyCardId = 0;
        [SerializeField] private bool _hasCurrentAnomaly = false;

        // Rule semantics flags
        [SerializeField] private bool _isFirstAnomalyRevealPending = true; // opening first anomaly does not trigger advent
        [SerializeField] private int _openingSummonFillRemaining = 6;      // opening 6 summon slots do not trigger advent

        public int GetSakuraCount() => _sakuraMochiDeck.Count;
        public int GetRelicDeckCount() => _relicPool.Count;
        public int GetAnomalyDeckCount() => _anomalyDeck.Count;
        public int GetExileCount() => _exileZone.Count;
        public int GetResolvedAnomalyExileCount() => _resolvedAnomalyExile.Count;

        public bool HasCurrentAnomaly() => _hasCurrentAnomaly;
        public int GetCurrentAnomalyCardId() => _hasCurrentAnomaly ? _currentAnomalyCardId : 0;

        public BaseCardSO GetCurrentAnomalyCard()
        {
            if (!_hasCurrentAnomaly || _currentAnomalyCardId == 0 || cardDatabase == null) return null;
            return cardDatabase.GetCardById(_currentAnomalyCardId);
        }

        /// <summary>
        /// Initializes shared table data only. Opening animations/dealing are driven by GameManager.
        /// </summary>
        public void InitializeTable()
        {
            _summonZone.Clear();
            _anomalyDeck.Clear();
            _sakuraMochiDeck.Clear();
            _exileZone.Clear();
            _resolvedAnomalyExile.Clear();
            _relicPool.Clear();

            _currentAnomalyCardId = 0;
            _hasCurrentAnomaly = false;

            // Reset opening rule markers.
            _isFirstAnomalyRevealPending = true;
            _openingSummonFillRemaining = 6;

            // Sakura mochi deck (fixed count from current prototype setup).
            for (int i = 0; i < 15; i++) _sakuraMochiDeck.Add(22003);

            // Build anomaly deck from config.
            if (supplyConfig != null)
            {
                foreach (var anomaly in supplyConfig.anomalyCards)
                {
                    if (anomaly != null) _anomalyDeck.Add(anomaly.cardId);
                }
                ShuffleList(_anomalyDeck);
            }

            // Build relic pool from config entries.
            if (supplyConfig != null)
            {
                foreach (var entry in supplyConfig.relicDeckEntries)
                {
                    if (entry.cardData == null || entry.quantity <= 0) continue;
                    for (int i = 0; i < entry.quantity; i++)
                    {
                        _relicPool.Add(entry.cardData.cardId);
                    }
                }
                ShuffleList(_relicPool);
            }

            Debug.Log("<color=green>[Table]</color> Table data initialized. GameManager controls opening fill/reveal.");
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

        public int GetSummonIndex(int cardId)
        {
            return _summonZone.IndexOf(cardId);
        }

        /// <summary>
        /// Draws 1 relic from hidden pool into the specified summon slot.
        /// Opening 6 fills suppress advent; later refills broadcast advent immediately.
        /// </summary>
        public void DrawCardToSummonSlot(int slotIndex)
        {
            if (_relicPool.Count == 0) return;
            if (slotIndex < 0)
            {
                Debug.LogWarning($"[Table] DrawCardToSummonSlot ignored: invalid slotIndex={slotIndex}");
                return;
            }

            int cardId = _relicPool[0];
            _relicPool.RemoveAt(0);

            while (_summonZone.Count <= slotIndex)
            {
                _summonZone.Add(0);
            }

            _summonZone[slotIndex] = cardId;

            var cardData = cardDatabase != null ? cardDatabase.GetCardById(cardId) : null;
            GameEvent.OnCardMoved?.Invoke(cardData, ZoneType.Deck, ZoneType.SummonZone, -1, slotIndex);

            bool isOpeningFill = _openingSummonFillRemaining > 0;
            if (_openingSummonFillRemaining > 0) _openingSummonFillRemaining--;

            // Rule semantic only: summon-zone refill should trigger advent immediately.
            if (!isOpeningFill && cardData != null)
            {
                GameEvent.OnAdventTriggered?.Invoke(cardData);
            }

            string cardName = cardData != null ? cardData.cardName : $"Card#{cardId}";
            Debug.Log($"<color=orange>[Table]</color> Summon slot {slotIndex} <- {cardName}" +
                      (isOpeningFill ? " (opening fill, no advent)" : " (refill advent notified)"));
        }

        public int PeekTopAnomaly()
        {
            if (_anomalyDeck.Count == 0) return 0;
            return _anomalyDeck[0];
        }

        public void ShuffleAnomalyDeck()
        {
            ShuffleList(_anomalyDeck);
            Debug.Log("<color=purple>[Table]</color> Anomaly deck shuffled.");
        }

        /// <summary>
        /// Reveals the next anomaly as the current active anomaly.
        /// Opening first reveal suppresses advent by rule.
        /// </summary>
        public void RevealTopAnomaly()
        {
            if (_anomalyDeck.Count == 0)
            {
                Debug.LogWarning("[Table] RevealTopAnomaly ignored: anomaly deck is empty.");
                return;
            }

            if (_hasCurrentAnomaly)
            {
                Debug.LogWarning("[Table] RevealTopAnomaly ignored: a current anomaly is already active.");
                return;
            }

            int cardId = _anomalyDeck[0];
            _anomalyDeck.RemoveAt(0);

            _currentAnomalyCardId = cardId;
            _hasCurrentAnomaly = true;

            var cardData = cardDatabase != null ? cardDatabase.GetCardById(cardId) : null;

            // Visuals currently reuse Battlefield as anomaly display destination.
            GameEvent.OnCardMoved?.Invoke(cardData, ZoneType.AnomalyDeck, ZoneType.Battlefield, -1, 0);
            GameEvent.OnAnomalyRevealed?.Invoke(cardData);

            bool isOpeningReveal = _isFirstAnomalyRevealPending;
            if (_isFirstAnomalyRevealPending) _isFirstAnomalyRevealPending = false;

            if (!isOpeningReveal && cardData != null)
            {
                GameEvent.OnAdventTriggered?.Invoke(cardData);
            }

            string cardName = cardData != null ? cardData.cardName : $"Card#{cardId}";
            Debug.Log($"<color=purple>[Table]</color> Anomaly revealed: {cardName}" +
                      (isOpeningReveal ? " (opening reveal, no advent)" : " (advent notified)"));
        }

        /// <summary>
        /// Placeholder API for future anomaly resolution flow.
        /// Moves current anomaly to exile containers and clears current anomaly state.
        /// Does not apply rewards or auto-reveal next anomaly.
        /// </summary>
        public bool TryMoveCurrentAnomalyToResolvedExile(out BaseCardSO anomalyCard)
        {
            anomalyCard = null;
            if (!_hasCurrentAnomaly || _currentAnomalyCardId == 0) return false;

            int cardId = _currentAnomalyCardId;
            _resolvedAnomalyExile.Add(cardId);
            _exileZone.Add(cardId);

            anomalyCard = cardDatabase != null ? cardDatabase.GetCardById(cardId) : null;
            GameEvent.OnCardMoved?.Invoke(anomalyCard, ZoneType.Battlefield, ZoneType.Exile, -1, -1);

            _currentAnomalyCardId = 0;
            _hasCurrentAnomaly = false;

            return true;
        }

        /// <summary>
        /// Generic exile container placeholder for future "放逐" effects.
        /// </summary>
        public void AddToExile(int cardId)
        {
            if (cardId == 0) return;
            _exileZone.Add(cardId);
        }
    }
}
