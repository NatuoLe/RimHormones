using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>蛋白质需求。消耗最慢，代表长期组织修复/合成的原料缺口。</summary>
    public class Need_MEE_Protein : Need_MEE_Base
    {
        public Need_MEE_Protein(Pawn pawn) : base(pawn) { }

        protected override float FallPerDay => 0.15f; // 每日约消耗 15%
    }
}
