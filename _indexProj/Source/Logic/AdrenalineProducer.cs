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

        /// <summary>
        /// 【2026-08-02 新增】每秒因流血产生的肾上腺素。
        /// 流血速率(HediffSet.BleedRateTotal) &lt;150% 用 AdrenalineBloodingLowBase，
        /// &gt;150% 用 AdrenalineBloodingHighBase，再乘体魄生成系数；不流血为 0。
        /// </summary>
        public static float GetBleedingAdrenalineGain(Pawn pawn, int physique)
        {
            float bleedRate = pawn.health.hediffSet.BleedRateTotal;
            if (bleedRate <= 0f) return 0f;
            float baseValue = bleedRate < Define.AdrenalineBloodingThreshold
                ? Define.AdrenalineBloodingLowBase
                : Define.AdrenalineBloodingHighBase;
            return baseValue * CalculateGenerationMultiplier(physique);
        }

        public static float CalculateNetChangePerSecond(Pawn pawn)
        {
            int physique = PhysiqueLgc.GetPhysiqueLevel(pawn);

            float decay = GetDecayPerSecond(physique);

            bool inCombatZone = IsInCombatZone(pawn);
            float combatGain = inCombatZone ? GetCombatInterpolationGain(physique) : 0f;

            float bleedingGain = GetBleedingAdrenalineGain(pawn, physique);

            return combatGain + bleedingGain - decay;
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
            HediffDef adrenalineDef = DefDatabase<HediffDef>.GetNamed("Adrenaline", false);
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(adrenalineDef);

            // 非类人生物（含旧存档遗留）：直接移除肾上腺素，不再处理。
            if (!PhysiqueLgc.IsHormoneSubject(pawn))
            {
                if (adrenaline != null)
                    pawn.health.RemoveHediff(adrenaline);
                return;
            }

            // 【2026-08-02】无肾上腺素但正在流血：流血本身即可唤起肾上腺素
            // （否则旧伤流血时 hediff 已衰减移除，流血产肾上腺素永远不生效）。
            if (adrenaline == null)
            {
                float createGain = GetBleedingAdrenalineGain(pawn, PhysiqueLgc.GetPhysiqueLevel(pawn));
                if (createGain > 0f)
                {
                    adrenaline = HediffMaker.MakeHediff(adrenalineDef, pawn);
                    adrenaline.Severity = Math.Min(createGain, 1f);
                    pawn.health.AddHediff(adrenaline);
                }
                return;
            }

            float netChange = CalculateNetChangePerSecond(pawn);

            // Pawn_Tick_Patch 每 60 tick 调用一次（≈1 秒），netChange 已是"每秒"变化，直接加
            float newSeverity = Math.Min(Math.Max(adrenaline.Severity + netChange, 0f), 1f);
            adrenaline.Severity = newSeverity;

            if (adrenaline.Severity <= 0)
            {
                pawn.health.RemoveHediff(adrenaline);
            }
        }

        public static void OnAttack(Pawn attacker, bool isMelee)
        {
            // 仅类人生物拥有肾上腺素系统；动物/机械体攻击不产生肾上腺素与后续透支损伤。
            if (!PhysiqueLgc.IsHormoneSubject(attacker)) return;
            Hediff adrenaline = attacker.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
            {
                adrenaline = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Adrenaline", false), attacker);
                attacker.health.AddHediff(adrenaline);
            }

            int physique = PhysiqueLgc.GetPhysiqueLevel(attacker);
            float gain = GetAttackAdrenalineGain(isMelee, physique);
            float oldSeverity = adrenaline.Severity;
            float newSeverity = Math.Min(oldSeverity + gain, 1f);
            
            adrenaline.Severity = newSeverity;
            string reason = isMelee ? "近战" : "远程";
            AdrenalineLogic.ShowAdrenalineMote(attacker, reason, newSeverity - oldSeverity, newSeverity);
        }

        public static void OnHit(Pawn victim)
        {
            // 仅类人生物拥有肾上腺素系统。
            if (!PhysiqueLgc.IsHormoneSubject(victim)) return;
            Hediff adrenaline = victim.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
            {
                adrenaline = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Adrenaline", false), victim);
                victim.health.AddHediff(adrenaline);
            }

            int physique = PhysiqueLgc.GetPhysiqueLevel(victim);
            float gain = GetHitAdrenalineGain(physique);
            float oldSeverity = adrenaline.Severity;
            float newSeverity = Math.Min(oldSeverity + gain, 1f);
            
            adrenaline.Severity = newSeverity;
            AdrenalineLogic.ShowAdrenalineMote(victim, "受伤", newSeverity - oldSeverity, newSeverity);
        }

    }
}