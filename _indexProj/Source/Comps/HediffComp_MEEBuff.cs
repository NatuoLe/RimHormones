using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// MEE 饮品 Buff 组件（主 mod）。
    /// 挂在 12h 限时 Hediff 上，由 Hediff 的增删生命周期经主 mod 既有的「饮品解耦接口」施加/撤销效果，
    /// 主 mod 不硬编码任何具体饮品 defName：
    ///   - cortisolDecayPerInterval   → Need_Cortisol.SetExtraCortisolDecay（奶茶：-3.25 ≈ -13%/日）
    ///   - strainRecoveryMult         → Need_MuscleStrain.SetExtraStrainRecoveryMultiplier（淡盐水：0.75 = 恢复效率-25%）
    ///   - physiqueXpMult             → PhysiqueXpMultUtility.SetExtraXpMult（果蔬汁：1.25 = +25%）
    ///   - moodThought                → 摄入时经本组件 TryGainMemory 施加的记忆心情（功能饮品+1/果蔬汁-3/奶茶+3，持续 12h）
    /// 注意：HediffStage 没有 baseMoodEffect 字段（那是 ThoughtStage 的），饮品心情不能写在 stage 里，必须用 ThoughtDef 记忆。
    /// </summary>
    public class CompProperties_MEEBuff : HediffCompProperties
    {
        /// <summary>每 150 tick 区间的额外皮质醇衰减（点数）。-3.25 ≈ -13%/日（MaxLevel=10000）。0 = 不生效。</summary>
        public float cortisolDecayPerInterval = 0f;

        /// <summary>劳损恢复速率额外乘区。0.75 = 恢复效率降低 25%。1 = 不生效。</summary>
        public float strainRecoveryMult = 1f;

        /// <summary>体魄经验额外乘区。1.25 = +25%。1 = 不生效。</summary>
        public float physiqueXpMult = 1f;

        /// <summary>摄入时施加的记忆心情 ThoughtDef（如 功能饮品+1/果蔬汁-3/奶茶+3）。null = 不施加心情。</summary>
        public ThoughtDef moodThought;

        public CompProperties_MEEBuff()
        {
            compClass = typeof(HediffComp_MEEBuff);
        }
    }

    public class HediffComp_MEEBuff : HediffComp
    {
        public CompProperties_MEEBuff Props => (CompProperties_MEEBuff)props;

        public override void CompPostPostAdd(DamageInfo? dinfo) => Apply(true);

        public override void CompPostPostRemoved() => Apply(false);

        private void Apply(bool on)
        {
            Pawn p = parent?.pawn;
            if (p == null) return;

            if (Props.cortisolDecayPerInterval != 0f)
            {
                Need_Cortisol c = p.needs?.TryGetNeed<Need_Cortisol>();
                if (c != null)
                {
                    if (on) c.SetExtraCortisolDecay(Props.cortisolDecayPerInterval);
                    else c.ResetExtraCortisolDecay();
                }
            }

            if (Props.strainRecoveryMult != 1f)
            {
                Need_MuscleStrain s = p.needs?.TryGetNeed<Need_MuscleStrain>();
                if (s != null)
                {
                    if (on) s.SetExtraStrainRecoveryMultiplier(Props.strainRecoveryMult);
                    else s.ResetExtraStrainRecoveryMultiplier();
                }
            }

            if (Props.physiqueXpMult != 1f)
            {
                if (on) PhysiqueXpMultUtility.SetExtraXpMult(p, Props.physiqueXpMult);
                else PhysiqueXpMultUtility.ResetExtraXpMult(p);
            }

            if (on && Props.moodThought != null)
            {
                p.needs?.mood?.thoughts?.memories?.TryGainMemory(Props.moodThought);
            }
        }
    }
}
