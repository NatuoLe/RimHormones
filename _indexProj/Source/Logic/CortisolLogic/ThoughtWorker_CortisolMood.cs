using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 皮质醇心情影响 ThoughtWorker（含体魄修正）。
    /// 由游戏周期性评估（ThoughtHandler 调用 CurrentStateInternal），根据 pawn 的皮质醇严重度档位
    /// 与体魄阶段选中对应 stage，stage 的 baseMoodEffect 即心情加成。
    ///
    /// stage 索引 = tier * 5 + physiqueIdx（physiqueIdx: Frail=0 Average=1 Fit=2 Strong=3 Peak=4）
    /// 心情 = 档位基础 + 体魄偏移（设计表）：
    ///   档位\体魄        frail  average  fit   strong  peak
    ///   0≤S&lt;0.33(+2)   +1     +2       +3    +3      +3
    ///   0.33≤S&lt;0.66(-2) -4     -2       -1    -1      -1
    ///   0.66≤S≤1.0(-5)   -8     -6       -4    -3      -2
    /// </summary>
    public class ThoughtWorker_CortisolMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // 仅对拥有皮质醇 Need 的 pawn 生效（minIntelligence=ToolUser，即类人生物）
            Need_Cortisol need = p.needs?.TryGetNeed<Need_Cortisol>();
            if (need == null)
                return ThoughtState.Inactive;

            float severity = need.GetSeverity();
            int tier = severity < 0.33f ? 0 : (severity < 0.66f ? 1 : 2);

            // 体魄阶段（Frail/Average/Fit/Strong/Peak → 0..4）
            PhysiqueStage phys = PhysiqueLgc.GetPhysiqueStage(p);
            int physIdx = (int)phys;

            int stage = tier * 5 + physIdx;
            return ThoughtState.ActiveAtStage(stage);
        }
    }
}
