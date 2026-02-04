using UnityEngine;
using TMPro; // 假设你使用的是 TextMeshPro
using CrescentWreath.Core;

namespace CrescentWreath.View
{
    /// <summary>
    /// 资源 UI 控制器
    /// 职责：监听资源变动事件，实时更新 UI 上的魔力、灵符与技能点数值。
    /// </summary>
    public class ResourceUIController : MonoBehaviour
    {
        [Header("Text References")]
        public TextMeshProUGUI magicText;      // 对应左边的数字 (魔力)
        public TextMeshProUGUI coinText;       // 对应右边的数字 (灵符)
        public TextMeshProUGUI skillPointText; // 对应紫色的数字 (技能点)

        private void OnEnable()
        {
            // 订阅资源变动事件
            GameEvent.OnResourceChanged += UpdateResourceDisplay;
        }

        private void OnDisable()
        {
            // 取消订阅，防止内存泄漏
            GameEvent.OnResourceChanged -= UpdateResourceDisplay;
        }

        /// <summary>
        /// 根据收到的资源类型和数值更新对应的文本
        /// </summary>
        private void UpdateResourceDisplay(ResourceType type, int newValue)
        {
            switch (type)
            {
                case ResourceType.Magic:
                    if (magicText != null) magicText.text = newValue.ToString();
                    break;

                case ResourceType.Coin:
                    if (coinText != null) coinText.text = newValue.ToString();
                    break;

                case ResourceType.SkillPoint:
                    if (skillPointText != null) skillPointText.text = newValue.ToString();
                    break;
            }
        }
    }
}