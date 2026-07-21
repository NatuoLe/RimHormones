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
        public static float GetSlightInsultWeightMultiplier(Pawn initiator)
        {
            float severity = CortisolLogic.GetCortisolSeverity(initiator);
            if (severity < 0.33f) return 0.5f;   // 正常波动
            if (severity < 0.66f) return 2.0f;   // 承压
            return 4.0f;                          // 高压
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_Slight), "RandomSelectionWeight")]
    public static class InteractionWorker_Slight_RandomSelectionWeight_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (initiator == null) return;
            __result *= CortisolInteractionUtility.GetSlightInsultWeightMultiplier(initiator);
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_Insult), "RandomSelectionWeight")]
    public static class InteractionWorker_Insult_RandomSelectionWeight_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (initiator == null) return;
            __result *= CortisolInteractionUtility.GetSlightInsultWeightMultiplier(initiator);
        }
    }
}
