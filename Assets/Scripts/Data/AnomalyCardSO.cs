using UnityEngine;

// 4. 异变卡：外部环境与任务
[CreateAssetMenu(fileName = "NewAnomaly", menuName = "DBG/Cards/Anomaly")]
public class AnomalyCardSO : BaseCardSO
{
    [TextArea] public string adventEffect; // 翻开时的即时影响
    [TextArea] public string condition;    // 解决它的代价
    [TextArea] public string reward;       // 解决后的收益
}