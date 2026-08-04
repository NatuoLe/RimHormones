using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Hormones.Logic.PhysiqueLogic
{
    // ============================================================
    // 体魄工作结算 —— 共享逻辑
    //   A 方案：按工作时长累计结算（由 HormonesComponent.CompTick 每 400 tick 驱动一次）
    //   本类只负责“给定一份工作参数 → 结算一次经验/劳损/拉伤”，供 tick 与 Job 完成补算复用。
    // ============================================================
    public static class PhysiqueWorkSettle
    {
        /// <summary>
        /// 判断某 jobDef 是否属于体魄工作白名单，并取出该工作“每结算周期”的经验/劳损/拉伤概率。
        /// 返回 false 表示该工作不参与体魄系统。
        /// </summary>
        public static bool TryGetWorkParams(string jobDefName, Job curJob, out float xp, out float strain, out float chance)
        {
            xp = 0f; strain = 0f; chance = 0f;
            if (string.IsNullOrEmpty(jobDefName)) return false;

            switch (jobDefName)
            {
                case "Mine":
                    xp = Define.MiningXP; strain = Define.MiningMuscleStrain; chance = Define.MiningStrainChance; return true;

                case "CutPlant":
                case "CutPlantDesignated":
                    {
                        Thing t = curJob?.GetTarget(TargetIndex.A).Thing;
                        bool isTree = t != null && t.def.plant != null && t.def.plant.IsTree;
                        if (isTree) { xp = Define.TreeCutXP; strain = Define.TreeCutMuscleStrain; chance = Define.TreeCutStrainChance; }
                        else { xp = Define.PlantCutXP; strain = Define.PlantCutMuscleStrain; chance = Define.PlantCutStrainChance; }
                        return true;
                    }

                case "Harvest":
                case "HarvestDesignated":
                    xp = Define.HarvestXP; strain = Define.HarvestMuscleStrain; chance = Define.HarvestStrainChance; return true;

                case "Slaughter":
                    // 修复：原先 case Slaughter 未赋 strainChance，导致宰杀永不触发拉伤
                    xp = Define.ButcherXP; strain = Define.ButcherMuscleStrain; chance = Define.ButcherStrainChance; return true;

                case "HaulToCell":
                case "HaulToContainer":
                case "HaulToStorage":
                case "HaulToCaravan":
                case "HaulToTransporter":
                    xp = Define.HaulXP; strain = Define.HaulMuscleStrain; chance = Define.HaulStrainChance; return true;

                // B: 新增工作类型（建造/拆除/种植）
                case "FinishFrame":
                    xp = Define.FinishFrameXP; strain = Define.FinishFrameMuscleStrain; chance = Define.FinishFrameStrainChance; return true;

                case "Deconstruct":
                case "DeconstructForBlueprint":
                    xp = Define.DeconstructXP; strain = Define.DeconstructMuscleStrain; chance = Define.DeconstructStrainChance; return true;

                case "Sow":
                    xp = Define.SowXP; strain = Define.SowMuscleStrain; chance = Define.SowStrainChance; return true;

                // 【2026-07-26 补齐】其它体力工作
                case "ExtractTree":
                    xp = Define.ExtractTreeXP; strain = Define.ExtractTreeMuscleStrain; chance = Define.ExtractTreeStrainChance; return true;

                case "OperateDeepDrill":
                    xp = Define.DeepDrillXP; strain = Define.DeepDrillMuscleStrain; chance = Define.DeepDrillStrainChance; return true;

                case "Uninstall":
                    xp = Define.UninstallXP; strain = Define.UninstallMuscleStrain; chance = Define.UninstallStrainChance; return true;

                case "SmoothFloor":
                case "SmoothWall":
                    xp = Define.SmoothXP; strain = Define.SmoothMuscleStrain; chance = Define.SmoothStrainChance; return true;

                case "Hunt":
                    xp = Define.HuntXP; strain = Define.HuntMuscleStrain; chance = Define.HuntStrainChance; return true;

                case "Repair":
                case "FixBrokenDownBuilding":
                    xp = Define.RepairXP; strain = Define.RepairMuscleStrain; chance = Define.RepairStrainChance; return true;

                case "Replant":
                case "PlantSeed":
                    xp = Define.ReplantXP; strain = Define.ReplantMuscleStrain; chance = Define.ReplantStrainChance; return true;
            }
            return false;
        }

        /// <summary>
        /// 判断某工作是否属于“移动型劳作”——即移动本身就是工作的一部分，
        /// 不能用“正在移动=赶路”来排除。
        ///   · 搬运（Haul*）：扛着东西走本来就是体力活
        /// 其余工作（挖矿/砍树/种植/建造/拆除/平整/修理/深钻/宰杀/收割/打猎等）都是
        /// “走到目标 → 站定施工/开火”，因此移动阶段视为赶路、不累计劳损。
        ///   · 打猎（Hunt）：移动接近 / 追击（“打野”）视为赶路，不累计；
        ///     只在站定开火 / 近战攻击那一刻才结算。
        /// </summary>
        public static bool IsMobileWork(string jobDefName)
        {
            switch (jobDefName)
            {
                case "HaulToCell":
                case "HaulToContainer":
                case "HaulToStorage":
                case "HaulToCaravan":
                case "HaulToTransporter":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断是否为搬运工作
        /// </summary>
        public static bool IsHaulJob(string jobDefName)
        {
            switch (jobDefName)
            {
                case "HaulToCell":
                case "HaulToContainer":
                case "HaulToStorage":
                case "HaulToCaravan":
                case "HaulToTransporter":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取搬运的物品
        /// </summary>
        private static Thing GetHauledThing(Job job)
        {
            if (job == null) return null;
            // 搬运物品通常在 targetA
            if (job.targetA.HasThing) return job.targetA.Thing;
            // 也可能在 targetB
            if (job.targetB.HasThing) return job.targetB.Thing;
            return null;
        }

        /// <summary>
        /// 结算一次工作产出：加体魄经验、扣劳损值、按玩家设置 roll 拉伤 Hediff。
        /// factor: 结算比例（1.0 = 一个完整周期；Job 完成补算时按余数/周期给不足 1 的值）。
        /// </summary>
        public static void SettleWork(Pawn pawn, float xp, float strain, float chance, Job curJob, float factor = 1f)
        {
            if (pawn == null || factor <= 0f) return;
            if (xp <= 0f) return;

            // 体魄日常衰减：标记“今日已有体力劳作”，本周期免于衰减（用进废退）
            pawn.GetComp<HormonesComponent>()?.MarkActivityToday();

            // 1) 体魄经验：基础经验 × 结算比例 × 额外乘区（2026-08-04 实现）
            //    额外乘区存于 HediffComp_PhysiqueDisplay，由外部（如饮品 Buff）经
            //    PhysiqueXpMultUtility 修改；默认 1.0 不影响原经验。
            SkillDef physiqueDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueDef != null && pawn.skills != null)
            {
                float finalXp = xp * factor * PhysiqueXpMultUtility.GetExtraXpMult(pawn);
                pawn.skills.Learn(physiqueDef, finalXp);
            }

            // 2) 劳损值扣减 + 拉伤 roll
            Need_MuscleStrain muscleStrain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
            if (muscleStrain == null)
            {
                return;
            }

            // 阶段扣减倍率：虚弱扣得快、强壮扛得住
            float consumeMult = PhysiqueLgc.GetMuscleStrainConsumeMultiplier(pawn);

            // 搬运：根据物品重量增加劳损
            float weightMult = 1f;
            if (IsHaulJob(curJob.def.defName))
            {
                Thing hauledThing = GetHauledThing(curJob);
                if (hauledThing != null)
                {
                    float weight = hauledThing.GetStatValue(StatDefOf.Mass);
                    // 重量修正：每 5kg 增加 20% 劳损，基准 25kg = 1.0
                    weightMult = 1f + (weight - 25f) / 25f * 0.5f;
                    if (weightMult < 0.5f) weightMult = 0.5f; // 最低 50%
                    if (weightMult > 3f) weightMult = 3f; // 最高 300%
                }
            }

            float finalStrain = strain * factor * consumeMult * weightMult;

            muscleStrain.AddStrain(finalStrain);

            float thresholdPct = RimHormonesMod.Settings != null
                ? RimHormonesMod.Settings.StrainTriggerThresholdPct
                : Define.DefaultStrainTriggerThresholdPct;
            float chanceMult = RimHormonesMod.Settings != null
                ? RimHormonesMod.Settings.StrainChanceMultiplier
                : Define.DefaultStrainChanceMultiplier;

            float finalChance = chance * factor
                * PhysiqueLgc.GetMuscleStrainChanceMultiplier(pawn)
                * chanceMult;

            bool canTrigger = muscleStrain.CurLevel < muscleStrain.MaxLevel * thresholdPct;
            bool triggered = canTrigger && Rand.Value < finalChance;

            if (RimHormonesMod.Settings != null && RimHormonesMod.Settings.ShowPhysiqueMotes
                && pawn.Map != null && pawn.Position.IsValid)
            {
                ShowMuscleStrainText(pawn.Position, pawn.Map, muscleStrain, finalStrain, finalChance, canTrigger, triggered, curJob);
            }

            if (triggered)
            {
                MuscleStrainUtility.TryAddMuscleStrain(pawn);
            }
        }

        private static void ShowMuscleStrainText(IntVec3 pos, Map map, Need_MuscleStrain muscleStrain, float strainAmount, float finalChance, bool canTrigger, bool triggered, Job curJob)
        {
            MoteText moteText = (MoteText)ThingMaker.MakeThing(ThingDefOf.Mote_Text);
            object vector3 = PhysiqueDatas.GetVector3(pos.x + 0.5f, 0.5f, pos.z + 0.5f);
            System.Reflection.FieldInfo field = typeof(MoteText).GetField("exactPosition");
            if (field == null) return;

            field.SetValue(moteText, vector3);
            moteText.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));

            float strainPercent = (muscleStrain.CurLevel / muscleStrain.MaxLevel) * 100f;
            string status = canTrigger
                ? (triggered ? "肌肉拉伤!" : $"尝试拉伤 {finalChance * 100:F1}%")
                : "体力充足";

            string targetName = "";
            Thing targetThing = curJob?.GetTarget(TargetIndex.A).Thing;
            if (targetThing != null)
            {
                targetName = $"[{targetThing.LabelShort}] ";
            }

            moteText.text = $"{targetName}劳损: {strainPercent:F0}% ({muscleStrain.CurLevel:F0}/{muscleStrain.MaxLevel:F0}) | -{strainAmount:F0} | {status}";
            GenSpawn.Spawn(moteText, pos, map);
            PhysiqueDatas.ReturnVector3(vector3);
        }
    }

    // ============================================================
    // 【2026-07-26 修复后语义】Job 结束时不再做"余数补算清零"。
    //   累计器 workTickAccumulator 跨 Job 持续保留，唯一结算路径是
    //   HormonesComponent.PhysiqueWorkTick 的"满 400 大结算"。
    //   这样种植/割除等碎片化短 Job（一格一个 Job、频繁 Succeeded）也能正确累积劳损。
    //   此 patch 保留仅作为将来"切到无关工作时衰减余数"的扩展点，目前为空操作。
    // ============================================================
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class Patch_Job_End_PhysiqueXP
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_JobTracker __instance, JobCondition condition)
        {
            Pawn pawn = GetPawn(__instance);
            if (pawn == null) return;

            HormonesComponent comp = pawn.GetComp<HormonesComponent>();
            if (comp == null) return;

            // 累计器跨 Job 保留（不清零）。见 SettleWorkRemainder 注释。
            comp.SettleWorkRemainder();
        }

        private static Pawn GetPawn(Pawn_JobTracker jobTracker)
        {
            var field = typeof(Pawn_JobTracker).GetField("pawn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(jobTracker) as Pawn;
        }
    }
}
