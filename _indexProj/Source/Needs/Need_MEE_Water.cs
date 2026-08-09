using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>水分需求。消耗较快，代表日常饮水缺口。</summary>
    public class Need_MEE_Water : Need_MEE_Base
    {
        public Need_MEE_Water(Pawn pawn) : base(pawn) { }

        protected override float FallPerDay => 0.6f; // 每日约消耗 60%
    }
}
