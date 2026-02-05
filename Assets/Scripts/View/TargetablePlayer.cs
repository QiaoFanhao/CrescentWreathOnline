using UnityEngine;

namespace CrescentWreath.View
{
    /// <summary>
    /// 挂在场景中代表玩家的 3D 物体上（如头像或区域底板）
    /// 必须配合 Collider 使用！
    /// </summary>
    public class TargetablePlayer : MonoBehaviour
    {
        public int playerId; // 在 Inspector 里填好：对手1就填1，对手2填2

        // 可以加一些鼠标悬停的高亮效果
        private void OnMouseEnter()
        {
            // TODO: 比如让模型发光，或者鼠标变色
            Debug.Log($"[Target] 鼠标指向了玩家 {playerId}");
        }
    }
}