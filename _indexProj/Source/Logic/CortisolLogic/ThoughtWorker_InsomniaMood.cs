using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 失眠心情影响 ThoughtWorker。
    /// 若 pawn 带有 CortisolInsomnia hediff（blocksSleeping 的失眠状态），则激活一个固定 -1 的心情惩罚。
    /// 失眠 Hediff 自身会在 disappearsAfterTicks(5000) 后消失，心情惩罚随之自动解除。
    /// </summary>
    public class ThoughtWorker_InsomniaMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.health?.hediffSet == null)
                return ThoughtState.Inactive;

            HediffDef def = DefDatabase<HediffDef>.GetNamed("CortisolInsomnia", false);
            if (def == null || !p.health.hediffSet.HasHediff(def))
                return ThoughtState.Inactive;

            // 单档：固定 -1 心情
            return ThoughtState.ActiveAtStage(0);
        }
    }
}
