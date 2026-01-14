using System;
using UnityEngine;

namespace CrescentWreath.Core
{
    /// <summary>
    /// 全局事件中枢 (Event Bus) V3.0 - 融合规则书与响应系统
    /// 核心职责：解耦 View 层与 Logic 层，并提供“询问-打断”机制
    /// </summary>
    public static class GameEvent
    {
        // ========================================================================
        // 1. 游戏流程控制 (Phase & Flow) [cite: 173-175]
        // ========================================================================

        /// <summary>
        /// 广播：回合开始（处理开始阶段Buff、重置技能值）
        /// </summary>
        public static Action<int> OnTurnStarted; // 参数：当前回合玩家ID

        /// <summary>
        /// 广播：阶段变更 (Start -> Action -> Summon -> End)
        /// </summary>
        public static Action<PhaseType> OnPhaseChanged;

        /// <summary>
        /// 广播：异变卡翻开/降临 [cite: 130]
        /// </summary>
        public static Action<BaseCardSO> OnAnomalyRevealed;

        // ========================================================================
        // 2. 玩家交互请求 (Client Requests) - UI 发起 -> Logic 响应
        // ========================================================================

        /// <summary>
        /// 请求：打出宝具手牌 (获得魔力/灵符/效果)
        /// 参数：手牌索引 (Index)
        /// </summary>
        public static Action<int> Request_PlayHandCard;

        /// <summary>
        /// 请求：使用角色技能
        /// 参数：技能槽位索引 (0:通常 1:响应 2:启动 3:终结) [cite: 83]
        /// </summary>
        public static Action<int> Request_UseCharacterSkill;

        /// <summary>
        /// 请求：从召唤区/樱花饼区 购买卡牌 [cite: 174]
        /// 参数：目标卡牌的 ConfigID
        /// </summary>
        public static Action<int> Request_SummonCard;

        /// <summary>
        /// 请求：尝试解决当前异变 [cite: 124]
        /// </summary>
        public static Action Request_ResolveAnomaly;

        /// <summary>
        /// 请求：丢弃一张手牌 (用于结束阶段手牌溢出时)
        /// 参数：手牌索引 Index
        /// </summary>
        public static Action<int> Request_DiscardHandCard;

        /// <summary>
        /// 请求：结束当前阶段/回合
        /// </summary>
        public static Action Request_EndPhase;

        // ========================================================================
        // 3. 核心数据同步 (State Sync / Broadcast) - Logic 变更 -> UI 表现
        // ========================================================================

        /// <summary>
        /// 广播：资源变动 (魔力/灵符/技能值/灵脉)
        /// 参数：资源类型, 变动后的最终值
        /// </summary>
        public static Action<ResourceType, int> OnResourceChanged;

        /// <summary>
        /// 广播：卡牌移动 (核心视觉逻辑)
        /// 参数：卡牌数据, 来源区域, 目标区域, 飞向哪个玩家的区域（0,1，2,3）
        /// </summary>
        // 默认值 -1 表示不需要具体位置
        public static Action<BaseCardSO, ZoneType, ZoneType, int, int> OnCardMoved;

        /// <summary>
        /// 广播：发生了一次确定的伤害结算 (UI飘字)
        /// 参数：来源ID, 目标ID, 数值, 类型
        /// </summary>
        public static Action<int, int, int, DamageType> OnDamageDealt;

        // ========================================================================
        // 4. 响应与询问系统 (Reaction Query System) - 核心博弈机制
        // ========================================================================

        /// <summary>
        /// 广播：开启一个响应窗口 (询问玩家是否发动【响应技】)
        /// 参数1: 时点类型 (如 OnDamageTaken)
        /// 参数2: 上下文数据 (包含伤害值、来源等，可被修改)
        /// 参数3: 回调函数 (响应处理完毕后，必须调用此 Action 继续流程)
        /// </summary>
        public static Action<ReactionTiming, ReactionContext, Action> OnQueryReaction;

        // ========================================================================
        // [新增] 5. 降临与环境事件 (Advent & Environment)
        // ========================================================================

        /// <summary>
        /// 广播：触发【降临】效果 [cite: 106, 130]
        /// 说明：当宝具补充进召唤区，或新异变翻开时触发。
        /// 注意：ZoneModule 在负责填充卡槽时，如果是游戏进行中（非Setup），必须广播此事件。
        /// 参数：触发降临的卡牌数据
        /// </summary>
        public static Action<BaseCardSO> OnAdventTriggered;

        // ========================================================================
        // [修正] ReactionTiming 枚举补充
        // ========================================================================

        // 既然有降临，那么理论上也许会有技能描述为：“当一张卡降临...时”
        // 虽然目前卡表里可能不多，但为了架构完整性，建议在 ReactionTiming 里加上它。

        // ========================================================================
        // [新增] 6. 状态与Buff系统 (Status System)
        // ========================================================================

        /// <summary>
        /// 广播：玩家状态发生变化 (用于 UI 刷新图标)
        /// 参数1: 玩家ID
        /// 参数2: 状态类型
        /// 参数3: 是添加(true) 还是 移除(false)
        /// </summary>
        public static Action<int, StatusType, bool> OnPlayerStatusChanged;
        /// <summary>
        /// 广播：通知玩家需要弃牌
        /// 参数：需要弃掉的数量 (如果为 0 则代表弃牌阶段结束)
        /// </summary>
        public static Action<int> OnDiscardRequired;

        // [交互] 请求显示卡牌详情（悬停时触发）
        // 参数：卡牌数据，鼠标当前屏幕坐标 (Vector3)
        public static Action<BaseCardSO, Vector3> Request_ShowCardTooltip;

        // [交互] 请求隐藏卡牌详情（移开时触发）
        public static Action Request_HideCardTooltip;

        // [交互] 请求 TTS 式放大查看（按住 Ctrl 时触发）
        // 参数：卡牌数据
        public static Action<BaseCardSO> Request_ZoomCard;

        // [交互] 请求取消放大
        public static Action Request_HideZoom;

       

    }

    /// <summary>
    /// 玩家特殊状态 [cite: 277-281]
    /// </summary>
    public enum StatusType
    {
        None,
        Seal,           // 封印 (防御-1, 下个回合开始时解除)
        Imprisonment,   // 禁锢 (下个回合开始时弃牌/跳过, 之后解除)
        Barrier,        // 结界 (免疫一次伤害, 触发后解除)
        Silence,        // 沉默 (本回合无法使用响应技, 回合结束时解除)

    }

    // ========================================================================
    // 基础枚举定义 (Enums) - 对应规则书概念
    // ========================================================================

    /// <summary>
    /// 游戏阶段 [cite: 173-175]
    /// </summary>
    public enum PhaseType
    {
        StartPhase,   // 开始阶段 (防御牌回手)
        ActionPhase,  // 行动阶段 (打牌/技能/异变)
        SummonPhase,  // 召唤阶段 (买牌)
        EndPhase      // 结束阶段 (弃牌/抽牌)
    }

    /// <summary>
    /// 玩家资源 [cite: 286-288]
    /// </summary>
    public enum ResourceType
    {
        Magic,        // 魔力 (褐色)
        Coin,         // 灵符 (蓝色)
        SkillPoint,   // 技能值 (沙漏)
        Score,        // 击杀分
        HP,           // 生命值
        Vein          // 灵脉 (队伍资源)
    }

    /// <summary>
    /// 伤害类型 [cite: 197]
    /// </summary>
    public enum DamageType
    {
        Physical,     // 体术 (黑色)
        Magic,        // 咒术 (黄色)
        Direct        // 直接伤害 (无法防御)
    }

    /// <summary>
    /// 卡牌区域 [cite: 161-169]
    /// </summary>
    public enum ZoneType
    {
        Unknown,
        Deck,         // 个人卡组
        Hand,         // 手牌
        Battlefield,  // 阵地区 (打出的牌/防御牌暂存区)
        Discard,      // 弃牌堆
        SummonZone,   // 召唤区 (公共)
        Exile,        // 间隙区 (放逐)
        AnomalyDeck,   // 异变卡组 (公共)
        SakuraMochiDeck, // 樱花饼区 (公共)

    }

    /// <summary>
    /// 响应时点枚举 - 仅定义流程节点，不定义具体条件
    /// </summary>
    public enum ReactionTiming
    {
        None,
        // 全局时点
        OnTurnStart,
        OnPhaseChange,

        // 行为时点
        OnCardPlayed,       // 卡牌被打出时
        OnSkillActivated,   // 技能发动时
        OnSummon,           // 购买卡牌时

        // 战斗伤害流程 [cite: 205-209]
        OnBeforeDamage,     // 伤害发生前 (增伤/减伤)
        OnDefense,          // 防御宣言时 (是否出防御牌)
        OnDamageTaken,      // 确实受到伤害时 (获得灵脉)
        OnSurvival,         // 受到伤害未死时 (反击)
        OnKill,             // 击杀对手时
        OnDeath,            // 自身被击杀时

        // 降临与异变
        OnAdvent,           // [新增] 当降临效果触发时
    }

    /// <summary>
    /// 响应上下文 - 在事件流中传递的“可变”数据包
    /// </summary>
    public class ReactionContext
    {
        public int SourceId;        // 来源玩家
        public int TargetId;        // 目标玩家
        public BaseCardSO Card;     // 关联卡牌/技能

        public int Value;           // 数值 (伤害值/回复值/抽牌数)
        public DamageType DmgType;  // 伤害类型

        public bool IsCanceled;     // 是否被打断/免疫
    }

}