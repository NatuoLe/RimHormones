using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Hormones.Logic.PhysiqueLogic;

namespace Hormones.Jobs
{
    // ================= WorkGiver：扫描锻炼点，派殖民者前去锻炼 =================
    public class WorkGiver_Exercise : WorkGiver_Scanner
    {
        // 只扫描简易锻炼点这一种建筑
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(ThingDef.Named("Hormones_ExerciseSpot"));

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            // 只对类人生物有效（与整套激素系统保持一致）
            if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike) return true;
            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.Destroyed) return false;
            if (t.def.defName != "Hormones_ExerciseSpot") return false;

            // 建筑必须可用、可达、可预约
            if (t.IsForbidden(pawn)) return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced)) return false;
            if (!pawn.CanReach(t, PathEndMode.Touch, pawn.NormalMaxDanger())) return false;
            if (t.IsBurning()) return false;

            // 体力储备（劳损）门槛：低于 35% 太累了，练不动 —— 不派活，
            // 并给出右键/悬停可见的原因（类似研究台不满足条件时的提示）。
            Need_MuscleStrain strain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
            if (strain != null && strain.MaxLevel > 0f
                && strain.CurLevel < strain.MaxLevel * Define.ExerciseMinStrainPct)
            {
                JobFailReason.Is("体力不足，无法锻炼（劳损储备低于 35%）");
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!HasJobOnThing(pawn, t, forced)) return null;
            return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Hormones_DoExercise"), t);
        }
    }

    // ================= JobDriver：在锻炼点停留锻炼，给体魄技能加经验 =================
    public class JobDriver_Exercise : JobDriver
    {
        // 一次锻炼的总时长（tick）。2500 tick ≈ 1 游戏小时
        private const int ExerciseDurationTicks = 5000;

        // 每 tick 给「体魄」技能加的经验（含 60 tick 汇总节流）
        private const float XpPerTick = 0.075f;

        private static SkillDef PhysiqueSkillDef =>
            DefDatabase<SkillDef>.GetNamed("Physique", false);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 预约锻炼点，同一时刻只允许一人使用
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);

            // 走到锻炼点旁边
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 停留锻炼
            Toil exercise = ToilMaker.MakeToil("Exercise");
            exercise.defaultCompleteMode = ToilCompleteMode.Delay;
            exercise.defaultDuration = ExerciseDurationTicks;
            exercise.handlingFacing = true;
            exercise.WithProgressBarToilDelay(TargetIndex.A);

            exercise.tickAction = delegate
            {
                // 面朝锻炼点
                pawn.rotationTracker.FaceTarget(job.targetA);

                // 扣除体力储备（劳损）：锻炼是消耗性的。含体魄消耗倍率——
                // 虚弱者扣得快、强壮者扛得住，与劳作系统一致。
                Need_MuscleStrain strain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
                if (strain != null)
                {
                    float consumeMult = PhysiqueLgc.GetMuscleStrainConsumeMultiplier(pawn);
                    strain.AddStrain(Define.ExerciseStrainPerTick * consumeMult);

                    // 练到体力储备跌破门槛就停下——练不动了，避免把储备耗尽。
                    if (strain.MaxLevel > 0f
                        && strain.CurLevel < strain.MaxLevel * Define.ExerciseMinStrainPct)
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                }

                // 给体魄技能加经验
                SkillDef physique = PhysiqueSkillDef;
                if (physique != null)
                {
                    SkillRecord rec = pawn.skills?.GetSkill(physique);
                    rec?.Learn(XpPerTick, false);
                }

                // 体魄日常衰减：标记“今日已有体力活动（锻炼）”，本周期免于衰减
                pawn.GetComp<HormonesComponent>()?.MarkActivityToday();
            };

            // 结束时随机偶尔飘一次提示（非必需，交给技能升级 mote 自动处理）
            exercise.AddFinishAction(delegate
            {
                // 锻炼完成，无额外处理；体魄经验已在 tick 中累加
            });

            yield return exercise;
        }
    }
}
