using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace Hormones
{

[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    static HarmonyPatches()
    {
        Log.Message("[Hormones] === Hormones Mod Loading Started ===");
        Log.Message("[Hormones] Assembly loaded: " + typeof(HarmonyPatches).Assembly.FullName);

        var compProps = new CompProperties_Hormones();
        Log.Message("[Hormones] CompProperties: Hormones(decay=" + compProps.decayRate + ", maxLevel=" + compProps.maxLevel + ")");

        var harmony = new Harmony("thgold.hormones");
        Log.Message("[Hormones] Patching all methods with Harmony...");

        harmony.PatchAll();

        Log.Message("[Harmony] Harmony patching complete!");
        Log.Message("[Hormones] === Hormones Mod Loaded Successfully ===");
    }
}

[HarmonyPatch(typeof(Pawn), "SpawnSetup")]
public static class Pawn_SpawnSetup_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
    {
        if (__instance == null || respawningAfterLoad) return;
        if (!__instance.RaceProps.Humanlike) return;

        Log.Message("[Hormones] Pawn_SpawnSetup_Patch: " + __instance?.Name?.ToStringFull ?? "Unknown");

        GetOrCreateHormonesComp(__instance);
    }

    private static HormonesComponent GetOrCreateHormonesComp(Pawn pawn)
    {
        var existingComp = pawn.GetComp<HormonesComponent>();
        if (existingComp != null)
        {
            return existingComp;
        }

        var compProps = new CompProperties_Hormones();

        var comp = new HormonesComponent();
        comp.props = compProps;
        comp.parent = pawn;
        comp.Initialize(compProps);
        pawn.AllComps.Add(comp);

        Log.Message("[Hormones] HormonesComponent added to " + pawn.Name.ToStringFull);
        return comp;
    }
}



}
