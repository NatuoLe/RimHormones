using RimWorld;
using Verse;
using Hormones;

namespace Hormones
{
    /// <summary>
    /// 代谢补剂消费组件（模式 B：补剂非饭）。
    /// 殖民者“摄入(Ingest)”带有本组件的物品时，调用对应代谢需求（Need_MEE_*）的
    /// Satisfy(amount) 补充该代谢物。
    ///
    /// 设计约定：
    ///  - 物品 XML 中 nutrition=0，不计入正常饮食饱食度（不是饭）；
    ///  - 仅“摄入”动作触发 PostIngested 才补充，单次回满比例由 satisfyFraction 控制（暂定 0.5 = 50%）；
    ///  - 代谢扩展模块未启用（MetaBolicLoadCtrl.Active=false）时需求不消耗、保持满值，摄入补剂零副作用（直接跳过）。
    /// </summary>
    public class CompProperties_MEE_Satisfier : CompProperties
    {
        /// <summary>被满足的代谢需求 NeedDef（MEEWater / MEESugar / MEEElectrolytes / MEEProtein）。</summary>
        public NeedDef needDef;

        /// <summary>单份回复量占该需求 MaxLevel 的比例（0~1）。暂定 0.5（50%），后续可按物品微调到 30%~50%。</summary>
        public float satisfyFraction = 0.5f;

        /// <summary>饮用时施加的限时 Buff Hediff（defName）。例如 12h 的心情/倍率 Buff。null = 不施加。</summary>
        public string applyBuffHediff = null;

        /// <summary>饮用时立即改变的肌肉劳损值（占 MaxLevel 的比例，0~1）。负值=降低劳损（如功能饮品 -0.25）。0 = 不改变。</summary>
        public float immediateStrainDelta = 0f;

        /// <summary>饮用时是否视为一次锻炼（标记今日已有体力劳作，体魄免于每日衰减）。</summary>
        public bool markExercise = false;

        /// <summary>饮用后致病概率（0~1）。0 = 不致病。用于生水。</summary>
        public float sicknessChance = 0f;

        /// <summary>致病时施加的 Hediff（defName），如 FoodPoisoning。sicknessChance>0 时生效。</summary>
        public string sicknessHediff = null;

        public CompProperties_MEE_Satisfier()
        {
            compClass = typeof(Comp_MEE_Satisfier);
        }
    }

    public class Comp_MEE_Satisfier : ThingComp
    {
        public CompProperties_MEE_Satisfier Props => (CompProperties_MEE_Satisfier)props;

        public override void PostIngested(Pawn ingester)
        {
            base.PostIngested(ingester);

            if (ingester == null || ingester.needs == null)
                return;

            // 模块未启用：代谢需求不消耗、保持满值，摄入补剂无副作用（直接跳过）。
            if (!MetaBolicLoadCtrl.Active)
                return;

            Need need = (Props.needDef != null) ? ingester.needs.TryGetNeed(Props.needDef) : null;
            if (need is Need_MEE_Base mee)
            {
                // 饮水（补充 Water）前先广播「饮用前满足度」，供模块检测溢出→水中毒。
                if (Props.needDef.defName == "MEEWater")
                    NeedChangeEvents.FireDrinkMEEWater(ingester, mee.CurLevel);
                // 摄入葡萄糖原浆（补充 Sugar）时广播，供模块施加「吃了糖」心情 Buff。
                else if (Props.needDef.defName == "MEESugar")
                    NeedChangeEvents.FireSugarEaten(ingester, mee.CurLevel);

                // satisfyFraction 为占 MaxLevel 的比例；MEE 需求 MaxLevel=1，故 0.5 即回 50%。
                mee.Satisfy(Props.satisfyFraction);
            }

            // ===== 可选附加效果（按物品配置） =====

            // 1) 致病（生水）：按概率施加 sicknessHediff
            if (Props.sicknessChance > 0f && !Props.sicknessHediff.NullOrEmpty() && Rand.Value < Props.sicknessChance)
            {
                HediffDef sd = DefDatabase<HediffDef>.GetNamedSilentFail(Props.sicknessHediff);
                if (sd != null && ingester.health?.hediffSet != null && !ingester.health.hediffSet.HasHediff(sd))
                    ingester.health.AddHediff(sd);
            }

            // 2) 视为锻炼（功能饮品）：标记今日体力劳作，体魄免于每日衰减
            if (Props.markExercise)
                ingester.GetComp<HormonesComponent>()?.MarkActivityToday();

            // 3) 即时改变劳损（功能饮品 -25% 等）：负值降低、正值增加
            if (Props.immediateStrainDelta != 0f)
            {
                Need_MuscleStrain ms = ingester.needs?.TryGetNeed<Need_MuscleStrain>();
                if (ms != null)
                {
                    if (Props.immediateStrainDelta < 0f) ms.ReduceStrain(-Props.immediateStrainDelta);
                    else ms.AddStrain(Props.immediateStrainDelta);
                }
            }

            // 4) 施加限时 Buff Hediff（心情/倍率，由 HediffComp_MEEBuff 经解耦接口生效）
            if (!Props.applyBuffHediff.NullOrEmpty())
            {
                HediffDef bd = DefDatabase<HediffDef>.GetNamedSilentFail(Props.applyBuffHediff);
                if (bd != null && ingester.health?.hediffSet != null && !ingester.health.hediffSet.HasHediff(bd))
                    ingester.health.AddHediff(bd);
            }
        }
    }
}
