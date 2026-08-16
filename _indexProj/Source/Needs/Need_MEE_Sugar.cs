using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>糖分需求。中等消耗，代表能量摄入缺口。</summary>
    public class Need_MEE_Sugar : Need_MEE_Base
    {
        public Need_MEE_Sugar(Pawn pawn) : base(pawn) { }

        protected override float FallPerDay => 0.4f + extraFallPerDay; // 每日自然掉落约 40%（可由外部模块在皮质醇高时下调、劳损时再额外上调）

        /// <summary>糖变化后触发变化事件；外部模块（如 Metabolic Essential 糖逻辑）据此联动皮质醇。</summary>
        protected override void OnModified(float before, float after)
            => NeedChangeEvents.FireSugarChanged(pawn, before, after);
    }
}
