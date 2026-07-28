using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Hormones
{
    /// <summary>
    /// 击飞技能 CompProperties：XML 可配置参数
    /// </summary>
    public class CompProperties_AbilityKnockback : CompProperties_AbilityEffect
    {
        // 击飞距离（默认 3 格）
        public int pushDistance = 3;

        // 落地撞击伤害
        public int impactDamage = 5;

        // 伤害穿甲
        public float armorPenetration = 0.1f;

        // 落地眩晕 tick 数（60 = 1 秒）
        public int stunTicks = 60;

        // 伤害类型（XML 中用 damageDef 字段，这里手动赋值 Blunt 兜底）
        public DamageDef damageDef;

        // 飞行特效（可选，用 null 则不显示）
        public EffecterDef flightEffecter;

        // 落地音效（可选）
        public SoundDef soundLanding;

        public CompProperties_AbilityKnockback()
        {
            compClass = typeof(CompAbilityEffect_Knockback);
        }
    }

    /// <summary>
    /// 击飞技能效果：选中敌人 → 沿连线方向推飞 → PawnFlyer 飞行 → 落地眩晕+伤害
    /// 冷却由 AbilityDef.cooldownTicksRange 统一管理，此处不自行计时。
    /// </summary>
    public class CompAbilityEffect_Knockback : CompAbilityEffect
    {
        public new CompProperties_AbilityKnockback Props => (CompProperties_AbilityKnockback)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            if (victim == null || victim == caster)
                return;
            if (victim.Dead || victim.Downed)
                return;

            Map map = caster.Map;

            // ===== 1. 计算推飞方向（沿 caster → victim 连线向外） =====
            Vector3 pushDir = (victim.DrawPos - caster.DrawPos).normalized;
            if (pushDir.sqrMagnitude < 0.001f)
            {
                // 施法者和目标重合，取随机方向
                float angle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                pushDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            // ===== 2. 从远到近找合法落地格 =====
            IntVec3 landingCell = FindLandingCell(victim, pushDir, Props.pushDistance, map);
            if (!landingCell.IsValid || landingCell == victim.Position)
            {
                // 没有合法落地格，仍然眩晕 + 伤害但原地不动
                ApplyDirectImpact(victim);
                return;
            }

            // ===== 3. 构建击飞飞行器 =====
            ThingDef flyerDef = DefDatabase<ThingDef>.GetNamed("Hormones_KnockbackFlyer");
            if (flyerDef == null)
            {
                Log.Error("[RimHormones] KnockbackFlyer ThingDef not found: Hormones_KnockbackFlyer");
                ApplyDirectImpact(victim);
                return;
            }

            PawnFlyer_Knockback flyer = (PawnFlyer_Knockback)PawnFlyer.MakeFlyer(
                flyerDef,
                victim,
                landingCell,
                Props.flightEffecter,
                Props.soundLanding,
                flyWithCarriedThing: false,
                overrideStartVec: null,
                triggeringAbility: parent,
                target: target
            );

            if (flyer != null)
            {
                flyer.impactDamageDef = Props.damageDef ?? DamageDefOf.Blunt;
                flyer.impactDamageAmount = Props.impactDamage;
                flyer.impactArmorPenetration = Props.armorPenetration;
                flyer.stunTicks = Props.stunTicks;

                GenSpawn.Spawn(flyer, landingCell, map);

                // ===== 4. 施法者脚下尘土特效 =====
                FleckMaker.ThrowDustPuff(caster.DrawPos, map, 1.5f);
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!base.CanApplyOn(target, dest))
                return false;

            Pawn victim = target.Pawn;
            if (victim == null)
                return false;
            if (victim == parent.pawn)
                return false;
            if (victim.Dead)
                return false;

            return true;
        }

        /// <summary>
        /// 从远到近搜索合法落地格（避免推到墙外）
        /// </summary>
        private IntVec3 FindLandingCell(Pawn victim, Vector3 pushDir, int maxDist, Map map)
        {
            for (int d = maxDist; d >= 1; d--)
            {
                IntVec3 candidate = victim.Position +
                    new IntVec3(
                        Mathf.RoundToInt(pushDir.x * d),
                        0,
                        Mathf.RoundToInt(pushDir.z * d)
                    );

                if (candidate.InBounds(map) && JumpUtility.ValidJumpTarget(victim, map, candidate))
                {
                    return candidate;
                }
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// 无合法落地格时的兜底：原地眩晕+伤害
        /// </summary>
        private void ApplyDirectImpact(Pawn victim)
        {
            victim.stances.stunner.StunFor(Props.stunTicks, null, addBattleLog: false, showMote: false);

            if (Props.impactDamage > 0)
            {
                var dinfo = new DamageInfo(
                    Props.damageDef ?? DamageDefOf.Blunt,
                    Props.impactDamage,
                    Props.armorPenetration,
                    -1f,
                    null,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    null
                );
                victim.TakeDamage(dinfo);
            }
        }
    }
}
