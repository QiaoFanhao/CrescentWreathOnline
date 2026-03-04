using UnityEngine;
using System.Collections;
using CrescentWreath.Modules;
using CrescentWreath.View;

namespace CrescentWreath.Core
{
    /// <summary>
    /// High-level startup director. Responsible for opening sequence and handing control to TurnModule.
    /// Does not own gameplay state itself; it orchestrates other modules.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("--- Core Module References ---")]
        [Tooltip("Owns shared table data (summon zone, anomaly deck, sakura mochi deck, etc.)")]
        public TableZoneModule tableModule;

        [Tooltip("Owns visual presentation (deck meshes, flying cards, etc.)")]
        public GameVisualManager visualManager;

        [Tooltip("Owns turn/phase progression only")]
        public TurnModule turnModule;

        private void Start()
        {
            StartCoroutine(GameStartFlow());
        }

        /// <summary>
        /// Opening sequence / cinematic bootstrap.
        /// </summary>
        private IEnumerator GameStartFlow()
        {
            Debug.Log("<color=yellow>[Director] Game startup flow begins...</color>");

            if (tableModule == null)
            {
                Debug.LogError("[Director] TableZoneModule reference is missing.");
                yield break;
            }

            // Act 1: build shared table data.
            tableModule.InitializeTable();
            Debug.Log($"[Director] Table data initialized. Relic pool remaining: {tableModule.GetRelicDeckCount()}");

            // Act 2: build static table visuals.
            if (visualManager != null)
            {
                visualManager.InitializeDecks();
                Debug.Log("[Director] Visual stage initialized.");
            }
            else
            {
                Debug.LogWarning("[Director] GameVisualManager reference is missing; skipping visual setup.");
            }

            yield return new WaitForSeconds(0.5f);

            // Act 3: fill summon zone with a paced animation.
            Debug.Log("[Director] Filling summon zone...");
            for (int i = 0; i < 6; i++)
            {
                tableModule.DrawCardToSummonSlot(i);
                yield return new WaitForSeconds(0.15f);
            }

            yield return new WaitForSeconds(0.8f);

            // Act 4: reveal the first anomaly.
            Debug.Log("[Director] Revealing first anomaly...");
            tableModule.RevealTopAnomaly();

            yield return new WaitForSeconds(1.0f);

            // Act 5: initialize player decks / opening hands.
            Debug.Log("[Director] Initializing player decks and opening hands...");
            InitializePlayersForMatch();
            yield return new WaitForSeconds(0.3f);

            // Final: hand control to turn system.
            Debug.Log("<color=cyan>[Director] Setup complete. Starting gameplay.</color>");
            if (turnModule != null)
            {
                turnModule.StartGame();
            }
            else
            {
                Debug.LogError("[Director] TurnModule reference is missing.");
            }
        }

        /// <summary>
        /// Single entry point for player-zone match setup.
        /// TurnModule should not initialize table/player data.
        /// </summary>
        private void InitializePlayersForMatch()
        {
            if (turnModule == null || turnModule.playerZoneModules == null) return;

            foreach (var playerZone in turnModule.playerZoneModules)
            {
                if (playerZone != null)
                {
                    playerZone.InitializePlayerDeck();
                }
            }
        }
    }
}
