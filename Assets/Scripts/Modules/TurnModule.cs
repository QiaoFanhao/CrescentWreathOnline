using System.Collections;
using UnityEngine;
using CrescentWreath.Core;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// Turn/phase controller (server-side gameplay logic).
    /// Owns phase progression and shared turn resources.
    /// Does not initialize table/player zones.
    /// </summary>
    public class TurnModule : MonoBehaviour
    {
        public enum ActionWindowState
        {
            Closed,
            Idle,                 // Can choose next action in ActionPhase
            ResolvingPlayCard,    // A hand card is resolving (Lua/effects)
            ResolvingSkill,       // Placeholder for character skill execution
            ResolvingAnomaly,     // Placeholder for anomaly resolution
            WaitingForResponse,   // Placeholder for future reaction window
            WaitingForDiscard     // EndPhase overflow discard selection
        }

        [Header("State Info")]
        public int activePlayerId = 0;
        public PhaseType currentPhase;
        public int turnCount = 0;
        public bool isGameRunning = false;
        public bool isGameOver = false;
        public bool isPhaseTransitioning = false;
        public bool isResolvingAction = false;

        [Header("Per-Turn Action State")]
        public ActionWindowState actionWindowState = ActionWindowState.Closed;
        public bool hasAttemptedAnomalyResolveThisTurn = false;
        public bool hasResolvedAnomalyThisTurn = false;
        public int anomalyResolveAttemptCountThisTurn = 0;

        [Header("Summon Phase Loop State")]
        public bool isSummonPhaseOpen = false;
        public int summonActionsThisPhase = 0;

        [Header("Global Resource Pool (shared by active player)")]
        public int Coin = 0;
        public int Magic = 0;
        public int SkillPoint = 0;

        [Header("Config")]
        public int totalPlayers = 4;

        [Header("Module References")]
        public TableZoneModule tableZoneModule; // kept for inspector compatibility; no longer used in StartGame
        public PlayerZoneModule[] playerZoneModules;
        public StatusModule[] playerStatusModules;

        private void OnEnable()
        {
            GameEvent.Request_PlayHandCard += OnRequestPlayHandCard;
            GameEvent.Request_UseCharacterSkill += OnRequestUseCharacterSkill;
            GameEvent.Request_ResolveAnomaly += OnRequestResolveAnomaly;
            GameEvent.Request_SummonCard += OnRequestSummonCard;
            GameEvent.Request_EndPhase += OnRequestEndPhase;
        }

        private void OnDisable()
        {
            GameEvent.Request_PlayHandCard -= OnRequestPlayHandCard;
            GameEvent.Request_UseCharacterSkill -= OnRequestUseCharacterSkill;
            GameEvent.Request_ResolveAnomaly -= OnRequestResolveAnomaly;
            GameEvent.Request_SummonCard -= OnRequestSummonCard;
            GameEvent.Request_EndPhase -= OnRequestEndPhase;
        }

        public void StartGame()
        {
            Debug.Log("<color=cyan>[Turn]</color> Turn system starts.");

            isGameRunning = true;
            isGameOver = false;
            turnCount = 1;
            activePlayerId = 0;
            StartTurn(activePlayerId);
        }

        /// <summary>
        /// Public API for Lua effects to add shared turn resources.
        /// </summary>
        public void AddResources(int coin, int magic, int sp = 0)
        {
            Coin += coin;
            Magic += magic;
            SkillPoint += sp;

            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, Coin);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, Magic);
            if (sp != 0)
                GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, SkillPoint);

            Debug.Log($"<color=yellow>[Resource]</color> Updated: Coin+{coin}, Magic+{magic}, SP+{sp}");
        }

        private void StartTurn(int playerId)
        {
            if (isGameOver)
            {
                Debug.LogWarning("[Turn] StartTurn ignored because the game is already over.");
                return;
            }

            Debug.Log($"<color=cyan>[Turn]</color> === Player {playerId} Turn Start ===");

            GameEvent.OnTurnStarted?.Invoke(playerId);
            ResetPerTurnActionState();

            // Rule: reset shared resources at turn start.
            Coin = 0;
            Magic = 0;
            SkillPoint = 1;

            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, 1);

            EnterPhase(PhaseType.StartPhase);
            StartCoroutine(ProcessStartPhase());
        }

        private IEnumerator ProcessStartPhase()
        {
            yield return new WaitForSeconds(0.5f);

            if (playerZoneModules != null && activePlayerId >= 0 && activePlayerId < playerZoneModules.Length)
            {
                var currentZone = playerZoneModules[activePlayerId];
                if (currentZone != null)
                {
                    int returnedDefenseCount = currentZone.ReturnDefenseCardsToHand();
                    if (returnedDefenseCount > 0)
                    {
                        Debug.Log($"[Turn] Player {activePlayerId} returns {returnedDefenseCount} defense card(s) to hand.");
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }

            if (playerStatusModules != null && activePlayerId >= 0 && activePlayerId < playerStatusModules.Length)
            {
                var statusModule = playerStatusModules[activePlayerId];
                if (statusModule != null && statusModule.HasStatus(StatusType.Imprisonment))
                {
                    Debug.LogWarning($"[Turn] Player {activePlayerId} is imprisoned. Skipping this turn.");
                    EndTurn();
                    yield break;
                }
            }

            EnterPhase(PhaseType.ActionPhase);
        }

        private void OnRequestEndPhase()
        {
            if (!isGameRunning || isGameOver) return;
            if (isPhaseTransitioning)
            {
                Debug.LogWarning("[Turn] Reject end-phase request: phase transition is in progress.");
                return;
            }

            if (currentPhase == PhaseType.ActionPhase)
            {
                if (isResolvingAction || actionWindowState != ActionWindowState.Idle)
                {
                    Debug.LogWarning($"[Turn] Reject end-phase request: action window is busy ({actionWindowState}).");
                    return;
                }
            }

            if (currentPhase == PhaseType.SummonPhase)
            {
                // Summon phase is a loop: player may summon multiple times until they actively end it.
                // No-op here except validation; actual summon logic is handled by OnRequestSummonCard.
                if (isResolvingAction)
                {
                    Debug.LogWarning("[Turn] Reject end-phase request: summon action is resolving.");
                    return;
                }
            }

            AdvancePhase();
        }

        private void OnRequestPlayHandCard(int handIndex)
        {
            if (!isGameRunning || isGameOver) return;

            // Local UI currently only represents player 0's hand.
            if (activePlayerId != 0)
            {
                Debug.LogWarning($"[Turn] Reject play request: active player is {activePlayerId}, not local player 0.");
                return;
            }

            if (currentPhase != PhaseType.ActionPhase)
            {
                Debug.LogWarning($"[Turn] Reject play request: current phase is {currentPhase}.");
                return;
            }

            if (isPhaseTransitioning || isResolvingAction)
            {
                Debug.LogWarning("[Turn] Reject play request: another action/transition is in progress.");
                return;
            }

            if (actionWindowState != ActionWindowState.Idle)
            {
                Debug.LogWarning($"[Turn] Reject play request: action window state is {actionWindowState}.");
                return;
            }

            if (playerZoneModules == null || activePlayerId < 0 || activePlayerId >= playerZoneModules.Length)
            {
                Debug.LogError("[Turn] Reject play request: PlayerZoneModules reference is invalid.");
                return;
            }

            var currentZone = playerZoneModules[activePlayerId];
            if (currentZone == null)
            {
                Debug.LogError($"[Turn] Reject play request: PlayerZoneModule for player {activePlayerId} is missing.");
                return;
            }

            if (!currentZone.TryPlayHandCard(handIndex, out var playedCard))
            {
                Debug.LogWarning($"[Turn] Reject play request: invalid hand index {handIndex}.");
                return;
            }

            isResolvingAction = true;
            actionWindowState = ActionWindowState.ResolvingPlayCard;

            if (playedCard == null)
            {
                Debug.LogWarning("[Turn] Card moved to battlefield but card data is null; Lua effect skipped.");
                isResolvingAction = false;
                actionWindowState = ActionWindowState.Idle;
                return;
            }

            if (LuaManager.Instance == null)
            {
                Debug.LogWarning("[Turn] LuaManager.Instance is null; card effect skipped.");
                isResolvingAction = false;
                actionWindowState = ActionWindowState.Idle;
                return;
            }

            LuaManager.Instance.ExecuteCardEffect(playedCard, activePlayerId);

            // Placeholder: current Lua execution is fire-and-forget from TurnModule's perspective.
            // We release the action lock immediately for now; later this should be driven by an effect-complete callback/event.
            isResolvingAction = false;
            actionWindowState = ActionWindowState.Idle;
        }

        private void OnRequestUseCharacterSkill(int skillSlot)
        {
            if (!isGameRunning || isGameOver) return;

            if (currentPhase != PhaseType.ActionPhase)
            {
                Debug.LogWarning($"[Turn] Reject skill request: current phase is {currentPhase}.");
                return;
            }

            if (isPhaseTransitioning || isResolvingAction || actionWindowState != ActionWindowState.Idle)
            {
                Debug.LogWarning($"[Turn] Reject skill request: action window is busy ({actionWindowState}).");
                return;
            }

            // Placeholder only: character selection/rules are intentionally deferred.
            actionWindowState = ActionWindowState.ResolvingSkill;
            Debug.Log($"[Turn] TODO: character skill execution not implemented yet. Requested slot={skillSlot}");
            actionWindowState = ActionWindowState.Idle;
        }

        private void OnRequestResolveAnomaly()
        {
            if (!isGameRunning || isGameOver) return;

            if (currentPhase != PhaseType.ActionPhase)
            {
                Debug.LogWarning($"[Turn] Reject anomaly resolve request: current phase is {currentPhase}.");
                return;
            }

            if (isPhaseTransitioning || isResolvingAction || actionWindowState != ActionWindowState.Idle)
            {
                Debug.LogWarning($"[Turn] Reject anomaly resolve request: action window is busy ({actionWindowState}).");
                return;
            }

            // Rule placeholder: each player can actively resolve anomaly at most once per turn.
            if (hasAttemptedAnomalyResolveThisTurn)
            {
                Debug.LogWarning("[Turn] Reject anomaly resolve request: already attempted once this turn.");
                return;
            }

            hasAttemptedAnomalyResolveThisTurn = true;
            anomalyResolveAttemptCountThisTurn++;
            actionWindowState = ActionWindowState.ResolvingAnomaly;

            if (tableZoneModule == null)
            {
                Debug.LogWarning("[Turn] Reject anomaly resolve request: TableZoneModule reference is missing.");
                actionWindowState = ActionWindowState.Idle;
                return;
            }

            if (!tableZoneModule.HasCurrentAnomaly())
            {
                Debug.LogWarning("[Turn] Reject anomaly resolve request: there is no current anomaly.");
                actionWindowState = ActionWindowState.Idle;
                return;
            }

            // Placeholder flow only:
            // 1) Mark current anomaly as resolved and move it to resolved exile.
            // 2) Immediately reveal the next anomaly (without reward/payment logic).
            if (tableZoneModule.TryMoveCurrentAnomalyToResolvedExile(out var resolvedAnomaly))
            {
                hasResolvedAnomalyThisTurn = true;
                string anomalyName = resolvedAnomaly != null ? resolvedAnomaly.cardName : "UnknownAnomaly";
                Debug.Log($"[Turn] Placeholder anomaly resolve succeeded: {anomalyName} moved to resolved exile.");

                // Placeholder continuation: reveal next anomaly immediately.
                tableZoneModule.RevealTopAnomaly();
            }
            else
            {
                Debug.LogWarning("[Turn] Placeholder anomaly resolve failed: could not move current anomaly to resolved exile.");
            }

            actionWindowState = ActionWindowState.Idle;
        }

        private void OnRequestSummonCard(int cardId)
        {
            if (!isGameRunning || isGameOver) return;

            if (currentPhase != PhaseType.SummonPhase)
            {
                Debug.LogWarning($"[Turn] Reject summon request: current phase is {currentPhase}.");
                return;
            }

            if (!isSummonPhaseOpen)
            {
                Debug.LogWarning("[Turn] Reject summon request: summon phase loop is not open.");
                return;
            }

            if (isPhaseTransitioning || isResolvingAction)
            {
                Debug.LogWarning("[Turn] Reject summon request: another action/transition is in progress.");
                return;
            }

            // Placeholder only: actual summon cost check / zone movement / refill logic not implemented yet.
            isResolvingAction = true;
            Debug.Log($"[Turn] TODO: summon flow is not implemented yet. Requested cardId={cardId}");
            summonActionsThisPhase++;
            isResolvingAction = false;
        }

        private void AdvancePhase()
        {
            switch (currentPhase)
            {
                case PhaseType.ActionPhase:
                    EnterPhase(PhaseType.SummonPhase);
                    break;

                case PhaseType.SummonPhase:
                    EnterPhase(PhaseType.EndPhase);
                    StartCoroutine(ProcessEndPhase());
                    break;
            }
        }

        private void EnterPhase(PhaseType phase)
        {
            isPhaseTransitioning = true;
            currentPhase = phase;
            Debug.Log($"<color=cyan>[Turn]</color> Enter phase: {phase}");
            GameEvent.OnPhaseChanged?.Invoke(phase);

            switch (phase)
            {
                case PhaseType.StartPhase:
                    actionWindowState = ActionWindowState.Closed;
                    isSummonPhaseOpen = false;
                    break;
                case PhaseType.ActionPhase:
                    actionWindowState = ActionWindowState.Idle;
                    isSummonPhaseOpen = false;
                    break;
                case PhaseType.SummonPhase:
                    actionWindowState = ActionWindowState.Closed;
                    isSummonPhaseOpen = true;
                    summonActionsThisPhase = 0;
                    break;
                case PhaseType.EndPhase:
                    actionWindowState = ActionWindowState.WaitingForDiscard;
                    isSummonPhaseOpen = false;
                    break;
            }

            isPhaseTransitioning = false;
        }

        private IEnumerator ProcessEndPhase()
        {
            Coin = 0;
            Magic = 0;
            SkillPoint = 0;

            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, 0);

            if (playerZoneModules == null || activePlayerId < 0 || activePlayerId >= playerZoneModules.Length)
            {
                Debug.LogError("[Turn] PlayerZoneModules reference is invalid.");
                yield break;
            }

            var currentZone = playerZoneModules[activePlayerId];
            if (currentZone == null)
            {
                Debug.LogError($"[Turn] PlayerZoneModule for player {activePlayerId} is missing.");
                yield break;
            }

            int discardedPlayedCards = currentZone.ClearPlayedCardsToDiscard();
            if (discardedPlayedCards > 0)
            {
                Debug.Log($"[Turn] End phase cleanup: discarded {discardedPlayedCards} played card(s) from battlefield.");
            }
            yield return new WaitForSeconds(0.5f);

            if (currentZone.HandCount < 6)
            {
                int drawCount = 6 - currentZone.HandCount;
                currentZone.DrawCards(drawCount);
                yield return new WaitForSeconds(0.5f);
            }
            else while (currentZone.HandCount > 6)
            {
                int overflow = currentZone.HandCount - 6;
                actionWindowState = ActionWindowState.WaitingForDiscard;
                GameEvent.OnDiscardRequired?.Invoke(overflow);
                yield return null;
            }

            actionWindowState = ActionWindowState.Closed;
            GameEvent.OnDiscardRequired?.Invoke(0);
            yield return new WaitForSeconds(0.3f);
            EndTurn();
        }

        private void EndTurn()
        {
            if (isGameOver)
            {
                isGameRunning = false;
                Debug.Log("[Turn] EndTurn stopped because the game is over.");
                return;
            }

            activePlayerId = (activePlayerId + 1) % totalPlayers;
            turnCount++;
            StartTurn(activePlayerId);
        }

        private void ResetPerTurnActionState()
        {
            isResolvingAction = false;
            isPhaseTransitioning = false;
            actionWindowState = ActionWindowState.Closed;

            hasAttemptedAnomalyResolveThisTurn = false;
            hasResolvedAnomalyThisTurn = false;
            anomalyResolveAttemptCountThisTurn = 0;

            isSummonPhaseOpen = false;
            summonActionsThisPhase = 0;
        }
    }
}
