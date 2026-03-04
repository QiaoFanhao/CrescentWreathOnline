using System;

namespace CrescentWreath.Core
{
    /// <summary>
    /// Runtime instance of a card for the current match.
    /// One physical/virtual card in a match = one RuntimeCard with a unique runtimeCardId.
    /// </summary>
    [Serializable]
    public class RuntimeCard
    {
        public int runtimeCardId;
        public int baseCardId;
        public int ownerPlayerId;
        public ZoneType zone;
        public BattlefieldSubZone battlefieldSubZone;
        public bool isFaceUp;
        public int createdTurn;

        public RuntimeCard(
            int runtimeId,
            int baseId,
            int ownerId,
            ZoneType startZone,
            BattlefieldSubZone startSubZone = BattlefieldSubZone.None,
            bool faceUp = false,
            int turn = 0)
        {
            runtimeCardId = runtimeId;
            baseCardId = baseId;
            ownerPlayerId = ownerId;
            zone = startZone;
            battlefieldSubZone = startSubZone;
            isFaceUp = faceUp;
            createdTurn = turn;
        }
    }

    /// <summary>
    /// Sub-zones inside ZoneType.Battlefield.
    /// </summary>
    public enum BattlefieldSubZone
    {
        None = 0,
        Played = 1,
        Defense = 2,
        Persistent = 3
    }
}
