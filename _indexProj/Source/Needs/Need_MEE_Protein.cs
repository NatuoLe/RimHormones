using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>蛋白质需求。消耗最慢，代表长期组织修复/合成的原料缺口。容量随体魄变化。</summary>
    public class Need_MEE_Protein : Need_MEE_Base
    {
        public Need_MEE_Protein(Pawn pawn) : base(pawn) { }

        /// <summary>蛋白质容量 = 基准 1.0 × 体魄阶段倍率（虚弱 0.90 ~ 卓越 1.30）。</summary>
        public override float MaxLevel => PhysiqueLgc.GetMEEProteinCapacityMult(pawn);

        protected override float FallPerDay => 0.15f; // 每日约消耗 15%

        protected override void OnModified(float before, float after)
        {
            NeedChangeEvents.FireProteinChanged(pawn, before, after);
        }
    }
}
