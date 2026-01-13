using System.Collections.Generic;
using UnityEngine;
using CrescentWreath.Core;

namespace CrescentWreath.Modules
{
    /// <summary>
    /// 状态管理器 (Server Logic)
    /// 职责：维护玩家当前的 Buff 列表，处理自动过期逻辑。
    /// </summary>
    public class StatusModule : MonoBehaviour
    {
        [Header("Debug Info")]
        public int playerId = 0;

        // 使用 HashSet 保证同一状态不会重复存在 (Unique)
         // [cite: 277-281] 规则隐含：不能存在复数相同的buff
        private HashSet<StatusType> _activeStatuses = new HashSet<StatusType>();

        private void OnEnable()
        {
            // 监听回合流程，处理 Buff 的自动过期
            GameEvent.OnTurnStarted += OnTurnStarted;
            GameEvent.OnPhaseChanged += OnPhaseChanged;
            
            // 监听伤害逻辑（为了处理结界消耗）
            // GameEvent.OnDamageTaken += OnDamageTaken; // 稍后在伤害系统里完善
        }

        private void OnDisable()
        {
            GameEvent.OnTurnStarted -= OnTurnStarted;
            GameEvent.OnPhaseChanged -= OnPhaseChanged;
        }

        // ========================================================================
        // 1. 对外接口 (API)
        // ========================================================================

        /// <summary>
        /// 尝试添加状态
        /// </summary>
        public void AddStatus(StatusType status)
        {
            if (_activeStatuses.Contains(status))
            {
                Debug.Log($"[Status] 玩家 {playerId} 已拥有 {status}，无需重复添加。");
                return;
            }

            _activeStatuses.Add(status);
            Debug.Log($"<color=green>[Status]</color> 玩家 {playerId} 获得了 【{status}】");
            
            // 广播给 UI
            GameEvent.OnPlayerStatusChanged?.Invoke(playerId, status, true);
        }

        /// <summary>
        /// 尝试移除状态
        /// </summary>
        public void RemoveStatus(StatusType status)
        {
            if (_activeStatuses.Contains(status))
            {
                _activeStatuses.Remove(status);
                Debug.Log($"<color=red>[Status]</color> 玩家 {playerId} 移除了 【{status}】");
                
                // 广播给 UI
                GameEvent.OnPlayerStatusChanged?.Invoke(playerId, status, false);
            }
        }

        /// <summary>
        /// 检查是否拥有某状态 (供 LogicModule/Lua 查询)
        /// </summary>
        public bool HasStatus(StatusType status)
        {
            return _activeStatuses.Contains(status);
        }

        // ========================================================================
        // 2. 自动过期逻辑 (Expiration Logic)
        // ========================================================================

        /// <summary>
        /// 处理回合开始时的过期：封印、禁锢、结界(通常不自动消，但被击杀会消，这里主要处理时效性Buff)
        /// </summary>
        private void OnTurnStarted(int currentTurnPlayerId)
        {
            // 只处理自己的回合开始
            if (currentTurnPlayerId != playerId) return;

             // [cite: 280] 封印：自己的下个回合开始时清除
            if (HasStatus(StatusType.Seal))
            {
                RemoveStatus(StatusType.Seal);
            }

             // [cite: 279] 禁锢：下个回合开始时触发惩罚（弃牌逻辑在 TurnModule 写），然后清除
            // 注意：这里我们只负责“清除状态”这个动作，具体的弃牌惩罚应该由 TurnModule 检测到有状态时去执行
            if (HasStatus(StatusType.Imprisonment))
            {
                // TODO: 通知 TurnModule 执行禁锢惩罚 (弃4张牌或跳过)
                // 惩罚执行完毕后移除状态
                RemoveStatus(StatusType.Imprisonment);
            }
            
            // 结界(Barrier) 通常是抵挡伤害后消失，或者被击杀后消失，不会随回合开始自动消失
        }

        /// <summary>
        /// 处理回合结束时的过期：沉默
        /// </summary>
        private void OnPhaseChanged(PhaseType phase)
        {
            // 我们假设是在 EndPhase 结束时清理
            if (phase == PhaseType.EndPhase)
            {
                 // [cite: 277] 沉默：本回合无法使用响应技 (持续到当前回合结束)
                // 这里需要判断：如果是“被沉默的角色”所在的当前回合结束了？
                // 规则书写的是“本回合”，通常指“当前正在进行的回合”。
                // 如果是A回合，B被沉默了，那B在A回合结束前都沉默。
                
                if (HasStatus(StatusType.Silence))
                {
                    RemoveStatus(StatusType.Silence);
                }
            }
        }
    }
}