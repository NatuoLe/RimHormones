using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Hormones
{
    public static class AdrenalineProducer
    {
        public static float CalculateGenerationMultiplier(int physique)
        {
            return 1.8f - 0.04f * physique;
        }

        public static float GetAttackAdrenalineGain(bool isMelee, int physique)
        {
            float baseValue = isMelee ? Define.AdrenalineMeleeAttackBase : Define.AdrenalineRangedAttackBase;
            float multiplier = CalculateGenerationMultiplier(physique);
            return baseValue * multiplier;
        }

        public static float GetHitAdrenalineGain(int physique)
        {
            float multiplier = CalculateGenerationMultiplier(physique);
            return Define.AdrenalineHitBase * multiplier;
        }

        public static float GetDecayPerSecond(int physique)
        {
            return Define.AdrenalineBaseDecay + physique * Define.AdrenalineDecayPerPhysique;
        }

        public static float GetCombatInterpolationGain(int physique)
        {
            float baseValue = Define.AdrenalineCombatInterpolationBase;
            float multiplier = CalculateGenerationMultiplier(physique);
            return baseValue * multiplier;
        }

        public static float CalculateNetChangePerSecond(Pawn pawn)
        {
            int physique = PhysiqueLgc.GetPhysiqueLevel(pawn);
            
            float decay = GetDecayPerSecond(physique);
            
            bool inCombatZone = IsInCombatZone(pawn);
            float combatGain = inCombatZone ? GetCombatInterpolationGain(physique) : 0f;
            
            return combatGain - decay;
        }

        public static bool IsInCombatZone(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned)
                return false;

            List<Thing> things = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);
            foreach (Thing thing in things)
            {
                Pawn other = thing as Pawn;
                if (other == null || other == pawn)
                    continue;
                
                if (!other.Spawned)
                    continue;

                float distance = pawn.Position.DistanceTo(other.Position);
                if (distance > Define.AdrenalineCombatDetectionRange)
                    continue;

                if (IsInCombatState(other))
                    return true;
            }
            
            return false;
        }

        public static bool IsInCombatState(Pawn pawn)
        {
            if (pawn.Dead || pawn.Downed)
                return false;

            if (pawn.HostileTo(Faction.OfPlayer))
            {
                if (pawn.mindState?.enemyTarget != null)
                    return true;

                if (pawn.mindState?.lastHarmTick > Find.TickManager.TicksGame - 60)
                    return true;
            }

            return false;
        }

        public static void ProcessAdrenalineDynamic(Pawn pawn)
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
                return;

            float netChange = CalculateNetChangePerSecond(pawn);
            float changePerTick = netChange / 60f;

            adrenaline.Severity = Math.Min(Math.Max(adrenaline.Severity + changePerTick, 0f), 1f);

            if (adrenaline.Severity <= 0)
            {
                pawn.health.RemoveHediff(adrenaline);
            }
        }

        public static void OnAttack(Pawn attacker, bool isMelee)
        {
            Hediff adrenaline = attacker.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
            {
                adrenaline = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Adrenaline", false), attacker);
                attacker.health.AddHediff(adrenaline);
            }

            int physique = PhysiqueLgc.GetPhysiqueLevel(attacker);
            float gain = GetAttackAdrenalineGain(isMelee, physique);
            
            adrenaline.Severity = Math.Min(adrenaline.Severity + gain, 1f);
        }

        public static void OnHit(Pawn victim)
        {
            Hediff adrenaline = victim.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
            {
                adrenaline = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Adrenaline", false), victim);
                victim.health.AddHediff(adrenaline);
            }

            int physique = PhysiqueLgc.GetPhysiqueLevel(victim);
            float gain = GetHitAdrenalineGain(physique);
            
            adrenaline.Severity = Math.Min(adrenaline.Severity + gain, 1f);
        }

    }
}