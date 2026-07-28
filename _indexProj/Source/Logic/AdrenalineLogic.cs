using Verse;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;

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

            int physiqueLevel = PhysiqueLgc.GetPhysiqueLevel(pawn);
            float physiqueMod = PhysiqueLgc.GetAdrenalinePhysiqueModifier(pawn);
            bool visionHearingExempt = PhysiqueLgc.IsAdrenalineExempt(pawn);
            bool meleeHitExempt = PhysiqueLgc.IsAdrenalineExempt(pawn);

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

        // chanceMultiplier：透支概率倍率。近战传 1.0（默认），射击传较小值（见 Define.AdrenalineRangedOverexertChanceMultiplier）。
        public static void TryApplyOverexertDamage(Pawn pawn, float chanceMultiplier = 1f)
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null || adrenaline.Severity < Define.AdrenalineThresholdMedium)
                return;

            int physiqueLevel = PhysiqueLgc.GetPhysiqueLevel(pawn);
            if (PhysiqueLgc.IsAdrenalineExempt(pawn))
                return;

            float chance = (Define.AdrenalineOverexertBaseChance + 
                          Define.AdrenalineOverexertChancePerPhysique * (Define.PhysiqueAdrenalineExemptionThreshold - physiqueLevel))
                          * chanceMultiplier;
            
            if (Rand.Value < chance)
            {
                ApplyRandomOverexertHediff(pawn, physiqueLevel);
            }
        }

        private static void ApplyRandomOverexertHediff(Pawn pawn, int physiqueLevel)
        {
            // 已整合进新损伤池（见 文档/Hediff/身体损伤.md「现有 Hediff 整合」表）：
            //   轻度 ← 肌肉/关节劳损；中度 ← 心肺透支；重度 ← 神经耗竭/坠落复合损伤
            List<string> mildHediffs = new List<string> { "LaborMuscleStrain", "CombatJointStrain" };
            List<string> moderateHediffs = new List<string> { "CardioOverexert", "SuffocationStrain" };
            List<string> severeHediffs = new List<string> { "CombatEnduranceExhaust", "FallJointStrain" };

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
            if (hediffDef == null) return;

            // 【2026-07-28 修复】按 defName 解析目标器官/肢体，避免损伤全部加到全身。
            // targetPart 为 null 时（部件缺失或无映射）回退为全身附着（安全）。
            BodyPartRecord targetPart = FindTargetPart(pawn, hediffDefName);

            // 按“部件”判重（同一器官/肢体不重复叠新 Hediff；左右肢体可各自独立）。
            bool alreadyHas = false;
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                if (h.def == hediffDef && h.Part == targetPart)
                {
                    alreadyHas = true;
                    break;
                }
            }
            if (alreadyHas) return;

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn, targetPart);
            hediff.Severity = 0.3f + Rand.Value * 0.4f;
            pawn.health.AddHediff(hediff, targetPart);
            Log.Message($"[Hormones] {pawn.Name?.ToStringFull ?? "Unknown"} suffered {hediffDef.label} on {(targetPart != null ? targetPart.Label : "whole body")} from adrenaline overexertion");
        }

        // ============================================================
        // 损伤 defName → 目标身体部件映射（与 Defs/HediffDefs/Hediff_StrainPool.xml 的器官注释一致）。
        //   多个候选部件（如 Arm/Leg、Eye/Ear）时随机取一个存在的部件；左右独立。
        //   找不到映射或部件缺失 → 返回 null（回退全身附着）。
        // ============================================================
        private static readonly Dictionary<string, string[]> HediffTargetParts = new Dictionary<string, string[]>
        {
            { "LaborMuscleStrain",      new[] { "Arm", "Leg" } },
            { "DiggingMuscleStrain",    new[] { "Arm" } },
            { "CardioOverexert",        new[] { "Heart" } },
            { "SuffocationStrain",      new[] { "Lung" } },
            { "CombatJointStrain",      new[] { "Arm", "Hand", "Foot" } },
            { "FallJointStrain",        new[] { "Leg" } },
            { "CombatEnduranceExhaust", new[] { "Brain" } },
            { "MetabolicExhaust",       new[] { "Stomach", "Liver" } },
            { "SensoryOverload",        new[] { "Eye", "Ear" } },
        };

        private static BodyPartRecord FindTargetPart(Pawn pawn, string hediffDefName)
        {
            if (pawn?.health?.hediffSet == null) return null;
            if (!HediffTargetParts.TryGetValue(hediffDefName, out string[] partNames)) return null;

            List<BodyPartRecord> candidates = new List<BodyPartRecord>();
            foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
            {
                foreach (var pn in partNames)
                {
                    if (part.def.defName == pn)
                    {
                        candidates.Add(part);
                        break;
                    }
                }
            }
            return candidates.Count > 0 ? candidates.RandomElement() : null;
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

        /// <summary>
        /// 显示肾上腺素飘字（Mote）。受 RimHormonesMod.Settings.ShowAdrenalineMotes 控制。
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <param name="reason">归因标签，如 "受伤""近战""远程"</param>
        /// <param name="change">原始变化量（Severity 尺度，0~1）</param>
        /// <param name="newSeverity">变化后的 Severity</param>
        public static void ShowAdrenalineMote(Pawn pawn, string reason, float change, float newSeverity)
        {
            if (!RimHormonesMod.Settings.ShowAdrenalineMotes)
                return;
            if (pawn?.Map == null)
                return;

            int changeDisplay = Mathf.RoundToInt(Mathf.Abs(change) * 100f);
            int currentDisplay = Mathf.RoundToInt(newSeverity * 100f);
            string sign = change >= 0 ? "+" : "-";
            string text = $"肾上腺[{reason}]：{sign}{changeDisplay} [{currentDisplay}/100]";

            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, text, new Color(1f, 0.7f, 0.2f), -1f);
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