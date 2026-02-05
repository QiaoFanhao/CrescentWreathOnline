-- 效果：抽2张牌；可以从召唤区放逐1张牌
local Card_23023 = {}

function Card_23023:OnPlay(context)
    -- 1. 抽 2 张牌
    context:DrawCards(context.ownerId, 2)
    
    -- 2. 放逐逻辑 (此处先留个日志，等待后续 UI 选择逻辑)
    print("[Lua] 准备移动一张牌从 SummonZone 到 ExileZone")
    -- context:MoveCard(targetId, "SummonZone", "ExileZone", 0)
end

return Card_23023