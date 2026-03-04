using System.Collections.Generic;
using UnityEngine;
using CrescentWreath.Core;
using CrescentWreath.Data;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// Per-player zone state (deck / hand / discard / battlefield).
    /// Battlefield is split into:
    /// - played cards (end-phase cleanup -> discard)
    /// - defense cards (returned to hand at this player's next start phase)
    /// </summary>
    public class PlayerZoneModule : MonoBehaviour
    {
        public int playerId;

        [Header("Private Containers")]
        [SerializeField] private List<int> _deck = new List<int>();
        [SerializeField] private List<int> _hand = new List<int>();
        [SerializeField] private List<int> _discard = new List<int>();
        [SerializeField] private List<int> _battlefieldPlayed = new List<int>();
        [SerializeField] private List<int> _battlefieldDefense = new List<int>();
        [SerializeField] private List<int> _battlefieldPersistent = new List<int>();

        [Header("Defense Placeholder Rules")]
        [SerializeField] private int _maxDefenseCardsPerDamage = 1;

        public CardDatabaseSO cardDatabase;

        public int HandCount => _hand.Count;
        public int BattlefieldPlayedCount => _battlefieldPlayed.Count;
        public int BattlefieldDefenseCount => _battlefieldDefense.Count;
        public int BattlefieldPersistentCount => _battlefieldPersistent.Count;
        public int MaxDefenseCardsPerDamage => _maxDefenseCardsPerDamage;

        private void OnEnable()
        {
            // Discard requests are still event-driven for end-phase overflow handling.
            GameEvent.Request_DiscardHandCard += OnRequestDiscard;
        }

        private void OnDisable()
        {
            GameEvent.Request_DiscardHandCard -= OnRequestDiscard;
        }

        public void InitializePlayerDeck()
        {
            _deck.Clear();
            _hand.Clear();
            _discard.Clear();
            _battlefieldPlayed.Clear();
            _battlefieldDefense.Clear();
            _battlefieldPersistent.Clear();

            for (int i = 0; i < 3; i++) _deck.Add(21002);
            for (int i = 0; i < 7; i++) _deck.Add(21001);

            ShuffleDeck();
            DrawCards(6);
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

        /// <summary>
        /// Move a hand card to battlefield and return its card data for effect execution.
        /// TurnModule is expected to validate phase/ownership before calling.
        /// </summary>
        public bool TryPlayHandCard(int index, out BaseCardSO playedCard)
        {
            playedCard = null;
            if (index < 0 || index >= _hand.Count) return false;

            int id = _hand[index];
            _hand.RemoveAt(index);
            _battlefieldPlayed.Add(id);
            BroadcastMove(id, ZoneType.Hand, ZoneType.Battlefield);

            if (cardDatabase != null)
            {
                playedCard = cardDatabase.GetCardById(id);
            }

            return true;
        }

        private void RecycleDiscard()
        {
            _deck.AddRange(_discard);
            _discard.Clear();
            ShuffleDeck();
        }

        private void OnRequestDiscard(int index)
        {
            if (index < 0 || index >= _hand.Count) return;

            int id = _hand[index];
            _hand.RemoveAt(index);
            _discard.Add(id);
            BroadcastMove(id, ZoneType.Hand, ZoneType.Discard);
        }

        /// <summary>
        /// Placeholder entry for future damage-resolution integration.
        /// Moves a hand card into the defense sub-zone on battlefield.
        /// Does not validate damage type / defense value yet.
        /// </summary>
        public bool TryCommitDefenseCardFromHand(int index, out BaseCardSO defenseCard)
        {
            defenseCard = null;

            if (index < 0 || index >= _hand.Count) return false;

            // Placeholder rule: per-damage declaration count should be enforced by damage-resolution module.
            // We keep the cap here only as a configurable reference and do not enforce it globally yet.
            int id = _hand[index];
            _hand.RemoveAt(index);
            _battlefieldDefense.Add(id);
            BroadcastMove(id, ZoneType.Hand, ZoneType.Battlefield);

            if (cardDatabase != null)
            {
                defenseCard = cardDatabase.GetCardById(id);
            }

            return true;
        }

        /// <summary>
        /// Rule: at this player's next start phase, defense cards in battlefield return to hand.
        /// </summary>
        public int ReturnDefenseCardsToHand()
        {
            if (_battlefieldDefense.Count == 0) return 0;

            int count = _battlefieldDefense.Count;
            foreach (var id in _battlefieldDefense)
            {
                _hand.Add(id);
                BroadcastMove(id, ZoneType.Battlefield, ZoneType.Hand);
            }
            _battlefieldDefense.Clear();
            return count;
        }

        /// <summary>
        /// Rule: end phase cleanup discards only played cards in battlefield.
        /// </summary>
        public int ClearPlayedCardsToDiscard()
        {
            int count = _battlefieldPlayed.Count;
            foreach (var id in _battlefieldPlayed)
            {
                _discard.Add(id);
                BroadcastMove(id, ZoneType.Battlefield, ZoneType.Discard);
            }
            _battlefieldPlayed.Clear();
            return count;
        }

        /// <summary>
        /// Placeholder API for special cards that remain in battlefield as persistent effects.
        /// Moves the newest matching card from played/defense sub-zone into the persistent sub-zone.
        /// </summary>
        public bool TryMoveBattlefieldCardToPersistent(int cardId)
        {
            if (cardId == 0) return false;

            for (int i = _battlefieldPlayed.Count - 1; i >= 0; i--)
            {
                if (_battlefieldPlayed[i] != cardId) continue;
                _battlefieldPersistent.Add(_battlefieldPlayed[i]);
                _battlefieldPlayed.RemoveAt(i);
                return true;
            }

            for (int i = _battlefieldDefense.Count - 1; i >= 0; i--)
            {
                if (_battlefieldDefense[i] != cardId) continue;
                _battlefieldPersistent.Add(_battlefieldDefense[i]);
                _battlefieldDefense.RemoveAt(i);
                return true;
            }

            return false;
        }

        public List<BaseCardSO> GetBattlefieldPlayedSnapshot()
        {
            return BuildCardSnapshot(_battlefieldPlayed);
        }

        public List<BaseCardSO> GetBattlefieldDefenseSnapshot()
        {
            return BuildCardSnapshot(_battlefieldDefense);
        }

        public List<BaseCardSO> GetBattlefieldPersistentSnapshot()
        {
            return BuildCardSnapshot(_battlefieldPersistent);
        }

        /// <summary>
        /// Compatibility wrapper. Intentionally clears only played cards (not defense cards).
        /// </summary>
        public void ClearBattlefield()
        {
            ClearPlayedCardsToDiscard();
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
            bool isLocalPlayer = (playerId == 0);
            bool isPublicZone = (to == ZoneType.Battlefield || to == ZoneType.SummonZone || to == ZoneType.Discard);

            BaseCardSO cardData = null;
            if (isLocalPlayer || isPublicZone)
            {
                cardData = cardDatabase != null ? cardDatabase.GetCardById(cardId) : null;
            }

            GameEvent.OnCardMoved?.Invoke(cardData, from, to, playerId, -1);
        }

        public int GetDeckCount(int ignoredPlayerId)
        {
            // TODO: return actual deck count for the requested player when multi-player data is centralized.
            return 40;
        }

        private List<BaseCardSO> BuildCardSnapshot(List<int> ids)
        {
            var result = new List<BaseCardSO>(ids.Count);
            if (ids.Count == 0) return result;

            for (int i = 0; i < ids.Count; i++)
            {
                BaseCardSO data = cardDatabase != null ? cardDatabase.GetCardById(ids[i]) : null;
                if (data != null)
                {
                    result.Add(data);
                }
            }

            return result;
        }
    }
}
