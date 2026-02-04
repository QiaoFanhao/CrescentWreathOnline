-- EffectConst.lua
EffectConst = {
    -- 资源类型 (必须与 C# ResourceType 一致)
    ResourceType = {
        Magic = 0,
        Coin = 1,
        SkillPoint = 2,
    },
    
    -- 阶段类型 (参考 PhaseType)
    Phase = {
        Start = 0,
        Action = 1,
        Summon = 2,
        End = 3,
    }
}