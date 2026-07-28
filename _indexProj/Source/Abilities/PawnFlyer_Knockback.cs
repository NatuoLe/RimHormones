using RimWorld;
using Verse;
using Verse.Sound;

namespace Hormones
{
    /// <summary>
    /// 击飞飞行器：敌人被击飞后，落地时受到眩晕 + 撞击伤害
    /// </summary>
    public class PawnFlyer_Knockback : PawnFlyer
    {
        public DamageDef impactDamageDef;
        public int impactDamageAmount;
        public float impactArmorPenetration;
        public int stunTicks;

        protected override void RespawnPawn()
        {
            // PawnFlyer 基础落地逻辑（放下 pawn、恢复征召状态、清理容器等）
            base.RespawnPawn();

            Pawn victim = FlyingPawn;
            if (victim == null || victim.Dead)
            {
                return;
            }

            // 落地眩晕
            if (stunTicks > 0)
            {
                victim.stances.stunner.StunFor(stunTicks, null, addBattleLog: false, showMote: false);
            }

            // 撞击伤害
            if (impactDamageAmount > 0 && impactDamageDef != null)
            {
                var dinfo = new DamageInfo(
                    impactDamageDef,
                    impactDamageAmount,
                    impactArmorPenetration,
                    -1f,
                    null,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    null
                );
                victim.TakeDamage(dinfo);
            }

            // 落地尘土特效
            FleckMaker.ThrowDustPuff(victim.DrawPos, victim.Map, 2f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref impactDamageDef, "impactDamageDef");
            Scribe_Values.Look(ref impactDamageAmount, "impactDamageAmount", 0);
            Scribe_Values.Look(ref impactArmorPenetration, "impactArmorPenetration", 0f);
            Scribe_Values.Look(ref stunTicks, "stunTicks", 0);
        }
    }
}
