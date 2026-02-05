using System;
using CrescentWreath.Data;

namespace CrescentWreath.Core
{
    /// <summary>
    /// Lua 异步指令基类
    /// 所有 Lua 想要 "等待" 的事情都继承自它
    /// </summary>
    public abstract class LuaYieldInstruction
    {
        // 当指令完成时，系统会调用此 Action，并把结果传回给 Lua
        public Action<object> OnCompleted;
    }

    /// <summary>
    /// 【升级版】通用选择请求
    /// 可以请求选择玩家、场上的怪、手牌、或者墓地的牌
    /// </summary>
    public class WaitForSelection : LuaYieldInstruction
    {
        public string Zone;      // "Player", "Battlefield", "Hand", etc.
        public int Scope;        // 0=Self, 1=Enemy, 2=All
        public string FilterTag; // 筛选标签
        public int Count;        // 选几个

        public WaitForSelection(string zone, int scope, string filterTag = "", int count = 1)
        {
            Zone = zone;
            Scope = scope;
            FilterTag = filterTag;
            Count = count;
        }
    }

    // 代表一个伤害处理请求
    public class WaitForDamageProcess : LuaYieldInstruction
    {
        public int TargetId;
        public int Amount;
        public string DamageType; // "Physical", "Magic"

        public WaitForDamageProcess(int targetId, int amount, string type)
        {
            TargetId = targetId;
            Amount = amount;
            DamageType = type;
        }
    }

}