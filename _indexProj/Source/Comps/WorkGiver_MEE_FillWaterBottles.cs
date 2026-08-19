using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using Hormones;

namespace Hormones
{
    /// <summary>
    /// MEE 需求驱动式「取水供配方」WorkGiver（仿 FDE / DBH 的 WorkGiver_FillWaterBottles）：
    /// 当地图上存在需要水（MEE_RawWater / MEE_WaterBottle）作原料的活动配方账单、且地面库存不足以满足时，
    /// 自动派小人到最近的 DBH 取水设施（带 CompPipe：水槽/浴缸/水池），执行 DBH 的 DBHStockpileWaterBottles Job。
    /// 该 Job 由 MetabolicEssential 模块的 WaterAdapter 重定向为「从管网真实扣水 → 逐瓶产 MEE_WaterBottle」，
    /// 产出的 MEE_WaterBottle 随后由 WorkGiver_DoBill 自动搬运到配方台，喂饱炉灶等需要水的账单。
    ///
    /// 门控：
    ///  - MetaBolicLoadCtrl.Active 为 false（MEE 模块未启用）时整体跳过；
    ///  - 地图上无 DBHStockpileWaterBottles JobDef（即未装 DBH）时跳过——MEE 在没有管网设施时无法按需产水。
    /// 零硬引用 DBH：设施类型与 JobDef 全部按名反射/查库，DBH 缺席时静默不生效。
    /// </summary>
    public class WorkGiver_MEE_FillWaterBottles : WorkGiver_Scanner
    {
        /// <summary>单次派工上限，避免单人搬运过量（对齐 DBH 右键档位，并凑整为合理批次）。</summary>
        private const int MaxPerJob = 20;

        /// <summary>“水”物品集合：配方消费与地面库存统计都视作同一种需求。</summary>
        private static readonly HashSet<string> MEEWaterDefs = new HashSet<string>
        {
            "MEE_RawWater",
            "MEE_WaterBottle",
        };

        private static Type _dbhCompPipeType;
        private static bool _dbhTypesResolved;
        private static void ResolveDBHTypes()
        {
            if (_dbhTypesResolved) return;
            _dbhTypesResolved = true;
            _dbhCompPipeType = AccessTools.TypeByName("DubsBadHygiene.CompPipe");
        }

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            if (pawn?.Map == null || !pawn.RaceProps.Humanlike) return true;
            // 无 DBH（无 DBHStockpileWaterBottles JobDef）则无法从管网产水，整体下线
            if (DefDatabase<JobDef>.GetNamedSilentFail("DBHStockpileWaterBottles") == null) return true;
            // 需求驱动：仅当地图上有“需要水且尚未满足”的配方账单时才触发
            return !HasUnmetWaterDemand(pawn.Map);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.Destroyed) return false;
            ResolveDBHTypes();
            // 仅认带 CompPipe 的 DBH 取水设施（有管网连接才能抽水解渴/灌瓶）。
            // 用 AllComps 遍历（同 WaterAdapter），避免依赖 Thing.GetComp(Type) 在本版本是否可用。
            bool hasPipe = false;
            if (_dbhCompPipeType != null && t is ThingWithComps twc)
            {
                foreach (var c in twc.AllComps)
                {
                    if (c != null && _dbhCompPipeType.IsAssignableFrom(c.GetType())) { hasPipe = true; break; }
                }
            }
            if (!hasPipe) return false;
            if (t.IsForbidden(pawn) || t.IsBurning()) return false;
            if (!pawn.CanReserve(t, 2)) return false;
            if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly)) return false;
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            JobDef fillJob = DefDatabase<JobDef>.GetNamedSilentFail("DBHStockpileWaterBottles");
            if (fillJob == null) return null;
            int need = Math.Min(MaxPerJob, DemandShortfall(pawn.Map));
            if (need <= 0) return null;
            Job job = JobMaker.MakeJob(fillJob, t);
            job.count = need; // 让模块侧 BuildMEEWaterFillToils 逐瓶产出这么多瓶 MEE_WaterBottle
            return job;
        }

        // —— 需求扫描（仿 FDE WorkGiver_FillWaterBottles） ——

        private static bool HasUnmetWaterDemand(Map map)
        {
            return DemandShortfall(map) > 0;
        }

        /// <summary>还差多少瓶水才能满足所有活动水账单（地面自由库存之外）。&lt;=0 表示库存已充足。</summary>
        private static int DemandShortfall(Map map)
        {
            if (map == null) return 0;

            // 地图现有水物品地面自由库存（未禁用、未装入容器/背包）
            int available = 0;
            foreach (string defName in MEEWaterDefs)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null) continue;
                List<Thing> things = map.listerThings.ThingsOfDef(def);
                if (things == null) continue;
                foreach (Thing thing in things)
                {
                    if (thing.Spawned && thing.ParentHolder is Map)
                        available += thing.stackCount;
                }
            }

            // 累计所有活动账单对水物品的需求
            int wanted = 0;
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                IBillGiver giver = building as IBillGiver;
                if (giver == null) continue;
                foreach (Bill bill in giver.BillStack)
                {
                    Bill_Production bp = bill as Bill_Production;
                    if (bp == null || !bp.ShouldDoNow()) continue;
                    if (bp.recipe == null || bp.recipe.ingredients == null) continue;

                    int perBatch = 0;
                    foreach (IngredientCount ing in bp.recipe.ingredients)
                    {
                        if (ing.filter == null) continue;
                        foreach (string defName in MEEWaterDefs)
                        {
                            ThingDef wd = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                            if (wd == null || !ing.filter.Allows(wd)) continue;
                            perBatch += ing.CountRequiredOfFor(wd, bp.recipe, bp);
                        }
                    }
                    if (perBatch <= 0) continue;

                    wanted += perBatch * RemainingIterations(bp);
                }
            }

            return wanted - available;
        }

        /// <summary>账单还剩多少次制作迭代（把“单次需求”放大成“总需求”）。
        /// Forever 视为开放需求、按一批计；RepeatCount 取剩余次数；TargetCount 取还差多少件产品。</summary>
        private static int RemainingIterations(Bill_Production bp)
        {
            if (bp.repeatMode == BillRepeatModeDefOf.Forever) return 1;
            if (bp.repeatMode == BillRepeatModeDefOf.RepeatCount) return Math.Max(1, bp.repeatCount);
            int produced = bp.recipe.WorkerCounter.CountProducts(bp);
            return Math.Max(0, bp.targetCount - produced);
        }
    }
}
