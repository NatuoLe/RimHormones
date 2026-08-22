using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>糖分需求。中等消耗，代表能量摄入缺口。</summary>
    public class Need_MEE_Sugar : Need_MEE_Base
    {
        public Need_MEE_Sugar(Pawn pawn) : base(pawn) { }

        /// <summary>糖分容量随体魄缩放：仅高体魄增益（虚弱/一般不惩罚），增强高体魄小人的能量缓冲抗风险能力。</summary>
        public override float MaxLevel => PhysiqueLgc.GetMEESugarCapacityMult(pawn);

        protected override float BaseFallPerDay => 0.2f; // 每日自然掉落约 20%（可由外部模块在皮质醇高时下调、劳损时再额外上调，叠加在 extraFallPerDay 上）

        /// <summary>糖变化后触发变化事件；外部模块（如 Metabolic Essential 糖逻辑）据此联动皮质醇。</summary>
        protected override void OnModified(float before, float after)
            => NeedChangeEvents.FireSugarChanged(pawn, before, after);
    }
}
