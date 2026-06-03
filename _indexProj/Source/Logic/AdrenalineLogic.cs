using Verse;
using RimWorld;
using System;
using System.Collections.Generic;

namespace Hormones
{
    public enum AdrenalineLevel
    {
        Dormant,
        Low,
        Medium,
        High
    }

    public static class AdrenalineLogic
    {
        public static AdrenalineLevel GetAdrenalineLevel(float severity)
        {
            if (severity < Define.AdrenalineThresholdDormant)
                return AdrenalineLevel.Dormant;
            if (severity < Define.AdrenalineThresholdLow)
                return AdrenalineLevel.Low;
            if (severity < Define.AdrenalineThresholdMedium)
                return AdrenalineLevel.Medium;
            return AdrenalineLevel.High;
        }

        public static float GetPhysiqueModifier(int physiqueLevel)
        {
            if (physiqueLevel < Define.PhysiqueAdrenalinePenaltyThreshold)
                return Define.PhysiqueAdrenalinePenaltyFactor;
            return 1.0f;
        }

        public static bool IsVisionHearingExempt(int physiqueLevel)
        {
            return physiqueLevel >= Define.PhysiqueAdrenalinePenaltyThreshold;
        }

        public static bool IsMeleeHitExempt(int physiqueLevel)
        {
            return physiqueLevel >= Define.PhysiqueAdrenalineExemptionThreshold;
        }

        public static AdrenalineEffects CalculateAdrenalineEffects(Pawn pawn)
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
                return new AdrenalineEffects();

            float severity = adrenaline.Severity;
            AdrenalineLevel level = GetAdrenalineLevel(severity);
            
            if (level == AdrenalineLevel.Dormant)
                return new AdrenalineEffects();

            int physiqueLevel = GetPhysiqueLevel(pawn);
            float physiqueMod = GetPhysiqueModifier(physiqueLevel);
            bool visionHearingExempt = IsVisionHearingExempt(physiqueLevel);
            bool meleeHitExempt = IsMeleeHitExempt(physiqueLevel);

            AdrenalineEffects effects = new AdrenalineEffects();
            effects.Level = level;
            effects.PhysiqueModifier = physiqueMod;

            switch (level)
            {
                case AdrenalineLevel.Low:
                    effects.Consciousness = Define.AdrenalineLow.Consciousness * physiqueMod;
                    effects.MoveSpeed = Define.AdrenalineLow.MoveSpeed * physiqueMod;
                    effects.Respiratory = Define.AdrenalineLow.RespiratoryCirculatory * physiqueMod;
                    effects.Circulation = Define.AdrenalineLow.RespiratoryCirculatory * physiqueMod;
                    effects.BloodFiltration = Define.AdrenalineLow.RespiratoryCirculatory * physiqueMod;
                    effects.Metabolism = Define.AdrenalineLow.Metabolism * physiqueMod;
                    effects.PainReduction = Define.AdrenalineLow.PainReduction * physiqueMod;
                    effects.VisionReduction = visionHearingExempt ? 0 : Define.AdrenalineLow.VisionReduction * physiqueMod;
                    effects.HearingReduction = visionHearingExempt ? 0 : Define.AdrenalineLow.HearingReduction * physiqueMod;
                    
                    effects.MeleeDamage = Define.AdrenalineLow.MeleeDamage * physiqueMod;
                    effects.Dodge = Define.AdrenalineLow.Dodge * physiqueMod;
                    effects.MeleeHitReduction = meleeHitExempt ? 0 : Define.AdrenalineLow.MeleeHitReduction * physiqueMod;
                    
                    effects.RestMultiplier = Define.AdrenalineRestMultiplierLow;
                    break;

                case AdrenalineLevel.Medium:
                    effects.Consciousness = Define.AdrenalineMedium.Consciousness * physiqueMod;
                    effects.MoveSpeed = Define.AdrenalineMedium.MoveSpeed * physiqueMod;
                    effects.Respiratory = Define.AdrenalineMedium.RespiratoryCirculatory * physiqueMod;
                    effects.Circulation = Define.AdrenalineMedium.RespiratoryCirculatory * physiqueMod;
                    effects.BloodFiltration = Define.AdrenalineMedium.RespiratoryCirculatory * physiqueMod;
                    effects.Metabolism = Define.AdrenalineMedium.Metabolism * physiqueMod;
                    effects.PainReduction = Define.AdrenalineMedium.PainReduction * physiqueMod;
                    effects.VisionReduction = visionHearingExempt ? 0 : Define.AdrenalineMedium.VisionReduction * physiqueMod;
                    effects.HearingReduction = visionHearingExempt ? 0 : Define.AdrenalineMedium.HearingReduction * physiqueMod;
                    
                    effects.MeleeDamage = Define.AdrenalineMedium.MeleeDamage * physiqueMod;
                    effects.Dodge = Define.AdrenalineMedium.Dodge * physiqueMod;
                    effects.MeleeHitReduction = meleeHitExempt ? 0 : Define.AdrenalineMedium.MeleeHitReduction * physiqueMod;
                    
                    effects.RestMultiplier = Define.AdrenalineRestMultiplierMedium;
                    break;

                case AdrenalineLevel.High:
                    effects.Consciousness = Define.AdrenalineHigh.Consciousness * physiqueMod;
                    effects.MoveSpeed = Define.AdrenalineHigh.MoveSpeed * physiqueMod;
                    effects.Respiratory = Define.AdrenalineHigh.RespiratoryCirculatory * physiqueMod;
                    effects.Circulation = Define.AdrenalineHigh.RespiratoryCirculatory * physiqueMod;
                    effects.BloodFiltration = Define.AdrenalineHigh.RespiratoryCirculatory * physiqueMod;
                    effects.Metabolism = Define.AdrenalineHigh.Metabolism * physiqueMod;
                    effects.PainReduction = Define.AdrenalineHigh.PainReduction * physiqueMod;
                    effects.VisionReduction = visionHearingExempt ? 0 : Define.AdrenalineHigh.VisionReduction * physiqueMod;
                    effects.HearingReduction = visionHearingExempt ? 0 : Define.AdrenalineHigh.HearingReduction * physiqueMod;
                    
                    effects.MeleeDamage = Define.AdrenalineHigh.MeleeDamage * physiqueMod;
                    effects.Dodge = Define.AdrenalineHigh.Dodge * physiqueMod;
                    effects.MeleeHitReduction = meleeHitExempt ? 0 : Define.AdrenalineHigh.MeleeHitReduction * physiqueMod;
                    
                    effects.RestMultiplier = Define.AdrenalineRestMultiplierHigh;
                    break;
            }

            return effects;
        }

        public static void TryApplyOverexertDamage(Pawn pawn)
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null || adrenaline.Severity < Define.AdrenalineThresholdMedium)
                return;

            int physiqueLevel = GetPhysiqueLevel(pawn);
            if (physiqueLevel >= Define.PhysiqueAdrenalineExemptionThreshold)
                return;

            float chance = Define.AdrenalineOverexertBaseChance + 
                          Define.AdrenalineOverexertChancePerPhysique * (Define.PhysiqueAdrenalineExemptionThreshold - physiqueLevel);
            
            if (Rand.Value < chance)
            {
                ApplyRandomOverexertHediff(pawn, physiqueLevel);
            }
        }

        private static void ApplyRandomOverexertHediff(Pawn pawn, int physiqueLevel)
        {
            List<string> mildHediffs = new List<string> { "MuscleStrain", "JointSprain" };
            List<string> moderateHediffs = new List<string> { "TendonFatigue", "HeartPalpitations", "ShortnessOfBreath" };
            List<string> severeHediffs = new List<string> { "LimbWeakness" };

            float roll = Rand.Value;
            string hediffDefName;

            if (physiqueLevel < 5)
            {
                if (roll < 0.3f)
                    hediffDefName = severeHediffs.RandomElement();
                else if (roll < 0.6f)
                    hediffDefName = moderateHediffs.RandomElement();
                else
                    hediffDefName = mildHediffs.RandomElement();
            }
            else if (physiqueLevel < 8)
            {
                if (roll < 0.15f)
                    hediffDefName = severeHediffs.RandomElement();
                else if (roll < 0.5f)
                    hediffDefName = moderateHediffs.RandomElement();
                else
                    hediffDefName = mildHediffs.RandomElement();
            }
            else
            {
                if (roll < 0.05f)
                    hediffDefName = severeHediffs.RandomElement();
                else if (roll < 0.3f)
                    hediffDefName = moderateHediffs.RandomElement();
                else
                    hediffDefName = mildHediffs.RandomElement();
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamed(hediffDefName, false);
            if (hediffDef != null && pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                hediff.Severity = 0.3f + Rand.Value * 0.4f;
                pawn.health.AddHediff(hediff);
                Log.Message($"[Hormones] {pawn.Name?.ToStringFull ?? "Unknown"} suffered {hediffDef.label} from adrenaline overexertion");
            }
        }

        public static void AddAdrenaline(Pawn pawn, float amount)
        {
            HediffDef adrenalineDef = DefDatabase<HediffDef>.GetNamed("Adrenaline", false);
            if (adrenalineDef == null)
                return;

            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(adrenalineDef);
            if (adrenaline == null)
            {
                adrenaline = HediffMaker.MakeHediff(adrenalineDef, pawn);
                pawn.health.AddHediff(adrenaline);
            }

            adrenaline.Severity = Math.Min(adrenaline.Severity + amount, 1.0f);
        }

        public static void RemoveAdrenaline(Pawn pawn, float amount)
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null)
                return;

            adrenaline.Severity = Math.Max(adrenaline.Severity - amount, 0);
            if (adrenaline.Severity <= 0)
            {
                pawn.health.RemoveHediff(adrenaline);
            }
        }

        private static int GetPhysiqueLevel(Pawn pawn)
        {
            if (pawn == null) return 1;
            SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueSkillDef == null) return 1;
            SkillRecord skill = pawn.skills?.GetSkill(physiqueSkillDef);
            int level = skill?.levelInt ?? 1;
            return Helpers.Clamp(level, Define.PhysiqueMinLevel, Define.PhysiqueMaxLevel);
        }
    }

    public class AdrenalineEffects
    {
        public AdrenalineLevel Level = AdrenalineLevel.Dormant;
        public float PhysiqueModifier = 1.0f;
        
        public float Consciousness = 0;
        public float MoveSpeed = 0;
        public float Respiratory = 0;
        public float Circulation = 0;
        public float BloodFiltration = 0;
        public float Metabolism = 0;
        public float PainReduction = 0;
        public float VisionReduction = 0;
        public float HearingReduction = 0;
        
        public float MeleeDamage = 0;
        public float Dodge = 0;
        public float MeleeHitReduction = 0;
        
        public float RestMultiplier = 1.0f;

        public bool HasActiveEffects => Level != AdrenalineLevel.Dormant;
    }
}