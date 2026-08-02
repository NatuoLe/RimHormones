using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 劳损「软禁止」工具与判定（2026-08-01 返工版）。
    ///
    /// 设计：不使用 HediffStage.disabledWorkTags（那是"无法从事"级——工作表锁死、
    /// 手动指派也被禁）。改为对 Pawn_WorkSettings.WorkGiversInOrder* 的 getter
    /// 做 postfix 过滤：处于「体力不支」状态的 pawn，AI 扫描工作时直接看不到
    /// 重体力 WorkGiver（采矿/搬运/建造/种植/狩猎）。
    /// 效果：
    ///   · 工作表【不】置灰、不显示"无法从事"（WorkTypeIsDisabled 不受影响）；
    ///   · AI 不会自动接重活（JobGiver_Work.cs:78 的列表被过滤）；
    ///   · 玩家右键"优先指派"仍然可用（FloatMenuOptionProvider_WorkGivers
    ///     不走 WorkGiversInOrder 列表）——类比研究台"禁止"：不锁能力、随时恢复；
    ///   · 缓存列表不被修改（postfix 返回新的过滤副本），解除后立刻恢复原列表。
    /// </summary>
    public static class StrainWorkBlockUtility
    {
        /// <summary>参与软禁止的重体力工作标签（与劳损结算白名单一致）。</summary>
        public const WorkTags HeavyTags = WorkTags.Mining | WorkTags.Hauling
            | WorkTags.Constructing | WorkTags.PlantWork | WorkTags.Hunting;

        private static HediffDef exhaustedDefCache;

        /// <summary>pawn 当前是否处于「体力不支」状态（hediff 由 HormonesComponent 按阈值+滞回管理）。</summary>
        public static bool IsBlocked(Pawn pawn)
        {
            if (pawn == null) return false;
            if (exhaustedDefCache == null)
                exhaustedDefCache = DefDatabase<HediffDef>.GetNamedSilentFail("PhysiqueStrainExhausted");
            if (exhaustedDefCache == null) return false;
            return pawn.health?.hediffSet?.GetFirstHediffOfDef(exhaustedDefCache) != null;
        }

        /// <summary>该 WorkGiver 是否属于被软禁止的重体力工种。</summary>
        public static bool IsHeavyWorkGiver(WorkGiver wg)
        {
            WorkTypeDef wt = wg?.def?.workType;
            return wt != null && (wt.workTags & HeavyTags) != 0;
        }

        /// <summary>体力不支时返回过滤掉重体力工种的新列表；否则返回 null（表示无需替换）。</summary>
        public static List<WorkGiver> FilterIfBlocked(Pawn pawn, List<WorkGiver> source)
        {
            if (source == null) return null;
            if (!IsBlocked(pawn)) return null;
            return source.Where(wg => !IsHeavyWorkGiver(wg)).ToList();
        }
    }

    /// <summary>常规工作扫描列表过滤（JobGiver_Work 非应急路径）。</summary>
    [HarmonyPatch(typeof(Pawn_WorkSettings), "WorkGiversInOrderNormal", MethodType.Getter)]
    public static class WorkGiversInOrderNormal_StrainBlock_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn ___pawn, ref List<WorkGiver> __result)
        {
            List<WorkGiver> filtered = StrainWorkBlockUtility.FilterIfBlocked(___pawn, __result);
            if (filtered != null) __result = filtered;
        }
    }

    /// <summary>应急工作扫描列表过滤（火灾等应急路径，保持一致的软禁止语义）。</summary>
    [HarmonyPatch(typeof(Pawn_WorkSettings), "WorkGiversInOrderEmergency", MethodType.Getter)]
    public static class WorkGiversInOrderEmergency_StrainBlock_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn ___pawn, ref List<WorkGiver> __result)
        {
            List<WorkGiver> filtered = StrainWorkBlockUtility.FilterIfBlocked(___pawn, __result);
            if (filtered != null) __result = filtered;
        }
    }
}
