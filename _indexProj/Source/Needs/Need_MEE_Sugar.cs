using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>糖分需求。中等消耗，代表能量摄入缺口。</summary>
    public class Need_MEE_Sugar : Need_MEE_Base
    {
        public Need_MEE_Sugar(Pawn pawn) : base(pawn) { }

        protected override float FallPerDay => 0.4f; // 每日约消耗 40%
    }
}
