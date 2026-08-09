using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 快感缺失(Anhedonia)逻辑：拥有 CortisolAnhedonia Hediff 的小人，
    /// 所有正面心情(Thought.MoodOffset > 0)失效归零，负面与中性心情保留。
    /// 该 Hediff 为「独立手动控制」——不随神经衰弱自动挂，存续由外部逻辑决定。
    /// </summary>
    public static class AnhedoniaLogic
    {
        public const string DefName = "CortisolAnhedonia";

        /// <summary>小人是否处于快感缺失状态</summary>
        public static bool HasAnhedonia(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
                return false;
            HediffDef def = DefDatabase<HediffDef>.GetNamed(DefName, false);
            return def != null && pawn.health.hediffSet.HasHediff(def);
        }

        /// <summary>独立触发：守卫式添加（避免重复叠加）</summary>
        public static void Add(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
                return;
            if (HasAnhedonia(pawn))
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamed(DefName, false);
            if (def == null)
                return;
            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            hediff.Severity = 1.0f;
            pawn.health.AddHediff(hediff);
        }

        /// <summary>独立解除</summary>
        public static void Remove(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamed(DefName, false);
            if (def == null)
                return;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
        }
    }
}
