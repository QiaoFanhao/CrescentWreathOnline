using System;
using System.Collections.Generic;

namespace CrescentWreath.Core
{
    /// <summary>
    /// Owns RuntimeCard instances for one match and provides deterministic helpers.
    /// This class is state-only and can be used by server logic or local prototype logic.
    /// </summary>
    [Serializable]
    public class RuntimeCardRegistry
    {
        private readonly Dictionary<int, RuntimeCard> _cards = new Dictionary<int, RuntimeCard>();

        [NonSerialized] private System.Random _rng = new System.Random();
        private int _nextRuntimeCardId = 1;

        public int Count => _cards.Count;

        public void BeginMatch(int seed, int startRuntimeCardId = 1)
        {
            _cards.Clear();
            _rng = new System.Random(seed);
            _nextRuntimeCardId = Math.Max(1, startRuntimeCardId);
        }

        public int CreateCard(
            int baseCardId,
            int ownerPlayerId,
            ZoneType zone,
            BattlefieldSubZone subZone = BattlefieldSubZone.None,
            bool isFaceUp = false,
            int createdTurn = 0)
        {
            int runtimeCardId = _nextRuntimeCardId++;
            var card = new RuntimeCard(runtimeCardId, baseCardId, ownerPlayerId, zone, subZone, isFaceUp, createdTurn);
            _cards[runtimeCardId] = card;
            return runtimeCardId;
        }

        public List<int> CreateCards(
            int baseCardId,
            int quantity,
            int ownerPlayerId,
            ZoneType zone,
            BattlefieldSubZone subZone = BattlefieldSubZone.None,
            bool isFaceUp = false,
            int createdTurn = 0)
        {
            var ids = new List<int>(Math.Max(0, quantity));
            if (quantity <= 0) return ids;

            for (int i = 0; i < quantity; i++)
            {
                ids.Add(CreateCard(baseCardId, ownerPlayerId, zone, subZone, isFaceUp, createdTurn));
            }

            return ids;
        }

        public bool TryGetCard(int runtimeCardId, out RuntimeCard card)
        {
            return _cards.TryGetValue(runtimeCardId, out card);
        }

        public RuntimeCard GetCardOrNull(int runtimeCardId)
        {
            _cards.TryGetValue(runtimeCardId, out var card);
            return card;
        }

        public bool MoveCard(
            int runtimeCardId,
            ZoneType toZone,
            int? newOwnerPlayerId = null,
            BattlefieldSubZone battlefieldSubZone = BattlefieldSubZone.None,
            bool? setFaceUp = null)
        {
            if (!_cards.TryGetValue(runtimeCardId, out var card)) return false;

            card.zone = toZone;
            card.battlefieldSubZone = toZone == ZoneType.Battlefield ? battlefieldSubZone : BattlefieldSubZone.None;

            if (newOwnerPlayerId.HasValue)
            {
                card.ownerPlayerId = newOwnerPlayerId.Value;
            }

            if (setFaceUp.HasValue)
            {
                card.isFaceUp = setFaceUp.Value;
            }

            return true;
        }

        public bool RemoveCard(int runtimeCardId)
        {
            return _cards.Remove(runtimeCardId);
        }

        /// <summary>
        /// Deterministic Fisher-Yates shuffle using the match RNG.
        /// Container stores runtimeCardId values.
        /// </summary>
        public void ShuffleInPlace(IList<int> container)
        {
            if (container == null || container.Count <= 1) return;

            for (int i = 0; i < container.Count - 1; i++)
            {
                int swapIndex = _rng.Next(i, container.Count);
                if (swapIndex == i) continue;

                int tmp = container[i];
                container[i] = container[swapIndex];
                container[swapIndex] = tmp;
            }
        }

        /// <summary>
        /// Pops top card from a container (index 0) and returns runtimeCardId.
        /// Returns false if container is empty.
        /// </summary>
        public bool TryDrawTop(List<int> container, out int runtimeCardId)
        {
            runtimeCardId = 0;
            if (container == null || container.Count == 0) return false;

            runtimeCardId = container[0];
            container.RemoveAt(0);
            return true;
        }

        public void PutToBottom(List<int> container, int runtimeCardId)
        {
            if (container == null || runtimeCardId == 0) return;
            container.Add(runtimeCardId);
        }
    }
}
