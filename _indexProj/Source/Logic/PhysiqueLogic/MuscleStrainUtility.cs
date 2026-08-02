using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using Hormones.UI;

namespace Hormones.Logic.PhysiqueLogic
{
    public static class MuscleStrainUtility
    {
        // 劳损飘字颜色（暖橙偏红，表示肌体透支）。可按需替换。
        public static Color StrainTextColor = new Color(1f, 0.55f, 0.35f);
        public static void TryAddMuscleStrain(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: pawn/health/hediffSet is null");
                return;
            }

            // 仅类人生物会产生肌肉劳损；动物/机械体排除。
            if (!PhysiqueLgc.IsHormoneSubject(pawn))
                return;

            BodyPartDef armDef = DefDatabase<BodyPartDef>.GetNamed("Arm", false);
            BodyPartDef legDef = DefDatabase<BodyPartDef>.GetNamed("Leg", false);

            Log.Message($"[Hormones] TryAddMuscleStrain: ArmDef={armDef?.defName}, LegDef={legDef?.defName}");

            List<BodyPartRecord> availableParts = new List<BodyPartRecord>();
            foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: part={part.def.defName}, label={part.Label}");
                if (armDef != null && part.def == armDef)
                {
                    availableParts.Add(part);
                }
                else if (legDef != null && part.def == legDef)
                {
                    availableParts.Add(part);
                }
            }

            if (availableParts.Count == 0)
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: No available limbs found");
                return;
            }

            BodyPartRecord targetPart = availableParts.RandomElement();
            Log.Message($"[Hormones] TryAddMuscleStrain: Selected part={targetPart.def.defName}, label={targetPart.Label}");

            // 【2026-07-29】仿生器官/假肢检查：若随机选中的肢体（或其祖先）已被人造部件替换，
            // 本次直接放弃、且【不重新随机】（义肢不产生肌肉劳损）。
            if (pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(targetPart))
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: target part {targetPart.Label} is artificial/bionic, skipped (no reroll)");
                return;
            }

            // 已整合进新损伤池：肌肉劳损统一使用 LaborMuscleStrain（0-1 连续 severity）
            HediffDef strainDef = DefDatabase<HediffDef>.GetNamed("LaborMuscleStrain", false);
            if (strainDef == null)
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: HediffDef LaborMuscleStrain not found");
                return;
            }

            Hediff existingHediff = null;
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                if (h.def == strainDef && h.Part == targetPart)
                {
                    existingHediff = h;
                    break;
                }
            }

            // LaborMuscleStrain 为 0-1 连续 severity（阶段边界 0 / 0.4 / 0.75）。
            // 每次触发累加 0.35，约 3 次达到重度封顶（1.0）。
            const float SeverityStep = 0.35f;
            if (existingHediff != null)
            {
                if (existingHediff.Severity < 1f)
                {
                    existingHediff.Severity = System.Math.Min(existingHediff.Severity + SeverityStep, 1f);
                    Log.Message($"[Hormones] TryAddMuscleStrain: Stacked! Severity={existingHediff.Severity} on {targetPart.Label}");
                }
                else
                {
                    Log.Message($"[Hormones] TryAddMuscleStrain: Max severity reached ({existingHediff.Severity}) on {targetPart.Label}");
                    return;
                }
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(strainDef, pawn, targetPart);
                hediff.Severity = SeverityStep;
                pawn.health.AddHediff(hediff, targetPart);
                Log.Message($"[Hormones] TryAddMuscleStrain: Success! Added LaborMuscleStrain to {targetPart.Label}");
            }

            if (pawn.Map != null && pawn.Position.IsValid)
            {
                ShowMuscleStrainText(pawn, targetPart.Label);
            }

            // 心情无需在此处理（2026-08-01 改版）：
            //   · MuscleStrainRest 已改为情景型（劳损储备 <10% 自动激活）；
            //   · 损伤心情由 StrainInjuryMood 情景型 thought 自动检测损伤 hediff 激活。
        }

        // 通过统一的 FlyTextMgr 显示劳损飘字：文本用池化 StringBuilder 拼接，颜色可替换。
        private static void ShowMuscleStrainText(Pawn pawn, string partLabel)
        {
            if (RimHormonesMod.Settings == null || !RimHormonesMod.Settings.ShowBodyDamageMotes) return;
            // 默认只显示玩家自己的殖民者；非玩家阵营需另开开关
            if (!RimHormonesMod.Settings.ShowEnemyBodyDamageMotes && pawn.Faction != Faction.OfPlayer) return;
            StringBuilder sb = FlyTextMgr.AcquireSB();
            sb.Append("肌肉拉伤: ").Append(partLabel);
            FlyTextMgr.Push(pawn, sb, StrainTextColor); // Push 会在内部归还 sb
        }
    }
}
