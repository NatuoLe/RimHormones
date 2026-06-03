using RimWorld;
using Verse;
using HarmonyLib;
using System;

namespace Hormones
{

public static class HormonesLogic
{
    private static int GetPhysiqueLevel(Pawn pawn)
    {
        if (pawn == null) return 1;
        SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
        if (physiqueSkillDef == null) return 1;
        SkillRecord skill = pawn.skills?.GetSkill(physiqueSkillDef);
        int level = skill?.levelInt ?? 1;
        return Helpers.Clamp(level, Define.PhysiqueMinLevel, Define.PhysiqueMaxLevel);
    }

    private static float GetPhysiqueBonus(Pawn pawn)
    {
        int physiqueLevel = GetPhysiqueLevel(pawn);

        if (physiqueLevel < Define.PhysiqueNegativeThresholdHigh)
        {
            return Define.PhysiqueLowPenalty;
        }
        else if (physiqueLevel <= Define.PhysiqueNegativeThresholdLow)
        {
            return Define.PhysiqueMediumPenalty;
        }
        else
        {
            float bonus = 1f + (physiqueLevel - Define.PhysiquePositiveThreshold + 1) * Define.PhysiqueBonusPerLevel;
            return bonus;
        }
    }

    public static float GetWorkEfficiency(Pawn pawn)
    {
        int physiqueLevel = GetPhysiqueLevel(pawn);
        float workEfficiency = Define.WorkEfficiencyBase + Define.WorkEfficiencyPerPhysique * physiqueLevel;
        return Helpers.Clamp(workEfficiency, Define.WorkEfficiencyMin, Define.WorkEfficiencyMax);
    }

    public static float GetHungerRate(Pawn pawn)
    {
        int physiqueLevel = GetPhysiqueLevel(pawn);
        float hungerRate = Define.HungerRateBase + Define.HungerRatePerPhysique * physiqueLevel;
        return Helpers.Clamp(hungerRate, Define.HungerRateMin, Define.HungerRateMax);
    }

    public static void ApplyPhysiqueCombatBonus(Pawn pawn, ref float hitChance)
    {
        float physiqueBonus = GetPhysiqueBonus(pawn);
        hitChance *= physiqueBonus;
    }

    public static float GetMetabolicRate(Pawn pawn)
    {
        int physiqueLevel = GetPhysiqueLevel(pawn);
        return Define.MetabolicRateBase + Define.MetabolicRatePerPhysique * physiqueLevel;
    }

    public static float GetAppetiteMultiplier(Pawn pawn)
{
    int physiqueLevel = GetPhysiqueLevel(pawn);
    float appetite = Define.AppetiteBase + Define.AppetitePerPhysique * physiqueLevel;
    return Helpers.Clamp(appetite, Define.AppetiteMinMultiplier, Define.AppetiteMaxMultiplier);
}

    public static void ApplyHormonesCombatPenalty(Pawn pawn, ref float hitChance)
    {
        HormonesComponent hormones = pawn.GetHormones();
        if (hormones != null)
        {
            if (hormones.IsPanicked)
            {
                hitChance *= 0.6f;
            }
            else if (hormones.IsStressed)
            {
                hitChance *= 0.8f;
            }
        }
    }
}

[HarmonyPatch(typeof(Verb_MeleeAttack), "GetNonMissChance")]
public static class Verb_MeleeAttack_GetNonMissChance_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, Verb_MeleeAttack __instance)
    {
        if (__instance.CasterPawn != null)
        {
            float originalHitChance = __result;
            HormonesLogic.ApplyPhysiqueCombatBonus(__instance.CasterPawn, ref __result);
            HormonesLogic.ApplyHormonesCombatPenalty(__instance.CasterPawn, ref __result);
            // Log.Message($"[Hormones] {__instance.CasterPawn?.Name?.ToStringFull ?? "Unknown"} MeleeHitChance: {originalHitChance:F3} -> {__result:F3}");
        }   
    }
}

[HarmonyPatch(typeof(Thing), "TakeDamage")]
public static class Thing_TakeDamage_Patch
{
    [HarmonyPostfix]
    public static void Postfix(DamageInfo dinfo, Thing __instance)
    {
        if (dinfo.Amount <= 0) return;

        Pawn pawn = __instance as Pawn;
        if (pawn == null) return;

        // Log.Message($"[Hormones] {pawn?.Name?.ToStringFull ?? "Unknown"} Took damage: {dinfo.Amount} from {dinfo.Def?.label ?? "unknown"}");

        HormonesComponent hormonesComp = pawn.GetComp<HormonesComponent>();
        if (hormonesComp != null)
        {
            float baseDamage = 15f;
            float damageFactor = Math.Min(dinfo.Amount / 10f, 3f);
            float actualReduction = baseDamage * damageFactor;

            hormonesComp.AddHormonesReduction(actualReduction);
            // Log.Message($"[Hormones] {pawn?.Name?.ToStringFull ?? "Unknown"} Hormones reduced by {actualReduction:F1} (base={baseDamage}, factor={damageFactor:F2})");
        }
        else
        {
            // Log.Warning($"[Hormones] {pawn?.Name?.ToStringFull ?? "Unknown"} HormonesComponent not found!");
        }

        AdrenalineProducer.OnHit(pawn);
    }
}

[HarmonyPatch(typeof(Need_Food), nameof(Need_Food.MaxLevel), MethodType.Getter)]
public static class Need_Food_MaxLevel_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, Need_Food __instance)
    {
        Pawn pawn = __instance.GetType().BaseType.GetField("pawn",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(__instance) as Pawn;

        if (pawn == null) return;

        float appetite = HormonesLogic.GetAppetiteMultiplier(pawn);
        float original = __result;
        __result *= appetite;

        // Log.Message($"[Hormones] {pawn?.Name?.ToStringFull ?? "Unknown"} FoodMaxLevel: {original:F2} -> {__result:F2} (Appetite={appetite:F2})");
    }
}

[HarmonyPatch(typeof(Need_Food), "FoodFallPerTickAssumingCategory")]
public static class Need_Food_FoodFallPerTickAssumingCategory_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, Need_Food __instance)
    {
        Pawn pawn = __instance.GetType().BaseType.GetField("pawn",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(__instance) as Pawn;

        if (pawn == null) return;

        float hungerRate = HormonesLogic.GetHungerRate(pawn);
        float original = __result;
        __result *= hungerRate;

        // Log.Message($"[Hormones] {pawn?.Name?.ToStringFull ?? "Unknown"} FoodFallPerTick: {original:F6} -> {__result:F6} (HungerRate={hungerRate:F2})");
    }
}

[HarmonyPatch(typeof(StatWorker), "GetValue", new System.Type[] { typeof(Thing), typeof(bool), typeof(int) })]
public static class StatWorker_GetValue_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, StatWorker __instance, Thing thing, bool applyPostProcess, int cacheStaleAfterTicks)
    {
        if (thing is Pawn pawn)
        {
            StatDef stat = __instance.GetType().GetField("stat",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(__instance) as StatDef;

            if (stat == StatDefOf.WorkSpeedGlobal)
            {
                float workEfficiency = HormonesLogic.GetWorkEfficiency(pawn);
                __result *= workEfficiency;
            }

            AdrenalineEffects effects = AdrenalineLogic.CalculateAdrenalineEffects(pawn);
            if (effects.HasActiveEffects)
            {
                if (stat == StatDefOf.MoveSpeed)
                {
                    __result *= (1 + effects.MoveSpeed);
                }
            }
        }
    }
}

[HarmonyPatch(typeof(Need_Food), "FoodFallPerTickAssumingCategory")]
public static class Need_Food_FallRate_Adrenaline_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, Need_Food __instance)
    {
        Pawn pawn = __instance.GetType().BaseType.GetField("pawn",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(__instance) as Pawn;

        if (pawn == null) return;

        AdrenalineEffects effects = AdrenalineLogic.CalculateAdrenalineEffects(pawn);
        if (effects.HasActiveEffects)
        {
            __result *= (1 + effects.Metabolism);
        }
    }
}

[HarmonyPatch(typeof(Pawn), "Tick")]
public static class Pawn_Tick_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        if (__instance.IsHashIntervalTick(60))
        {
            AdrenalineProducer.ProcessAdrenalineDynamic(__instance);
        }
    }
}

[HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
public static class Verb_MeleeAttack_TryCastShot_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Verb_MeleeAttack __instance)
    {
        Pawn attacker = __instance.CasterPawn;
        if (attacker != null)
        {
            AdrenalineProducer.OnAttack(attacker, true);

            AdrenalineEffects effects = AdrenalineLogic.CalculateAdrenalineEffects(attacker);
            if (effects.HasActiveEffects && effects.Level >= AdrenalineLevel.High)
            {
                AdrenalineLogic.TryApplyOverexertDamage(attacker);
            }
        }
    }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class Verb_LaunchProjectile_TryCastShot_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Verb_LaunchProjectile __instance)
    {
        Pawn attacker = __instance.CasterPawn;
        if (attacker != null)
        {
            AdrenalineProducer.OnAttack(attacker, false);
        }
    }
}

}