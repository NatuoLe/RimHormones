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

            // Combat Extended 适配已拆分为独立补丁包（thgold.hormones.ce）。
            // 本体只保留原版战斗逻辑；装了 CE 补丁包后由其重挂 CE 战斗入口。

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
            // 注意：读档时 respawningAfterLoad==true，但手动 AllComps.Add 的 comp 不随存档序列化，
            // 因此读档后老殖民者身上没有 HormonesComponent，必须在这里补挂，否则 CompTick 不跑（体魄/激素全失效）。
            if (__instance == null || map == null) return;
            if (!__instance.RaceProps.Humanlike) return;

            // respawningAfterLoad 时不能用 initializedPawns 缓存跳过（该缓存跨存档不清空，会误判已初始化）
            int pawnId = __instance.thingIDNumber;
            if (!respawningAfterLoad && initializedPawns.Contains(pawnId)) return;

            var hormonesComp = GetOrCreateHormonesComp(__instance);
            if (hormonesComp != null)
            {
                Log.Message("[Hormones] HormonesComponent initialized for " + __instance.Name.ToStringFull + " (respawningAfterLoad=" + respawningAfterLoad + ")");
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