using UnityEngine;
using System.Collections.Generic;

namespace CrescentWreath.View
{
    public class HandLayoutManager : MonoBehaviour
    {
        [Header("布局参数 (Inspector可调)")]
        [Tooltip("扇形半径：建议设大一点 (2000-3000)，正数表示圆心在屏幕下方")]
        public float radius = 2500f; 
        
        [Tooltip("卡牌间距角度：建议 3-5 度")]
        public float anglePerCard = 4.5f;
        
        [Tooltip("整体Y轴偏移：微调高度")]
        public float yOffset = -200f; 

        private List<UICardView> _cards = new List<UICardView>();

        private void OnTransformChildrenChanged() => UpdateListAndLayout();
        public void AddCard(UICardView card) => UpdateListAndLayout();
        public void RemoveCard(UICardView card) => UpdateListAndLayout();

        [ContextMenu("强制刷新布局")]
        public void UpdateListAndLayout()
        {
            _cards.Clear();
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                var card = child.GetComponent<UICardView>();
                if (card != null) _cards.Add(card);
            }
            CalculateLayout();
        }

        private void CalculateLayout()
        {
            int count = _cards.Count;
            if (count == 0) return;

            // 中心索引 (比如 5张牌，中心是2.0)
            float centerIndex = (count - 1) / 2f;

            for (int i = 0; i < count; i++)
            {
                UICardView card = _cards[i];
                
                // 1. 计算偏移 (左边为负，右边为正)
                float offsetFromCenter = i - centerIndex;

                // 2. 计算角度
                // 我们希望：
                // 左边的牌 (offset < 0) -> 往左歪 (正角度, Unity逆时针为正)
                // 右边的牌 (offset > 0) -> 往右歪 (负角度)
                float angleDeg = -offsetFromCenter * anglePerCard; 
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // 3. 核心坐标公式 (圆心在下方)
                // Math.Sin(angle) : 角度为正(左倾)时，x应该是负的(往左移) -> 所以要加个负号或者调整逻辑
                // 这里我们用修正后的逻辑：
                // 如果 angleDeg 是正数 (10度, 左倾), Sin是正数. 
                // 但我们希望它在左边 (X < 0). 所以 X = -Radius * Sin.
                // 或者更直观的：左边的牌 offset 是负数，我们希望 X 是负数。
                
                // 重新推导：
                // 假设圆心在 (0, -R). 
                // 卡牌位置 = (R * sin(theta), R * cos(theta) - R)
                // 这里 theta 对应 offset. 
                // 左边 (offset < 0) -> theta < 0 -> sin < 0 (X在左边, 对了)
                
                float theta = offsetFromCenter * anglePerCard * Mathf.Deg2Rad;
                
                float x = radius * Mathf.Sin(theta);
                float y = (radius * Mathf.Cos(theta)) - radius; // 中间高(0), 两边低(负数)

                // 4. 计算旋转
                // 卡牌在左边(X<0)时，应该向右歪(Z轴负方向? 不，是Z轴正方向CCW)
                // 想象时钟：11点钟方向(在左边)，顶部指向左上，这是 Z轴正旋转。
                // 所以旋转角度应该 = -theta * Rad2Deg
                float rotationZ = -offsetFromCenter * anglePerCard;

                // 应用
                card.LayoutPosition = new Vector2(x, y + yOffset);
                card.LayoutRotation = Quaternion.Euler(0, 0, rotationZ);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying) UpdateListAndLayout();
        }
    }
}