-- CardBase.lua
local CardBase = {}
CardBase.__index = CardBase

function CardBase.New(id)
    local instance = {
        id = id,
        -- 这里可以存储一些卡牌运行时的临时状态
    }
    setmetatable(instance, CardBase)
    return instance
end

-- 钩子函数：打出时执行
function CardBase:OnPlay(context)
    -- 子类重写此方法
end

-- 钩子函数：回合开始触发
function CardBase:OnTurnStart(context)
end

return CardBase