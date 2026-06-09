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
            int physiqueLevel = GetPhysiqueLevel();
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

        private int GetPhysiqueLevel()
        {
            if (Pawn == null) return 1;
            SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueSkillDef == null) return 1;
            SkillRecord skill = Pawn.skills?.GetSkill(physiqueSkillDef);
            int level = skill?.levelInt ?? 1;
            return Helpers.Clamp(level, Define.PhysiqueMinLevel, Define.PhysiqueMaxLevel);
        }

        private float GetPhysiqueBonus(int level)
        {
            if (level < Define.PhysiqueNegativeThresholdHigh)
                return Define.PhysiqueLowPenalty;
            if (level <= Define.PhysiqueNegativeThresholdLow)
                return Define.PhysiqueMediumPenalty;
            return 1f + (level - Define.PhysiquePositiveThreshold + 1) * Define.PhysiqueBonusPerLevel;
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
                int physiqueLevel = GetPhysiqueLevel();
                float bonus = GetPhysiqueBonus(physiqueLevel);

                string tip ="";
                //   tip += $"\n\n{"PhysiqueLevel".Translate()}: {physiqueLevel}/20\n";
                //   tip +="测试文本\n";
                // tip += $"{"OverallBonus".Translate()}: {bonus:P1}\n";
                tip += $"{"WorkEfficiency".Translate()}: {HormonesLogic.GetWorkEfficiency(Pawn):P0}\n";
                tip += $"{"HungerRate".Translate()}: {HormonesLogic.GetHungerRate(Pawn):P0}\n";
                tip += $"{"MetabolicRate".Translate()}: {HormonesLogic.GetMetabolicRate(Pawn):P0}\n";
                tip += $"{"Appetite".Translate()}: {HormonesLogic.GetAppetiteMultiplier(Pawn):P0}\n";

                // 激素系统影响
                float recoveryBonus = 1f + (physiqueLevel - 1f) / (Define.PhysiqueMaxLevel - 1) * Define.PhysiqueHormonesRecoveryBonusFactor;
                float damageReduction = 1f - (physiqueLevel - 1f) / (Define.PhysiqueMaxLevel - 1) * Define.PhysiqueHormonesDamageReductionFactor;
                tip += $"\n{"HormoneRecovery".Translate()}: +{(recoveryBonus - 1f):P0}\n";
                tip += $"{"HormoneDamageReduction".Translate()}: {(1f - damageReduction):P0}\n";

                // // 肾上腺素联动
                // if (physiqueLevel < Define.PhysiqueAdrenalinePenaltyThreshold)
                //     tip += $"{"AdrenalinePenalty".Translate()}: -50%\n";
                // else if (physiqueLevel >= Define.PhysiqueAdrenalineExemptionThreshold)
                //     tip += $"{"AdrenalineExempt".Translate()}: {"Yes".Translate()}\n";

                // // 皮质醇联动
                // if (physiqueLevel < Define.PhysiqueCortisolPenaltyThreshold)
                //     tip += $"{"CortisolGrowth".Translate()}: +50%\n";
                // else if (physiqueLevel >= Define.PhysiqueCortisolBonusThreshold)
                //     tip += $"{"CortisolGrowth".Translate()}: -20%\n";

                return tip;
            }
        }
    }
}
