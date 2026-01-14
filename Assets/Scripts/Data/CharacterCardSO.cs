using UnityEngine;

// 3. 角色卡：玩家的化身（不进入牌组）
[CreateAssetMenu(fileName = "NewCharacter", menuName = "DBG/Cards/Character")]
public class CharacterCardSO : BaseCardSO
{
    [Header("身份属性")]
    public string camp;          // 阵营（仅角色拥有）
    public string race;          // 种族

    [Header("技能组")]
    public string[] skills = new string[4]; // 恒定四个技能
}


