using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 快感缺失(Anhedonia)：拥有 CortisolAnhedonia Hediff 的小人，所有正面心情
    /// (Thought.MoodOffset() > 0) 失效归零；负面与中性心情完全保留。
    ///
    /// 拦截点在 Thought.MoodOffset()——这是每条心情对总心情贡献的签名值来源。
    /// 因 Harmony 的基类 patch 不会自动覆盖子类 override，故需同时 patch 基类
    /// 与所有 override 子类（1.6 源码核实共 8 个 override 子类）。
    /// </summary>

    // 基类：覆盖所有直接使用基类实现的心情（绝大多数 situational / 普通想法）
    [HarmonyPatch(typeof(Thought), "MoodOffset")]
    public static class Thought_MoodOffset_Base_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thought __instance, ref float __result)
        {
            if (__result > 0f && AnhedoniaLogic.HasAnhedonia(__instance.pawn))
                __result = 0f;
        }
    }

    // 全部 override 子类（1.6 源码搜索 "public override float MoodOffset" 得到）
    [HarmonyPatch(typeof(Thought_Memory), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_Situational_GeneticChemicalDependency), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_Situational_Precept_SlavesInColony), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_Situational_KillThirst), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_Situational_Recluse), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_Counselled), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_DecreeUnmet), "MoodOffset")]
    [HarmonyPatch(typeof(Thought_PsychicHarmonizer), "MoodOffset")]
    public static class Thought_MoodOffset_Override_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thought __instance, ref float __result)
        {
            if (__result > 0f && AnhedoniaLogic.HasAnhedonia(__instance.pawn))
                __result = 0f;
        }
    }
}
