using System.Collections.Generic;
using RimWorld;
using Verse;
using Hormones;

namespace MetabolicEssential
{
    /// <summary>
    /// 蛋白质代谢逻辑：把「蛋白质」作为体魄升级的缓冲区。
    ///
    /// 解耦原则（同 MetabolicLogic_Sugar）：只订阅主 mod 的 NeedChangeEvents 公共事件、
    /// 只经主 mod 暴露的公共接口写回，绝不直接读/写主 mod 私有字段。
    ///
    /// 已实现的反馈（对应设计草稿 五、MEE_Protein）：
    ///   1) 体魄升级（OnPhysiqueLevelChanged）时，按升级跨度等比例消耗蛋白质。
    ///      —— 缓冲作用：快速升级（如双火小人）会迅速耗尽蛋白质，从而自然限速。
    ///   2) 蛋白质 &lt; 10% 时，通过主 mod 的 PhysiqueUpgradeGate 门控禁止获得体魄经验
    ///      （= 无法升级体魄），并施加「营养不足」Hediff 作为可见症状；
    ///      蛋白质恢复 ≥10% 后解除门控并移除 Hediff。
    ///
    /// 触发链：
    ///   主 mod 在每次体魄经验获取（SkillRecord.Learn，经 SkillRecord_Learn_Physique_Patch）
    ///   前调用 PhysiqueUpgradeGate(pawn) 咨询是否放行；升级成功后触发 OnPhysiqueLevelChanged，
    ///   本逻辑据此扣减蛋白质。
    /// </summary>
    public static class MetabolicLogic_Protein
    {
        // ===== 设计参数 =====
        /// <summary>每提升 1 级体魄消耗的蛋白质比例（占 MaxLevel 0~1）。草稿未给定值，暂定 15%/级。</summary>
        private const float ProteinCostPerLevel = 0.15f;
        /// <summary>蛋白质满足度阈值：低于此（&lt;10%）禁止体魄升级并施加「营养不足」Hediff。</summary>
        private const float MalnutritionThreshold = 0.10f;
        /// <summary>「营养不足」Hediff 的 defName（见 Defs/HediffDefs/Hediff_MEE_Malnutrition.xml）。</summary>
        private const string MalnutritionHediff = "MEE_Malnutrition";

        /// <summary>模块 Init 时调用：订阅主 mod 的体魄升级事件。</summary>
        public static void Register()
        {
            NeedChangeEvents.OnPhysiqueLevelChanged += OnLevelUp;
        }

        /// <summary>
        /// 体魄升级门控（由主 mod 在 SkillRecord.Learn 前调用）。
        /// 蛋白&lt;10% → 禁止升级 + 施加「营养不足」Hediff；否则放行并解除 Hediff。
        /// </summary>
        public static MetaBolicLoadCtrl.PhysiqueUpgradeGateResult Gate(Pawn pawn)
        {
            if (pawn?.needs == null || !MetaBolicLoadCtrl.Active)
                return new MetaBolicLoadCtrl.PhysiqueUpgradeGateResult(true, 1f);

            Need_MEE_Protein protein = pawn.needs.TryGetNeed<Need_MEE_Protein>();
            if (protein == null)
                return new MetaBolicLoadCtrl.PhysiqueUpgradeGateResult(true, 1f);

            if (protein.Severity < MalnutritionThreshold)
            {
                EnsureMalnutrition(pawn);
                return new MetaBolicLoadCtrl.PhysiqueUpgradeGateResult(false, 1f); // 无法升级体魄
            }

            RemoveMalnutrition(pawn);
            return new MetaBolicLoadCtrl.PhysiqueUpgradeGateResult(true, 1f);
        }

        /// <summary>体魄升级时按跨度消耗蛋白质缓冲。</summary>
        private static void OnLevelUp(Pawn pawn, int oldLevel, int newLevel)
        {
            if (pawn == null || newLevel <= oldLevel) return;

            Need_MEE_Protein protein = pawn.needs?.TryGetNeed<Need_MEE_Protein>();
            if (protein == null) return;

            int gained = newLevel - oldLevel;
            protein.Consume(ProteinCostPerLevel * gained);
        }

        private static void EnsureMalnutrition(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(MalnutritionHediff, false);
            if (def == null || pawn.health?.hediffSet == null) return;
            if (!pawn.health.hediffSet.HasHediff(def))
                pawn.health.AddHediff(def);
        }

        private static void RemoveMalnutrition(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(MalnutritionHediff, false);
            if (def == null || pawn.health?.hediffSet == null) return;
            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (h != null) pawn.health.RemoveHediff(h);
        }
    }
}
