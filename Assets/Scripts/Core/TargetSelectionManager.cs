using System;
using UnityEngine;
using CrescentWreath.View; // 引用 TargetablePlayer 所在的命名空间

namespace CrescentWreath.Core
{
    public class TargetSelectionManager : MonoBehaviour
    {
        public static TargetSelectionManager Instance { get; private set; }

        [Header("References")]
        public Camera mainCamera;

        // --- 状态变量 ---
        private bool _isTargeting = false;
        private Action<int> _onTargetSelected; 
        private Action _onCancel;
        
        // 新增：筛选条件
        private int _requiredScope; // 0=自己, 1=对手, 2=全场

        private void Awake()
        {
            Instance = this;
            if (mainCamera == null) mainCamera = Camera.main;
        }

        // =========================================================
        //  新增接口：适配 Lua 协程的 Scope 选择
        // =========================================================
        
        /// <summary>
        /// 开始选择玩家 (Lua 驱动)
        /// </summary>
        /// <param name="scope">0=Self, 1=Enemy, 2=All</param>
        public void StartPlayerSelection(int scope, Action<int> onSelected, Action onCancel = null)
        {
            _isTargeting = true;
            _requiredScope = scope; // 记录要求的范围
            _onTargetSelected = onSelected;
            _onCancel = onCancel;

            Debug.Log($"<color=yellow>[Target]</color> 请选择一名玩家 (Scope: {scope})...");
            // TODO: 可以在这里开启 UI 提示文字，例如 "请选择对手"
        }

        // =========================================================
        //  原有的接口 (如果还要兼容旧代码可以保留，不需要则可删除)
        // =========================================================
        /*
        public void StartSelection(TargetType type, Action<int> onSelected, Action onCancel = null)
        {
            // 你可以在这里把旧枚举转换成新 Scope，保持兼容
            int scope = (type == TargetType.SingleAlly) ? 0 : 
                        (type == TargetType.SingleEnemy ? 1 : 2);
            StartPlayerSelection(scope, onSelected, onCancel);
        }
        */

        private void Update()
        {
            if (!_isTargeting) return;

            // 右键取消
            if (Input.GetMouseButtonDown(1))
            {
                CancelSelection();
                return;
            }

            // 左键确认
            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }
        }

        private void HandleClick()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 获取被点击的玩家组件
                var targetPlayer = hit.collider.GetComponent<TargetablePlayer>();
                
                if (targetPlayer != null)
                {
                    // 校验是否符合 Scope 条件
                    if (IsValidTarget(targetPlayer.playerId))
                    {
                        Debug.Log($"<color=green>[Target]</color> 有效目标: 玩家 {targetPlayer.playerId}");
                        ConfirmSelection(targetPlayer.playerId);
                    }
                    else
                    {
                        Debug.LogWarning($"<color=red>[Target]</color> 无效目标! 你需要选择 Scope={_requiredScope} 的单位。");
                        // TODO: 播放一个错误音效或震动
                    }
                }
            }
        }

        // --- 核心校验逻辑 ---
        private bool IsValidTarget(int targetPlayerId)
        {
            // 假设本地玩家 ID 总是 0 (如果做了联机，这里要改成本地玩家的真实ID)
            int localPlayerId = 0; 

            switch (_requiredScope)
            {
                case 0: // 仅己方
                    return targetPlayerId == localPlayerId;
                case 1: // 仅对手
                    return targetPlayerId != localPlayerId;
                case 2: // 任意玩家
                    return true;
                default:
                    return false;
            }
        }

        private void ConfirmSelection(int targetId)
        {
            _isTargeting = false;
            _onTargetSelected?.Invoke(targetId);
            _onTargetSelected = null;
            _onCancel = null;
        }

        public void CancelSelection()
        {
            if (!_isTargeting) return;
            
            _isTargeting = false;
            _onCancel?.Invoke();
            _onCancel = null;
            
            Debug.Log("<color=red>[Target]</color> 玩家取消了选择");
        }
    }
}