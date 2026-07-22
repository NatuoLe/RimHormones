using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 神经衰弱心情影响 ThoughtWorker（含体魄修正）。
    /// 若 pawn 带有 CortisolNeurasthenia hediff，则按体魄阶段给出心情，stage 索引 = physiqueIdx
    /// （Frail=0 Average=1 Fit=2 Strong=3 Peak=4）。
    /// 心情 = 基础 -5 + 体魄偏移（设计表）：
    ///   frail  average  fit   strong  peak
    ///   -7     -5       -4    -2      -1
    /// </summary>
    public class ThoughtWorker_NeurastheniaMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.health?.hediffSet == null)
                return ThoughtState.Inactive;

            HediffDef def = DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false);
            if (def == null || !p.health.hediffSet.HasHediff(def))
                return ThoughtState.Inactive;

            PhysiqueStage phys = PhysiqueLgc.GetPhysiqueStage(p);
            return ThoughtState.ActiveAtStage((int)phys);
        }
    }
}
