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

        // 【二进制兼容垫片，勿删】0.4.2 给下方方法加了 chanceMultiplier 参数后，
        // IL 层面"单参数签名"即不复存在；旧版 RimHormonesCE.dll 等已编译程序集
        // 仍按精确签名查找 void TryApplyOverexertDamage(Pawn)，找不到就抛
        // MissingMethodException。保留此单参重载做转发，旧依赖 DLL 无需重编译即可工作。
        public static void TryApplyOverexertDamage(Pawn pawn)
        {
            TryApplyOverexertDamage(pawn, 1f);
        }

        // chanceMultiplier：透支概率倍率。近战传 1.0，射击传较小值（见 Define.AdrenalineRangedOverexertChanceMultiplier）。
        // 【二进制兼容】旧签名保留：默认按「近战」触发源处理，行为与历史版本一致。
        public static void TryApplyOverexertDamage(Pawn pawn, float chanceMultiplier)
        {
            TryApplyOverexertDamage(pawn, chanceMultiplier, StrainTriggerSource.Melee);
        }

        /// <summary>
        /// 战斗透支损伤判定（2026-08-02 重写：损伤池按触发源标签动态筛选）。
        /// 概率逻辑与历史版本完全一致，触发源只决定「可抽哪些损伤」。
        /// </summary>
        /// <param name="source">触发源：射击 / 近战 / 被近战命中，对应 HediffDef 的 StrainHediffExt 标签。</param>
        public static void TryApplyOverexertDamage(Pawn pawn, float chanceMultiplier, StrainTriggerSource source)
        {
            // 仅类人生物会因肾上腺素透支而受身体损伤；动物/机械体排除。
            if (!PhysiqueLgc.IsHormoneSubject(pawn))
                return;
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            // 【2026-08-02】内侧门槛按触发源区分：射击 Low(≥0.15)，近战/被近战维持 Low 档下限 0.5。
            // 外层 patch 已先按 effects.Level 过滤（射击 Low / 近战 High），此处为兜底。
            float minSeverity = source == StrainTriggerSource.Shoot
                ? Define.AdrenalineThresholdDormant
                : Define.AdrenalineThresholdLow;
            if (adrenaline == null || adrenaline.Severity < minSeverity)
                return;

            int physiqueLevel = PhysiqueLgc.GetPhysiqueLevel(pawn);
            if (PhysiqueLgc.IsAdrenalineExempt(pawn))
                return;

            float chance = (Define.AdrenalineOverexertBaseChance + 
                          Define.AdrenalineOverexertChancePerPhysique * (Define.PhysiqueAdrenalineExemptionThreshold - physiqueLevel))
                          * chanceMultiplier;
            
            if (Rand.Value < chance)
            {
                ApplyRandomOverexertHediff(pawn, physiqueLevel, source);
            }
        }

        /// <summary>
        /// 肾上腺素长期堆积损伤判定（2026-08-02 新增）。
        /// 与「战斗透支」不同：不依赖攻击动作，而是持续处于高肾上腺素状态时
        /// 每 Define.AdrenalineBuildupCheckIntervalTicks（600t=10 游戏秒）检测一次。
        ///   概率 = (Base − PerPhysique × 体魄) × 阶段倍率 × 玩家总倍率
        ///   体魄 0 → 1.00%/次；体魄 12 → 0.30%/次；体魄 ≥13 豁免。
        /// 阶段 → 档位映射（Low 不触发 / Medium→中度 / High→重度）读自
        /// Defs/MiscDefs/StrainAdrenalineStageRules.xml。
        /// </summary>
        public static void TryApplyAdrenalineBuildupDamage(Pawn pawn)
        {
            // 仅类人生物；动物/机械体排除。
            if (!PhysiqueLgc.IsHormoneSubject(pawn)) return;

            Hediff adrenaline = pawn.health?.hediffSet?.GetFirstHediffOfDef(
                DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            if (adrenaline == null) return;

            // 体魄 ≥13 豁免（与战斗透支一致）
            if (PhysiqueLgc.IsAdrenalineExempt(pawn)) return;

            // 当前肾上腺素阶段 → 该阶段的规则（是否触发 / 抽哪一档）
            AdrenalineLevel level = GetAdrenalineLevel(adrenaline.Severity);
            StrainAdrenalineStageRule rule = StrainHediffTags.StageRules?.RuleFor(level);
            if (rule == null || !rule.enabled) return;

            // 玩家可调总倍率（0=关闭）
            float globalMult = RimHormonesMod.Settings != null
                ? RimHormonesMod.Settings.AdrenalineBuildupGlobalMult
                : Define.DefaultAdrenalineBuildupGlobalMult;
            if (globalMult <= 0f) return;

            int physiqueLevel = PhysiqueLgc.GetPhysiqueLevel(pawn);
            float chance = (Define.AdrenalineBuildupBaseChance
                            - Define.AdrenalineBuildupChancePerPhysique * physiqueLevel)
                           * rule.chanceFactor * globalMult;
            if (chance <= 0f) return;

            if (Rand.Value >= chance) return;

            // 在「长期堆积」池内、仅取该阶段对应档位的损伤
            List<HediffDef> pool = StrainHediffTags.GetPool(StrainTriggerSource.AdrenalineBuildup);
            List<HediffDef> tierPool = new List<HediffDef>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (StrainHediffTags.GetTier(pool[i]) == rule.tier) tierPool.Add(pool[i]);
            }
            if (tierPool.Count == 0) return; // 该档位无可抽损伤（不回退，严格按阶段）

            ApplyOverexertHediff(pawn, tierPool.RandomElement());
        }

        private static void ApplyRandomOverexertHediff(Pawn pawn, int physiqueLevel, StrainTriggerSource source)
        {
            // 【2026-08-02 重写】损伤池不再硬编码，改为按触发源标签动态筛选：
            // 只有 HediffDef 上挂了 StrainHediffExt 且 combatTriggered=true、
            // 并响应当前触发源（onShoot/onMelee/onMeleeHitTaken）的损伤才进池。
            // 标签配置见 Defs/HediffDefs/Hediff_StrainPool.xml，
            // 对应「文档/Hediff/损伤Hediff与触发逻辑.xlsx」→ Hediff总览 的关键词列。
            List<HediffDef> pool = StrainHediffTags.GetPool(source);
            if (pool.Count == 0) return; // 该触发源无可抽损伤

            // 轻/中/重分档仍按体魄决定比例；在【当前池】内按 severityTier 分组抽取。
            // 为兼容既有平衡，分档比例与历史版本一致。
            HediffDef hediffDef = PickByPhysiqueTier(pool, physiqueLevel);
            if (hediffDef == null) return;
            ApplyOverexertHediff(pawn, hediffDef);
        }

        /// <summary>
        /// 把指定透支损伤附着到 pawn（部件映射 / 义体跳过 / 按部件判重 / 飘字）。
        /// 战斗透支与长期堆积共用此落地流程。
        /// </summary>
        private static void ApplyOverexertHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (hediffDef == null) return;
            string hediffDefName = hediffDef.defName;

            // 【2026-07-28 修复】按 defName 解析目标器官/肢体，避免损伤全部加到全身。
            // targetPart 为 null 时（部件缺失或无映射）回退为全身附着（安全）。
            BodyPartRecord targetPart = FindTargetPart(pawn, hediffDefName);

            // 【2026-07-29】仿生器官/假肢检查：若随机选中的目标部件（或其祖先）已被人造部件替换，
            // 本次直接放弃添加，且【不重新随机】（义体不受生理透支损伤影响）。
            // targetPart 为 null（全身附着）时不适用此检查。
            if (targetPart != null && pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(targetPart))
            {
                Log.Message($"[Hormones] {hediffDef.label} skipped: target part {targetPart.Label} is an artificial/bionic part (no reroll)");
                return;
            }

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

            // 透支损伤飘字：按轻/中/重档区分颜色（轻=黄 / 中=橙 / 重=红），颜色可替换。
            // 档位读自该 Hediff 的 XML 标签 StrainHediffExt.severityTier。
            StrainSeverityTier tierOf = StrainHediffTags.GetTier(hediffDef);
            UnityEngine.Color tier =
                tierOf == StrainSeverityTier.Severe   ? OverexertSevereColor :
                tierOf == StrainSeverityTier.Moderate ? OverexertModerateColor :
                                                        OverexertMildColor;
            ShowOverexertText(pawn, hediffDef.LabelCap, targetPart, tier);
        }

        /// <summary>
        /// 在给定池内按体魄档决定轻/中/重比例并抽一个损伤。
        /// 【2026-08-02 全 XML 配置化】
        ///   · 各损伤属于哪一档 ← HediffDef 的 StrainHediffExt.severityTier
        ///   · 各体魄档的抽取比例 ← Defs/MiscDefs/StrainTierRules.xml
        /// 若抽中的档位在当前池内为空，则依次向更轻的档位回退（保证一定抽到）。
        /// </summary>
        private static HediffDef PickByPhysiqueTier(List<HediffDef> pool, int physiqueLevel)
        {
            List<HediffDef> severe = new List<HediffDef>();
            List<HediffDef> moderate = new List<HediffDef>();
            List<HediffDef> mild = new List<HediffDef>();
            foreach (HediffDef d in pool)
            {
                switch (StrainHediffTags.GetTier(d))
                {
                    case StrainSeverityTier.Severe:   severe.Add(d);   break;
                    case StrainSeverityTier.Moderate: moderate.Add(d); break;
                    default:                          mild.Add(d);     break;
                }
            }

            // 比例读 XML；缺失时回退到内置默认（与历史平衡一致）。
            float severeCut, moderateCut;
            StrainTierRule rule = StrainHediffTags.TierRules?.RuleFor(physiqueLevel);
            if (rule != null)
            {
                severeCut = rule.severeChance;
                moderateCut = rule.moderateChance;
            }
            else if (physiqueLevel < 5)  { severeCut = 0.30f; moderateCut = 0.60f; }
            else if (physiqueLevel < 8)  { severeCut = 0.15f; moderateCut = 0.50f; }
            else                         { severeCut = 0.05f; moderateCut = 0.30f; }

            float roll = Rand.Value;
            if (roll < severeCut && severe.Count > 0) return severe.RandomElement();
            if (roll < moderateCut && moderate.Count > 0) return moderate.RandomElement();
            if (mild.Count > 0) return mild.RandomElement();
            // 轻度档为空时的回退：优先中度，再重度
            if (moderate.Count > 0) return moderate.RandomElement();
            if (severe.Count > 0) return severe.RandomElement();
            return null;
        }

        // 透支飘字分档颜色（可按需替换）。
        public static UnityEngine.Color OverexertMildColor     = new UnityEngine.Color(1f, 0.85f, 0.3f);  // 黄
        public static UnityEngine.Color OverexertModerateColor = new UnityEngine.Color(1f, 0.55f, 0.2f);  // 橙
        public static UnityEngine.Color OverexertSevereColor   = new UnityEngine.Color(1f, 0.3f, 0.25f);  // 红

        // 通过统一 FlyTextMgr 显示透支损伤飘字：池化 StringBuilder 拼接，颜色可替换。
        private static void ShowOverexertText(Pawn pawn, string label, BodyPartRecord part, UnityEngine.Color color)
        {
            if (RimHormonesMod.Settings == null || !RimHormonesMod.Settings.ShowBodyDamageMotes) return;
            // 默认只显示玩家自己的殖民者；非玩家阵营需另开开关
            if (!RimHormonesMod.Settings.ShowEnemyBodyDamageMotes && pawn.Faction != Faction.OfPlayer) return;
            System.Text.StringBuilder sb = Hormones.UI.FlyTextMgr.AcquireSB();
            sb.Append(label);
            if (part != null)
            {
                sb.Append(": ").Append(part.Label);
            }
            Hormones.UI.FlyTextMgr.Push(pawn, sb, color); // Push 内部归还 sb
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
            { "VisualStrain",           new[] { "Eye" } },
            { "AuditoryStrain",         new[] { "Ear" } },
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