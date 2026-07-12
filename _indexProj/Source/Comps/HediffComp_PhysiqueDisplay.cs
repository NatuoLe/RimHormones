using RimWorld;
using Verse;
using System.Collections.Generic;

namespace Hormones
{
    public class HediffCompProperties_PhysiqueDisplay : HediffCompProperties
    {
        public HediffCompProperties_PhysiqueDisplay()
        {
            compClass = typeof(HediffComp_PhysiqueDisplay);
        }
    }

    public class HediffComp_PhysiqueDisplay : HediffComp
    {
        private int lastPhysiqueLevel = -1;
        private float lastSeverity = -1f;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 每 60 ticks (约 1 秒) 更新一次
            if (Pawn != null && Pawn.IsHashIntervalTick(60))
            {
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            int physiqueLevel = PhysiqueLgc.GetPhysiqueLevel(Pawn);
            // Severity 必须 > 0，否则 Hediff 会被自动移除
            float targetSeverity = System.Math.Max(0.01f, physiqueLevel / 20f);

            // 只有变化时才更新
            if (physiqueLevel != lastPhysiqueLevel || System.Math.Abs(parent.Severity - targetSeverity) > 0.01f)
            {
                parent.Severity = targetSeverity;
                lastPhysiqueLevel = physiqueLevel;
                lastSeverity = targetSeverity;

                // 通知健康系统更新显示
                Pawn.health.Notify_HediffChanged(parent);
            }
        }

        // 在 Hediff 标签括号中显示体魄等级
        // public override string CompLabelInBracketsExtra
        // {
        //     get
        //     {
        //         int physiqueLevel = GetPhysiqueLevel();
        //         return $"Lv.{physiqueLevel}";
        //     }
        // }

        // 在 Tooltip 中显示详细数据
        public override string CompTipStringExtra
        {
            get
            {
                int physiqueLevel = PhysiqueLgc.GetPhysiqueLevel(Pawn);

                string tip ="";
                tip += $"{"WorkEfficiency".Translate()}: {PhysiqueLgc.GetWorkEfficiency(Pawn):P0}\n";
                tip += $"{"HungerRate".Translate()}: {PhysiqueLgc.GetHungerRate(Pawn):P0}\n";
                tip += $"{"MetabolicRate".Translate()}: {PhysiqueLgc.GetMetabolicRate(Pawn):P0}\n";
                tip += $"{"Appetite".Translate()}: {PhysiqueLgc.GetAppetiteMultiplier(Pawn):P0}\n";

                float recoveryBonus = PhysiqueLgc.GetRecoveryBonus(Pawn);
                float damageReduction = PhysiqueLgc.GetDamageReductionFactor(Pawn);
                tip += $"\n{"HormoneRecovery".Translate()}: +{(recoveryBonus - 1f):P0}\n";
                tip += $"{"HormoneDamageReduction".Translate()}: {(1f - damageReduction):P0}\n";

                return tip;
            }
        }
    }
}
