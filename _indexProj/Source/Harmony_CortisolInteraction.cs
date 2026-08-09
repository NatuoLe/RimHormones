using HarmonyLib;
using RimWorld;
using Verse;
using Hormones;

namespace Hormones
{
    /// <summary>
    /// 皮质醇对人物交互权重的影响。
    /// 皮质醇越高，发起者越倾向于对别人做出冒犯(Slight)/侮辱(Insult)行为。
    /// 仅作用于 RandomSelectionWeight（互动被选择的概率），不影响社交打架概率(socialFightBaseChance)。
    /// 系数按「发起者(initiator)自己的皮质醇浓度」映射：
    ///   0 ≤ S < 0.33    正常波动  ×0.5
    ///   0.33 ≤ S < 0.66 承压     ×2.0
    ///   0.66 ≤ S ≤ 1.0  高压     ×4.0
    /// </summary>
    public static class CortisolInteractionUtility
    {
        /// <summary>
        /// 返回 (档位名, 权重倍率)。
        /// 0 ≤ S &lt; 0.33    正常波动  ×0.5
        /// 0.33 ≤ S &lt; 0.66 承压     ×2.0
        /// 0.66 ≤ S ≤ 1.0  高压     ×4.0
        /// </summary>
        public static (string tierLabel, float mult) GetSlightInsultWeightInfo(Pawn initiator)
        {
            float severity = Need_Cortisol.GetCortisolSeverity(initiator);
            if (severity < 0.33f) return ("正常波动", 0.5f);   // 正常波动
            if (severity < 0.66f) return ("承压", 2.0f);   // 承压
            return ("高压", 4.0f);                          // 高压
        }

        public static float GetSlightInsultWeightMultiplier(Pawn initiator)
        {
            return GetSlightInsultWeightInfo(initiator).mult;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_Slight), "RandomSelectionWeight")]
    public static class InteractionWorker_Slight_RandomSelectionWeight_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (initiator == null) return;
            var info = CortisolInteractionUtility.GetSlightInsultWeightInfo(initiator);
            __result *= info.mult;
            Need_Cortisol.ShowCortisolSocialMote(initiator,
                $"社交·冒犯: {info.tierLabel} ×{info.mult:F1}");
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_Insult), "RandomSelectionWeight")]
    public static class InteractionWorker_Insult_RandomSelectionWeight_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (initiator == null) return;
            var info = CortisolInteractionUtility.GetSlightInsultWeightInfo(initiator);
            __result *= info.mult;
            Need_Cortisol.ShowCortisolSocialMote(initiator,
                $"社交·侮辱: {info.tierLabel} ×{info.mult:F1}");
        }
    }
}
