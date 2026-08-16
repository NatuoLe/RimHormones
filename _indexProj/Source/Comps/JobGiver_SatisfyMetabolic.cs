using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using Hormones;

namespace Hormones
{
    /// <summary>
    /// 代谢需求自动摄入驱动器（类比口渴自动喝水）。
    /// RimWorld 1.6 中 JobGiver（如 JobGiver_Ingest）直接继承 ThinkNode_JobGiver，本类同理。
    /// 当某个 MEE 代谢需求低于阈值、且殖民者能拿到对应补剂时，自动派发一个 Ingest 任务去摄入该补剂，
    /// 由 Comp_MEE_Satisfier.PostIngested 调用 Need_MEE_Base.Satisfy 补充。
    /// 通过 [StaticConstructorOnStartup] 把本节点插到所有含 JobGiver_Ingest 的思考树中（紧贴 Ingest 之后，
    /// 优先级高于工作，使"需求低时"能中断手头活去补）。
    /// </summary>
    public class JobGiver_SatisfyMetabolic : ThinkNode_JobGiver
    {
        /// <summary>需求低于该比例(0~1)时触发自动摄入。暂定 0.5（半满即去补）。</summary>
        private const float NeedThreshold = 0.5f;

        public override float GetPriority(Pawn pawn)
        {
            // 代谢需求优先级应与吃饭喝水相近，确保需求低时能中断工作去补
            return 5f;
        }

        /// <summary>需求 DefName → 可补充该需求的物品 DefName 列表（与 Comp_MEE_Satisfier 各物品配置一致）。
        /// 水需求可饮「饮用水」或「生水」；其余需求目前各只有一种补剂。</summary>
        private static readonly (string need, string[] items)[] Map = new[]
        {
            ("MEEWater", new[] { "MEE_WaterBottle", "MEE_RawWater" }),
            ("MEESugar", new[] { "MEE_GlucoseMash" }),
            ("MEEElectrolytes", new[] { "MEE_Salt" }),
            ("MEEProtein", new[] { "MEE_ProteinExtract" }),
        };

        protected override Job TryGiveJob(Pawn pawn)
        {
            // 模块未启用：需求不消耗、保持满值，自动摄入无意义（直接跳过）。
            if (!MetaBolicLoadCtrl.Active) return null;
            if (pawn == null || pawn.needs == null || pawn.Dead || pawn.Suspended) return null;
            if (pawn.Map == null) return null;
            // 睡眠/卧床中不派发补剂任务（代谢需求醒来后再补），避免半夜被叫醒打断睡眠。
            if (pawn.jobs?.curJob?.def == JobDefOf.LayDown) return null;

            foreach (var pair in Map)
            {
                NeedDef nd = DefDatabase<NeedDef>.GetNamed(pair.need, false);
                if (nd == null) continue;
                Need need = pawn.needs.TryGetNeed(nd);
                if (need == null) continue;
                if (need.CurLevelPercentage > NeedThreshold) continue;

                // 1) 优先找可摄入的补剂物品（水：先饮用水，其次生水）
                foreach (string itemName in pair.items)
                {
                    ThingDef itemDef = DefDatabase<ThingDef>.GetNamed(itemName, false);
                    if (itemDef == null) continue;
                    Thing item = FindItem(pawn, itemDef);
                    if (item != null)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Ingest, item);
                        job.count = 1;
                        return job;
                    }
                }

                // 2) 仅水需求：没有水物品时，退而求其次去池塘/野水直接喝（参考 DBH 的 DrinkFromGround）
                if (pair.need == "MEEWater")
                {
                    IntVec3 cell = FindNearestWaterCell(pawn, 9999f);
                    if (cell.IsValid)
                    {
                        JobDef drinkJob = DefDatabase<JobDef>.GetNamedSilentFail("MEE_DrinkFromGround");
                        if (drinkJob != null)
                        {
                            Job job = JobMaker.MakeJob(drinkJob, cell);
                            return job;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>优先取随身库存里的补剂（免搬运）；否则在地图上找可取用的。
        /// 注意：地图上的物品必须还能被预定到（CanReserveStack &gt; 0），否则 JobDriver_Ingest 预定时会
        /// 因 stackCount=0 打 "Could not reserve ... stackCount 0" 报错（原版 JobGiver_GetFood 会先检查预定）。</summary>
        private static Thing FindItem(Pawn pawn, ThingDef itemDef)
        {
            if (pawn.inventory != null && pawn.inventory.innerContainer != null)
            {
                foreach (Thing t in pawn.inventory.innerContainer)
                {
                    if (t.def == itemDef) return t;
                }
            }

            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(itemDef),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Deadly),
                999f,
                t => !t.IsForbidden(pawn)
                     && t.Map != null
                     && t.Map.reservationManager.CanReserveStack(pawn, t, 10) > 0);
        }

        /// <summary>在 pawn 附近寻找最近的可饮用自然水面格子（池塘/河/海等）。
        /// 采用与 DBH 类似的「区域扩散 + 逐格校验」：先找含水格的邻近区域，再在其中挑最近、未被禁用、可预约的水格。</summary>
        private static IntVec3 FindNearestWaterCell(Pawn pawn, float range)
        {
            Map map = pawn.Map;
            if (map == null) return IntVec3.Invalid;

            Region root = pawn.Position.GetRegion(map, RegionType.Set_Passable);
            if (root == null) return IntVec3.Invalid;

            if (!CellFinder.TryFindClosestRegionWith(
                    root,
                    TraverseParms.For(pawn, Danger.Deadly),
                    r => RegionHasWater(r, map, pawn, range),
                    300,
                    out Region found,
                    RegionType.Set_Passable))
                return IntVec3.Invalid;

            IntVec3 result = IntVec3.Invalid;
            float best = float.MaxValue;
            foreach (IntVec3 cell in found.Cells)
            {
                if (!IsWaterCell(cell, map)) continue;
                if (ForbidUtility.IsForbidden(cell, pawn)) continue;
                if (IntVec3Utility.DistanceTo(cell, pawn.Position) > range) continue;
                if (!ReservationUtility.CanReserve(pawn, cell, 1, -1, null, false)) continue;
                float d = IntVec3Utility.DistanceTo(cell, pawn.Position);
                if (d < best)
                {
                    best = d;
                    result = cell;
                }
            }
            return result;
        }

        private static bool RegionHasWater(Region r, Map map, Pawn pawn, float range)
        {
            if (ForbidUtility.IsForbiddenEntirely(r, pawn)) return false;
            if (range < 5000f && r.extentsClose.ClosestDistSquaredTo(pawn.Position) > range * range) return false;
            foreach (IntVec3 cell in r.Cells)
            {
                if (IsWaterCell(cell, map) && !ForbidUtility.IsForbidden(cell, pawn))
                    return true;
            }
            return false;
        }

        /// <summary>判定某格是否为自然水面（池塘/河/海）。
        /// 采用 defName 含 "Water" 的启发式，覆盖原版 WaterShallow/WaterDeep/WaterOcean*/WaterMoving* 及常见模组水面。</summary>
        private static bool IsWaterCell(IntVec3 cell, Map map)
        {
            TerrainDef td = GridsUtility.GetTerrain(cell, map);
            if (td == null) return false;
            return td.defName.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [StaticConstructorOnStartup]
    public static class ThinkTree_SatisfyMetabolic_Injection
    {
        static ThinkTree_SatisfyMetabolic_Injection()
        {
            foreach (ThinkTreeDef tree in DefDatabase<ThinkTreeDef>.AllDefs)
            {
                InjectInto(tree.thinkRoot);
            }
        }

        private static void InjectInto(ThinkNode node)
        {
            if (node == null) return;

            var subNodes = node.subNodes;
            if (subNodes == null) return;

            for (int i = 0; i < subNodes.Count; i++)
            {
                var child = subNodes[i];
                if (child is JobGiver_GetFood)
                {
                    // 紧贴 JobGiver_GetFood（饥饿进食）之后插入，使其优先级高于工作（需求低可中断手头活去补）
                    subNodes.Insert(i + 1, new JobGiver_SatisfyMetabolic());
                    return; // 本树已插入，结束该子树遍历
                }
                InjectInto(child);
            }
        }
    }
}
