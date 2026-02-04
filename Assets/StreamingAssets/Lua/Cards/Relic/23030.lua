local CardBase = require("Base.CardBase")
local Card_23030 = CardBase.New(23030)

-- 重写打出逻辑
function Card_23030:OnPlay(context)
    -- 1. 增加技能点 (调用 context 注入的 C# 方法)
    context:AddSkillPoint(1)
    
    -- 2. 处理伤害
    if context.targetId ~= -1 then
        context:DealDamage(context.targetId, 3)
    end
end

return Card_23030