using System.Collections;
using UnityEngine;
using CrescentWreath.Core;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// 回合管理器 (Server Logic)
    /// 职责：驱动游戏阶段流转，处理资源重置与回合交替。
    /// </summary>
    public class TurnModule : MonoBehaviour
    {
        [Header("State Info")]
        public int activePlayerId = 0;
        public PhaseType currentPhase;
        public int turnCount = 0;

        [Header("Config")]
        public int totalPlayers = 4;
        
        [Header("Module References")]
        public TableZoneModule tableZoneModule;        // 引用唯一的公共区域模块
        public PlayerZoneModule[] playerZoneModules;   // 引用 4 个玩家的个人区域模块
        public StatusModule[] playerStatusModules;     // 引用 4 个玩家的状态模块

        private void OnEnable()
        {
            GameEvent.Request_EndPhase += OnRequestEndPhase;
        }

        private void OnDisable()
        {
            GameEvent.Request_EndPhase -= OnRequestEndPhase;
        }

        private void Start()
        {
            // 稍作延迟，确保所有模块的 Awake 已经执行完毕
            Invoke(nameof(StartGame), 0.5f);
        }

        public void StartGame()
        {
            Debug.Log("<color=cyan>[Turn]</color> 游戏点火！正在初始化桌面...");
            
            // 1. [核心] 先让公共桌面初始化 (洗宝具堆、翻开召唤区)
            if (tableZoneModule != null)
                tableZoneModule.InitializeTable();

            // 2. [核心] 初始化所有玩家的个人卡组并抽初始手牌
            foreach (var playerZone in playerZoneModules)
            {
                if (playerZone != null)
                    playerZone.InitializePlayerDeck();
            }

            turnCount = 1;
            activePlayerId = 0; // 默认为 P1 (Id 0) 开始
            StartTurn(activePlayerId);
        }

        private void StartTurn(int playerId)
        {
            Debug.Log($"<color=cyan>[Turn]</color> === 玩家 {playerId} 回合开始 ===");

            // 1. 广播回合开始
            GameEvent.OnTurnStarted?.Invoke(playerId);

            // 2. [规则] 重置技能点为 1 [cite: 251]
            GameEvent.OnResourceChanged?.Invoke(ResourceType.SkillPoint, 1);

            // 3. 进入开始阶段
            EnterPhase(PhaseType.StartPhase);

            // 4. 处理开始阶段逻辑
            StartCoroutine(ProcessStartPhase());
        }

        private IEnumerator ProcessStartPhase()
        {
            yield return new WaitForSeconds(0.5f);

            // --- 检查【禁锢】状态 [cite: 278] ---
            var statusModule = playerStatusModules[activePlayerId];
            if (statusModule != null && statusModule.HasStatus(StatusType.Imprisonment))
            {
                Debug.LogWarning($"[Turn] 玩家 {activePlayerId} 处于禁锢中，跳过此回合！");
                EndTurn();
                yield break;
            }

            // --- 防御牌回手 [cite: 172, 200] ---
            // 注意：这一步可以在 PlayerZoneModule 监听 OnTurnStarted 自动完成，
            // 也可以在这里显式调用相关逻辑。

            EnterPhase(PhaseType.ActionPhase);
        }

        private void OnRequestEndPhase()
        {
            // 可以在此处加入权限检查：if (senderId != activePlayerId) return;
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

        /// <summary>
        /// 结束阶段：资源清空、阵地清理、手牌平衡 [cite: 175, 285, 286]
        /// </summary>
        private IEnumerator ProcessEndPhase()
        {
            // 1. [规则] 资源清零
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Magic, 0);
            GameEvent.OnResourceChanged?.Invoke(ResourceType.Coin, 0);

            // 2. [规则] 让当前玩家清理阵地区 (打出的牌进弃牌堆)
            var currentZone = playerZoneModules[activePlayerId];
            currentZone.ClearBattlefield();
            
            yield return new WaitForSeconds(0.5f);

            // 3. [规则] 手牌补给与平衡
            // A. 手牌不足 6 张：自动补满 
            if (currentZone.HandCount < 6)
            {
                int drawCount = 6 - currentZone.HandCount;
                currentZone.DrawCards(drawCount);
                yield return new WaitForSeconds(0.5f); 
            }
            // B. 手牌溢出 6 张：阻塞并等待玩家手动弃牌 
            else while (currentZone.HandCount > 6)
            {
                int overflow = currentZone.HandCount - 6;
                GameEvent.OnDiscardRequired?.Invoke(overflow);
                // 挂起，等待下一帧直到玩家通过 UI 触发 Request_DiscardHandCard 导致 HandCount 减少
                yield return null; 
            }

            // 4. 确认平衡完成，关闭 UI 提示
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