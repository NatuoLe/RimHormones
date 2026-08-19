using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Hormones;

namespace Hormones
{
    /// <summary>
    /// MEE 专属「存储水」WorkGiver：把地图上未存放的 MEE 水物品（MEE_RawWater / MEE_WaterBottle）
    /// 搬运进储藏区，作为 DBH 的 StockpileWaterBottles 在 MEE 接管下的等价物。
    /// 仅在 MEE 模块激活（MetaBolicLoadCtrl.Active）时工作；模块关时 ShouldSkip 直接跳过。
    /// </summary>
    public class WorkGiver_MEE_StoreWater : WorkGiver_Scanner
    {
        private static readonly HashSet<string> MEEWaterDefs = new HashSet<string>
        {
            "MEE_RawWater",
            "MEE_WaterBottle",
        };

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !MetaBolicLoadCtrl.Active;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || !MEEWaterDefs.Contains(t.def.defName)) return false;
            if (t.IsForbidden(pawn)) return false;
            if (t.IsInValidStorage()) return false;
            // 铁律：堆叠可能已被其他殖民民整堆预定（两 pawn 争抢同一瓶水）。
            // 不预检可预定性的话会派发出无法 Reserve 的任务，刷 "Could not reserve ... stackCount N" 报错。
            // CanReserveStack 返回 pawn 还能预定的数量，<=0 表示整堆已被他人占满。
            if (pawn.Map.reservationManager.CanReserveStack(pawn, t, 1) <= 0) return false;
            if (!StoreUtility.TryFindBestBetterStoreCellFor(t, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out _)) return false;
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 复用原版搬运逻辑：内部会正确寻找存储格并设置 job.count（搬运整个堆叠并考虑目标格剩余空间）。
            // 直接 JobMaker.MakeJob(HaulToCell, t, cell) 不设 count 会让 job.count 默认 -1，
            // Toils_Haul.cs 把 -1 判为非法强制改成 1，触发 "Invalid count: -1" 报错且每次只搬 1 个。
            return HaulAIUtility.HaulToStorageJob(pawn, t, forced);
        }
    }
}
