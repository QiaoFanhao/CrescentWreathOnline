using CrescentWreath.Core;
using CrescentWreath.Modules;

namespace CrescentWreath.Lua
{
    /// <summary>
    /// 这是真正传给 Lua 的对象。
    /// Lua 脚本通过 context:AddSkillPoint(1) 这种方式来调用 C#。
    /// </summary>
    public class LuaEffectContext
    {
        public int ownerId;      // 谁发动的效果
        public int targetId;     // 玩家选中的目标 ID
        public BaseCardSO card;  // 正在结算的卡牌数据

        private TurnModule _turn;
        private PlayerZoneModule[] _playerZones;

        public LuaEffectContext(int owner, int target, BaseCardSO data, TurnModule turn, PlayerZoneModule[] zones)
        {
            this.ownerId = owner;
            this.targetId = target;
            this.card = data;
            this._turn = turn;
            this._playerZones = zones;
        }

        // --- 核心方法：异步选择目标 ---
        // --- 新增：通用选择接口 ---
        // Lua 调用示例: context:SelectCard("Battlefield", 1, "NonHuman") -> 选对手场上的非人类
        public WaitForSelection AsyncSelect(string zone, int scope)
        {
            // 返回指令对象，让 Lua 去 yield
            return new WaitForSelection(zone, scope);
        }

        // 也可以加一个带 Filter 的重载
        public WaitForSelection AsyncSelect(string zone, int scope, string filter)
        {
            return new WaitForSelection(zone, scope, filter);
        }
        // --- 以下是暴露给 Lua 的“原子操作” ---

        // 增加技能点：直接调用你刚刚修改过的 TurnModule 接口
        public void AddSkillPoint(int amount)
        {
            if (_turn != null) _turn.AddResources(0, 0, amount);
        }

        public void MoveCard(int cardId, ZoneType from, ZoneType to, int ownerId)
        {
            // 调用逻辑层真正的移动处理
            // 比如：_playerZones[ownerId].ExecuteMove(cardId, from, to);
            UnityEngine.Debug.Log($"[Lua指令] 移动卡牌 {cardId}: {from} -> {to}");
        }

        // 抽牌逻辑
        public void DrawCards(int playerId, int amount)
        {
            if (playerId >= 0 && playerId < _playerZones.Length)
                _playerZones[playerId].DrawCards(amount);
        }

        // 造成伤害 (预留接口)
        public void DealDamage(int targetId, int amount)
        {
            UnityEngine.Debug.Log($"[Lua执行] 对目标 {targetId} 造成 {amount} 点伤害！");
        }

        // Lua 调用此方法，C# 返回一个指令，Lua 再 yield 出去
        public WaitForDamageProcess ApplyDamage(int targetId, int amount, string type)
        {
            return new WaitForDamageProcess(targetId, amount, type);
        }
    }
}