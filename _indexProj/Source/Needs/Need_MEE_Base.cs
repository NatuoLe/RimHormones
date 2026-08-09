using RimWorld;
using Verse;
using System.Collections.Generic;

namespace Hormones
{
    /// <summary>
    /// 代谢基础需求（水分 / 糖 / 电解质 / 蛋白质）的公共基类。
    /// 设计约定：
    ///  - 需求满值 = 健康；随时间自然消耗（消耗速率由子类覆盖）。
    ///  - 仅在 MetabolicState.Active（代谢扩展模块已启用）时才会消耗并生效；关闭时保持满值、零副作用。
    ///  - 外部进食/饮水逻辑通过 Satisfy(amount) 补充该代谢物。
    ///
    /// 这些需求由主 mod 默认加载并始终实例化，但“是否显示在需求栏”由 MetabolicState.IsLoadedMME 控制：
    /// 模块未启用 → 隐藏（ShowOnNeedList=false）、不消耗、无副作用；启用并重启 → 显示并开始按 FallPerDay 自然消耗。
    /// </summary>
    public abstract class Need_MEE_Base : Need
    {
        /// <summary>每日自然消耗速率（占 MaxLevel 的 0~1 比例）。子类按代谢特性覆盖。</summary>
        protected abstract float FallPerDay { get; }

        /// <summary>UI 阈值百分比（用于需求条配色区间）。</summary>
        protected virtual List<float> Thresholds => new List<float> { 0.15f, 0.3f, 0.7f };

        public override float MaxLevel => 1f;

        /// <summary>仅在代谢扩展模块已加载（IsLoadedMME）时显示在需求栏；否则隐藏但仍实例化、不消耗。</summary>
        public override bool ShowOnNeedList => MetabolicState.IsLoadedMME;

        protected Need_MEE_Base(Pawn pawn) : base(pawn)
        {
            threshPercents = Thresholds;
        }

        public override void SetInitialLevel()
        {
            // 初始为满值（健康状态）。与 NeedDef 的 baseLevel=1 保持一致。
            CurLevel = MaxLevel;
        }

        public override void NeedInterval()
        {
            if (IsFrozen) return;
            if (!MetabolicState.Active) return; // 模块未启用：不消耗、无副作用

            float fall = FallPerDay * 150f / 60000f; // NeedInterval 每 150 tick 一次；60000 tick = 1 天
            CurLevel -= fall;
            if (CurLevel < 0f) CurLevel = 0f;
        }

        /// <summary>外部（进食/饮水逻辑）调用，补充该代谢物。amount 为 0~1 比例。</summary>
        public void Satisfy(float amount)
        {
            CurLevel += amount;
            if (CurLevel > MaxLevel) CurLevel = MaxLevel;
        }

        /// <summary>当前满足度（0~1）。</summary>
        public float Severity => CurLevel / MaxLevel;
    }
}
