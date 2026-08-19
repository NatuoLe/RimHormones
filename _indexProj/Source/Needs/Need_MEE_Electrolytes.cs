using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>电解质需求。消耗较慢，长期失衡影响生理稳态。容量随体魄变化。</summary>
    public class Need_MEE_Electrolytes : Need_MEE_Base
    {
        public Need_MEE_Electrolytes(Pawn pawn) : base(pawn) { }

        /// <summary>电解质容量 = 基准 1.0 × 体魄阶段倍率（虚弱 0.90 ~ 卓越 1.30）。</summary>
        public override float MaxLevel => PhysiqueLgc.GetMEEElectrolytesCapacityMult(pawn);

        protected override float FallPerDay => 0.3f; // 每日约消耗 30%

        protected override void OnModified(float before, float after)
        {
            NeedChangeEvents.FireElectrolytesChanged(pawn, before, after);
        }
    }
}
