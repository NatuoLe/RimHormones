using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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

            Logic.PhysiqueLogic.PhysiqueDatas.Initialize();
            Log.Message("[Hormones] PhysiqueDatas initialized with Vector3 object pool");

            var harmony = new Harmony("thgold.hormones");
            harmony.PatchAll();

            Log.Message("[Hormones] === Hormones Mod Loaded Successfully ===");
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Pawn_SpawnSetup_Patch
    {
        private static readonly HashSet<int> initializedPawns = new HashSet<int>();

        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (__instance == null || respawningAfterLoad || map == null) return;
            if (!__instance.RaceProps.Humanlike) return;

            int pawnId = __instance.thingIDNumber;
            if (initializedPawns.Contains(pawnId)) return;

            var hormonesComp = GetOrCreateHormonesComp(__instance);
            if (hormonesComp != null)
            {
                Log.Message("[Hormones] HormonesComponent initialized for " + __instance.Name.ToStringFull);
            }

            initializedPawns.Add(pawnId);
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