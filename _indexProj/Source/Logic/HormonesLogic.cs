using RimWorld;
using Verse;
using HarmonyLib;
using System;

namespace Hormones
{

public static class HormonesLogic
{
    public static float GetWorkEfficiency(Pawn pawn)
    {
        return PhysiqueLgc.GetWorkEfficiency(pawn);
    }

    public static float GetHungerRate(Pawn pawn)
    {
        return PhysiqueLgc.GetHungerRate(pawn);
    }

    public static void ApplyPhysiqueCombatBonus(Pawn pawn, ref float hitChance)
    {
        PhysiqueLgc.ApplyPhysiqueCombatBonus(pawn, ref hitChance);
    }

    public static float GetMetabolicRate(Pawn pawn)
    {
        return PhysiqueLgc.GetMetabolicRate(pawn);
    }

    public static float GetAppetiteMultiplier(Pawn pawn)
    {
        return PhysiqueLgc.GetAppetiteMultiplier(pawn);
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
                else if (stat == StatDefOf.MeleeDamageFactor)
                {
                    __result *= (1 + effects.MeleeDamage);
                }
                else if (stat == StatDefOf.MeleeDodgeChance)
                {
                    __result *= (1 + effects.Dodge);
                }
                else if (stat == StatDefOf.MeleeHitChance)
                {
                    // MeleeHitReduction 为负值（战斗应激手抖），此处按比例降低命中率
                    __result *= (1 + effects.MeleeHitReduction);
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

            AdrenalineEffects effects = AdrenalineLogic.CalculateAdrenalineEffects(attacker);
            if (effects.HasActiveEffects && effects.Level >= AdrenalineLevel.High)
            {
                AdrenalineLogic.TryApplyOverexertDamage(attacker, Define.AdrenalineRangedOverexertChanceMultiplier);
            }
        }
    }
}

/// <summary>
/// 皮质醇冒犯/侮辱权重修正
/// 通过 Harmony 修改 NegativeInteractionUtility.NegativeInteractionChanceFactor 的返回值
/// 正常波动 ×0.5，承压 ×2.0，高压 ×4.0
/// </summary>
[HarmonyPatch(typeof(NegativeInteractionUtility), "NegativeInteractionChanceFactor")]
public static class NegativeInteractionUtility_ChanceFactor_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, Pawn initiator, Pawn recipient)
    {
        if (initiator != null)
        {
            Need_Cortisol cortisol = initiator.needs?.TryGetNeed<Need_Cortisol>();
            if (cortisol != null)
            {
                __result *= cortisol.GetSocialFightChanceFactor();
            }
        }
    }
}

/// <summary>
/// 体魄技能热情倍率自定义补丁
/// 覆盖原版 SkillRecord.Learn 的 Passion 倍率：
///   无(None):×1.0, 好奇(Minor):×1.1, 狂热(Major):×1.2
/// 原版真实倍率（RimWorld 1.6，来自 SkillDef.json 的 PassionsData.LearningFactor）：
///   无(None):×0.333, 好奇(Minor):×1.0, 狂热(Major):×1.5
/// 通过 Prefix 调整 xp 参数，先除掉原版倍率再乘自定义倍率，实现精确覆盖。
/// 注意：只对 Physique 技能生效，其余技能保持原版。
/// </summary>
[HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
public static class SkillRecord_Learn_Physique_Patch
{
    private static SkillDef PhysiqueSkillDef => DefDatabase<SkillDef>.GetNamed("Physique", false);

    [HarmonyPrefix]
    public static void Prefix(SkillRecord __instance, ref float xp)
    {
        if (__instance?.def != PhysiqueSkillDef)
            return;
        // 只对“学习获得”的正经验施加热情倍率覆盖。
        // 负经验（如体魄日常衰减 Learn(-decayXP, direct:true)）是独立机制，
        // 其数值已在调用处算好，不能再被热情倍率缩放，否则衰减量会失真。
        if (xp <= 0f)
            return;

        float originalMultiplier = GetOriginalPassionMultiplier(__instance.passion);
        float customMultiplier = GetCustomPassionMultiplier(__instance.passion);
        xp = xp / originalMultiplier * customMultiplier;
    }

    // 原版 RimWorld 对 xp 实际施加的 passion 学习倍率（按用户指定值）：
    //   无 0.35 / 好奇 1.0 / 狂热 1.5。
    private static float GetOriginalPassionMultiplier(Passion passion)
    {
        switch (passion)
        {
            case Passion.None:   return 0.35f;
            case Passion.Minor:  return 1.0f;
            case Passion.Major:  return 1.5f;
            default:             return 1.5f; // Major 以上（如 mod 扩展的双狂热）按狂热处理
        }
    }

    // 想要的自定义倍率：无 100% / 好奇 110% / 狂热 120%。
    private static float GetCustomPassionMultiplier(Passion passion)
    {
        switch (passion)
        {
            case Passion.None:   return 1.0f;
            case Passion.Minor:  return 1.1f;
            case Passion.Major:  return 1.2f;
            default:             return 1.2f;
        }
    }
}

}
