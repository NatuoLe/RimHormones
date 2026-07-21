using HarmonyLib;
using RimWorld;
using Verse;
using Hormones;
using System.Collections.Generic;

namespace Hormones
{
    /// <summary>
    /// 诊断补丁：追踪 Cortisol Need 是否真正被 RimWorld 加入 pawn。
    /// 用于排查"飘字日志不出现 / 皮质醇数值不变化 / 初始化失败"。
    /// 确认问题修复后可整文件删除。
    /// </summary>

    // 最早时机：所有 Def 加载完成后 RimWorld 会调用本静态构造。
    // 用于确认 Cortisol NeedDef 是否真的进了 DefDatabase（XML 加载是否成功）。
    [StaticConstructorOnStartup]
    public static class CortisolNeed_ModLoadTrace
    {
        static CortisolNeed_ModLoadTrace()
        {
            bool defExists = DefDatabase<NeedDef>.GetNamed("Cortisol", false) != null;
            bool typeOk = false;
            try
            {
                System.Type t = typeof(Need_Cortisol);
                typeOk = t != null;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Cortisol-ModLoad] 访问 Need_Cortisol 类型异常: {ex}");
            }
            Log.Warning($"[Cortisol-ModLoad] DLL 已加载 | Cortisol NeedDef 已加载={defExists} | Need_Cortisol 类型可访问={typeOk}");
            if (!defExists)
            {
                Log.Error("[Cortisol-ModLoad] ⚠ Cortisol NeedDef 未加载！请检查 Defs/NeedDefs/Need_Cortisol.xml 是否存在、XML 是否合法、needClass 是否=Hormones.Need_Cortisol。Need 将永远不会被添加。");
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "AddOrRemoveNeedsAsAppropriate")]
    public static class CortisolNeed_InitTrace_Patch
    {
        // 每个 pawn 只报告一次“未拥有 / 已拥有”，避免刷屏
        private static readonly HashSet<Pawn> reportedNoNeed = new HashSet<Pawn>();
        private static readonly HashSet<Pawn> reportedHasNeed = new HashSet<Pawn>();

        [HarmonyPostfix]
        public static void Postfix(Pawn_NeedsTracker __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null)
                return;

            NeedDef cortisolDef = DefDatabase<NeedDef>.GetNamed("Cortisol", false);
            if (cortisolDef == null)
            {
                // Def 没加载，AddNeed 永远不会发生；只报一次
                if (!reportedNoNeed.Contains(pawn))
                {
                    reportedNoNeed.Add(pawn);
                    Log.Error($"[Cortisol-Init] pawn={(pawn.Name?.ToStringShort ?? "null")} 找不到 Cortisol NeedDef！Need 永远不会被添加。");
                }
                return;
            }

            Need need = __instance.TryGetNeed(cortisolDef);
            if (need == null)
            {
                if (!reportedNoNeed.Contains(pawn))
                {
                    reportedNoNeed.Add(pawn);
                    // 尝试诊断 ShouldHaveNeed 失败原因
                    string reason = DiagnoseShouldHaveNeed(__instance, pawn, cortisolDef);
                    Log.Warning($"[Cortisol-Init] pawn={(pawn.Name?.ToStringShort ?? "null")} 【没有】Cortisol Need。AddNeed 未执行或被异常吞掉。诊断: {reason}");
                }
            }
            else if (!reportedHasNeed.Contains(pawn))
            {
                reportedHasNeed.Add(pawn);
                reportedNoNeed.Remove(pawn);
                Log.Warning($"[Cortisol-Init] pawn={(pawn.Name?.ToStringShort ?? "null")} ✅ 已拥有 Cortisol Need（初始化成功）, CurLevel={need.CurLevel}");
            }
        }

        private static string DiagnoseShouldHaveNeed(Pawn_NeedsTracker tracker, Pawn pawn, NeedDef nd)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"intelligence={pawn.RaceProps.intelligence}(need>={nd.minIntelligence}) ");
            if ((int)pawn.RaceProps.intelligence < (int)nd.minIntelligence)
                sb.Append("[×intelligence不达标] ");
            sb.Append($"devStage={pawn.DevelopmentalStage}(filter={nd.developmentalStageFilter}) ");
            if (!nd.developmentalStageFilter.Has(pawn.DevelopmentalStage))
                sb.Append("[×发育阶段不符] ");
            if (nd.colonistsOnly && (pawn.Faction == null || !pawn.Faction.IsPlayer))
                sb.Append("[×仅殖民者] ");
            if (pawn.health?.hediffSet?.DisablesNeed(nd) == true)
                sb.Append("[×被Hediff禁用] ");
            if (pawn.story?.traits?.DisablesNeed(nd) == true)
                sb.Append("[×被Trait禁用] ");
            if (pawn.Ideo?.DisablesNeed(nd) == true)
                sb.Append("[×被意识形态禁用] ");
            return sb.ToString();
        }
    }
}
