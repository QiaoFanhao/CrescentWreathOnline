using CrescentWreath.Core;
using CrescentWreath.Modules;
using UnityEngine;

namespace CrescentWreath.Lua
{
    /// <summary>
    /// Object passed into Lua card scripts.
    /// Exposes a stable gameplay API surface for card effects.
    /// </summary>
    public class LuaEffectContext
    {
        public int ownerId;
        public int targetId;
        public BaseCardSO card;

        private readonly TurnModule _turn;
        private readonly PlayerZoneModule[] _playerZones;

        public LuaEffectContext(int owner, int target, BaseCardSO data, TurnModule turn, PlayerZoneModule[] zones)
        {
            ownerId = owner;
            targetId = target;
            card = data;
            _turn = turn;
            _playerZones = zones;
        }

        // ---------------------------------------------------------------------
        // Selection API (preferred)
        // ---------------------------------------------------------------------

        // scope: 0=self, 1=enemy, 2=all
        public WaitForSelection SelectPlayer(int scope)
        {
            return new WaitForSelection("Player", scope);
        }

        public WaitForSelection SelectPlayer(int scope, string filterTag)
        {
            return new WaitForSelection("Player", scope, filterTag);
        }

        public WaitForSelection SelectCard(string zone, int scope)
        {
            return new WaitForSelection(zone, scope);
        }

        public WaitForSelection SelectCard(string zone, int scope, string filterTag)
        {
            return new WaitForSelection(zone, scope, filterTag);
        }

        public WaitForSelection SelectCard(string zone, int scope, string filterTag, int count)
        {
            return new WaitForSelection(zone, scope, filterTag, count);
        }

        // ---------------------------------------------------------------------
        // Compatibility wrappers (keep old scripts working)
        // ---------------------------------------------------------------------

        public WaitForSelection AsyncSelect(string zone, int scope)
        {
            return SelectCard(zone, scope);
        }

        public WaitForSelection AsyncSelect(string zone, int scope, string filterTag)
        {
            return SelectCard(zone, scope, filterTag);
        }

        // ---------------------------------------------------------------------
        // Resource / card actions
        // ---------------------------------------------------------------------

        public void AddSkillPoint(int amount)
        {
            if (_turn != null) _turn.AddResources(0, 0, amount);
        }

        public void AddCoin(int amount)
        {
            if (_turn != null) _turn.AddResources(amount, 0, 0);
        }

        public void AddMagic(int amount)
        {
            if (_turn != null) _turn.AddResources(0, amount, 0);
        }

        public void DrawCards(int playerId, int amount)
        {
            if (_playerZones == null) return;
            if (playerId < 0 || playerId >= _playerZones.Length) return;
            if (_playerZones[playerId] == null) return;

            _playerZones[playerId].DrawCards(amount);
        }

        public void MoveCard(int cardId, ZoneType from, ZoneType to, int ownerId)
        {
            Debug.Log($"[Lua] MoveCard request card={cardId} {from}->{to} owner={ownerId}");
            // TODO: Route into actual runtime card movement pipeline.
        }

        // String overload for Lua convenience / compatibility.
        public void MoveCard(int cardId, string fromZone, string toZone, int ownerId)
        {
            if (!TryParseZone(fromZone, out var from) || !TryParseZone(toZone, out var to))
            {
                Debug.LogWarning($"[Lua] MoveCard invalid zone(s): {fromZone} -> {toZone}");
                return;
            }

            MoveCard(cardId, from, to, ownerId);
        }

        // ---------------------------------------------------------------------
        // Damage API
        // ---------------------------------------------------------------------

        /// <summary>
        /// Immediate damage/heal application placeholder (non-yield path).
        /// Positive values = damage, negative values = heal.
        /// </summary>
        public void DealDamage(int targetId, int amount)
        {
            DealDamage(targetId, amount, "Direct");
        }

        public void DealDamage(int targetId, int amount, string type)
        {
            if (amount >= 0)
                Debug.Log($"[Lua] DealDamage target={targetId} amount={amount} type={type}");
            else
                Debug.Log($"[Lua] Heal target={targetId} amount={-amount} type={type}");

            // TODO: Connect to real HP / damage resolution pipeline.
        }

        /// <summary>
        /// Request asynchronous damage processing (defense/reaction window).
        /// For non-positive values, this applies immediately and returns null for compatibility.
        /// Lua usage:
        ///   local id = coroutine.yield(context:SelectPlayer(1))
        ///   local final = coroutine.yield(context:ApplyDamage(id, 3, "Magic"))
        /// </summary>
        public object ApplyDamage(int targetId, int amount)
        {
            return ApplyDamage(targetId, amount, "Direct");
        }

        public object ApplyDamage(int targetId, int amount, string type)
        {
            if (amount <= 0)
            {
                DealDamage(targetId, amount, type);
                return null;
            }

            return new WaitForDamageProcess(targetId, amount, type);
        }

        private bool TryParseZone(string zoneName, out ZoneType zone)
        {
            if (string.IsNullOrEmpty(zoneName))
            {
                zone = ZoneType.Unknown;
                return false;
            }

            return System.Enum.TryParse(zoneName, true, out zone);
        }
    }
}
