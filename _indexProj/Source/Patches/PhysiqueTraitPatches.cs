using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 修改体魄技能获取经验倍率（升级效率）
    /// </summary>
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_Skill_PhysiqueExpRate
    {
        [HarmonyPrefix]
        public static void Prefix(SkillRecord __instance, ref float xp)
        {
            if (__instance == null || __instance.def == null) return;

            SkillDef physiqueDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueDef == null) return;
            if (__instance.def != physiqueDef) return;

            Pawn pawn = GetPawn(__instance);
            if (pawn == null) return;

            float multi = PhysiqueTraitUtility.GetTotalExpFactor(pawn);
            multi = Math.Max(0.2f, Math.Min(3f, multi));
            
            if (multi != 1f)
            {
                xp *= multi;
            }
        }

        private static Pawn GetPawn(SkillRecord skillRecord)
        {
            var field = typeof(SkillRecord).GetField("pawn", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(skillRecord) as Pawn;
        }
    }
}