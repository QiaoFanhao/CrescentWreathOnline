using System.Collections;
using UnityEngine;
using CrescentWreath.Core;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// 回合管理器 (Server Logic)
    /// 职责：驱动游戏阶段流转，处理资源重置与回合交替。
    /// 整合功能：公有资源池管理（灵符、魔力、技能点）
    /// </summary>
    public class TurnModule : MonoBehaviour
    {
        [Header("State Info")]
        public int activePlayerId = 0;
        public PhaseType currentPhase;
        public int turnCount = 0;

        [Header("Global Resource Pool (公有池)")]
        // 灵符、魔力、技能点仅供当前 activePlayer 使用
        public int Coin = 0;
        public int Magic = 0;
        public int SkillPoint = 0;

        [Header("Config")]
        public int totalPlayers = 4;
        
        [Header("Module References")]
        public TableZoneModule tableZoneModule;
        public PlayerZoneModule[] playerZoneModules;
        public StatusModule[] playerStatusModules;

        private void OnEnable()
        {
            GameEvent.Request_EndPhase += OnRequestEndPhase;
        }

        private void OnDisable()
        {
            GameEvent.Request_EndPhase -= OnRequestEndPhase;
        }

        public void StartGame()
        {
            Debug.Log("<color=cyan>[Turn]</color> 游戏点火！");
            
            if (tableZoneModule != null)
                tableZoneModule.InitializeTable();

            foreach (var playerZone in playerZoneModules)
            {
                if (playerZone != null)
                    playerZone.InitializePlayerDeck();
            }

            turnCount = 1;
            activePlayerId = 0; 
            StartTurn(activePlayerId);
        }

        /// <summary>
        /// 公有接口：供 Lua 效果结算后调用，增加当前回合池的资源
        /// </summary>
        public void AddResources(int coin, int magic, int sp = 0)
        {
            Coin += coin;
            Magic += magic;
            SkillPoint += sp;

            // 触发事件通知 UI 更新数字
            // 这里我们复用 OnResourceChanged，由于是公有池，所有玩家 UI 看到的数字是一致的
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, Coin);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, Magic);
            if (sp != 0)
                GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, SkillPoint);
            
            Debug.Log($"<color=yellow>[Resource]</color> 资源更新: 灵符+{coin}, 魔力+{magic}, 技能点+{sp}");
        }

        private void StartTurn(int playerId)
        {
            Debug.Log($"<color=cyan>[Turn]</color> === 玩家 {playerId} 回合开始 ===");

            // 1. 广播回合开始
            GameEvent.OnTurnStarted?.Invoke(playerId);

            // 2. [规则] 资源初始化：灵符/魔力归零，技能点重置为 1
            Coin = 0;
            Magic = 0;
            SkillPoint = 1;

            // 同步 UI
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, 1);

            EnterPhase(PhaseType.StartPhase);
            StartCoroutine(ProcessStartPhase());
            
        }

        private IEnumerator ProcessStartPhase()
        {
            yield return new WaitForSeconds(0.5f);

            var statusModule = playerStatusModules[activePlayerId];
            if (statusModule != null && statusModule.HasStatus(StatusType.Imprisonment))
            {
                Debug.LogWarning($"[Turn] 玩家 {activePlayerId} 处于禁锢中，跳过此回合！");
                EndTurn();
                yield break;
            }

            EnterPhase(PhaseType.ActionPhase);
        }

        private void OnRequestEndPhase()
        {
            AdvancePhase();
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
            currentPhase = phase;
            Debug.Log($"<color=cyan>[Turn]</color> 进入阶段: {phase}");
            GameEvent.OnPhaseChanged?.Invoke(phase);
        }

        private IEnumerator ProcessEndPhase()
        {
            // 1. [规则] 资源清零
            Coin = 0;
            Magic = 0;
            SkillPoint = 0;

            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, 0);

            // 2. [规则] 清理阵地
            var currentZone = playerZoneModules[activePlayerId];
            currentZone.ClearBattlefield();
            
            yield return new WaitForSeconds(0.5f);

            // 3. [规则] 手牌平衡
            if (currentZone.HandCount < 6)
            {
                int drawCount = 6 - currentZone.HandCount;
                currentZone.DrawCards(drawCount);
                yield return new WaitForSeconds(0.5f); 
            }
            else while (currentZone.HandCount > 6)
            {
                int overflow = currentZone.HandCount - 6;
                GameEvent.OnDiscardRequired?.Invoke(overflow);
                yield return null; 
            }

            GameEvent.OnDiscardRequired?.Invoke(0);
            yield return new WaitForSeconds(0.3f);
            EndTurn();
        }

        private void EndTurn()
        {
            activePlayerId = (activePlayerId + 1) % totalPlayers;
            turnCount++;
            StartTurn(activePlayerId);
        }
    }
}