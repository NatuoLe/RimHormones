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

        // ============================================================
        // 【额外经验乘区 2026-08-04】
        //   每个小人独立的体魄经验乘区（默认 1.0），算入最终经验：
        //     finalXp = xp * factor * PhysiqueExtraXpMult
        //   由外部系统修改（如饮品 Buff 的 comp：进入时 Set/Multiply，退出时还原），
        //   随存档持久化。消费点：PhysiqueWorkSettle.SettleWork（劳作结算）。
        //   外部请优先用同文件底部的静态工具类 PhysiqueXpMultUtility，
        //   不要直接满世界找这个 comp。
        // ============================================================
        private float _physiqueExtraMultiAea = 1f;

        /// <summary>
        /// 体魄经验额外乘区（get/set 预留给外部，如饮品系统）。
        /// 一律用**绝对设置**（set），不要用增量乘/除——浮点 ×N ÷N 多次进出会累积误差。
        /// set 时下限保护为 0（0 = 完全不给经验）。
        /// </summary>
        public float PhysiqueExtraXpMult
        {
            get => _physiqueExtraMultiAea;
            set => _physiqueExtraMultiAea = value < 0f ? 0f : value;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref _physiqueExtraMultiAea, "physiqueExtraXpMult", 1f);
        }
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

                // 额外经验乘区（仅在外部修改过、非默认 1.0 时显示，便于确认饮品等来源生效）
                if (System.Math.Abs(_physiqueExtraMultiAea - 1f) > 0.001f)
                {
                    tip += $"体魄经验加成: ×{_physiqueExtraMultiAea:F2}\n";
                }

                return tip;
            }
        }
    }

    /// <summary>
    /// 体魄经验额外乘区的外部访问入口（2026-08-04 新增，预留给饮品等外部系统）。
    /// 统一封装「找 PhysiqueBodyCondition hediff → 取 HediffComp_PhysiqueDisplay」，
    /// 外部不必自己翻 comp。三种用法：
    ///   GetExtraXpMult(pawn)                 读当前乘区（默认 1.0）
    ///   SetExtraXpMult(pawn, 1.3f)           绝对设置（Buff 存在期间设为 X）
    ///   ResetExtraXpMult(pawn)               还原为 1.0（Buff 消失时调）
    ///
    /// 设计约定（用户定）：一律**绝对设置**，不提供增量乘/除接口——
    /// 浮点 ×N ÷N 多次进出会累积误差，Set 绝对值无此问题。
    /// 多 Buff 场景为「后者覆盖」语义（后 Set 的生效）；各 Buff 退出时各自
    /// 调用 Reset 即可。若未来需要多 Buff 精确叠加，再改为存来源列表重算。
    /// </summary>
    public static class PhysiqueXpMultUtility
    {
        private static HediffDef physiqueDisplayDefCache;

        private static HediffComp_PhysiqueDisplay GetComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            if (physiqueDisplayDefCache == null)
                physiqueDisplayDefCache = DefDatabase<HediffDef>.GetNamedSilentFail("PhysiqueBodyCondition");
            if (physiqueDisplayDefCache == null) return null;
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(physiqueDisplayDefCache);
            return h?.TryGetComp<HediffComp_PhysiqueDisplay>();
        }

        /// <summary>读当前额外乘区（无 comp 时按 1.0）。</summary>
        public static float GetExtraXpMult(Pawn pawn)
        {
            HediffComp_PhysiqueDisplay comp = GetComp(pawn);
            return comp != null ? comp.PhysiqueExtraXpMult : 1f;
        }

        /// <summary>绝对设置乘区（下限 0）。Buff 存在期间调用一次即可。</summary>
        public static void SetExtraXpMult(Pawn pawn, float value)
        {
            HediffComp_PhysiqueDisplay comp = GetComp(pawn);
            if (comp != null) comp.PhysiqueExtraXpMult = value;
        }

        /// <summary>还原为 1.0（Buff 消失时调用）。</summary>
        public static void ResetExtraXpMult(Pawn pawn)
        {
            HediffComp_PhysiqueDisplay comp = GetComp(pawn);
            if (comp != null) comp.PhysiqueExtraXpMult = 1f;
        }
    }
}
