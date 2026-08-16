using Verse;
using Verse.AI;
using RimWorld;
using System.Collections.Generic;

namespace Hormones
{
    public class Need_MuscleStrain : Need
    {
        public override int GUIChangeArrow
        {
            get
            {
                if (pawn.needs?.rest?.Resting == true || IsDoingJoy())
                {
                    return 1;
                }
                return 0;
            }
        }

        /// <summary>
        /// 判断 pawn 当前是否正在进行娱乐(Joy)活动。
        /// RimWorld 里娱乐类 Job 的 JobDef.joyKind != null（打桌球/看云/社交喝酒等全部命中）。
        /// </summary>
        private bool IsDoingJoy()
        {
            Job curJob = pawn.CurJob;
            return curJob != null && curJob.def != null && curJob.def.joyKind != null;
        }

        public override float MaxLevel => PhysiqueLgc.GetMuscleStrainMax(pawn);

        public override bool ShowOnNeedList => true;

        public Need_MuscleStrain(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float> { 0.3f, 0.7f };
        }

        // ===== 外置 Mod 接口：劳损积累速率 / 体魄经验的额外修正乘区 =====
        // 由外部 Mod（如 Function Drinks Expanded / 饮品拓展）在运行时通过下方方法设置，
        // 主 Mod 不再硬编码任何具体饮品 defName。
        // 约定：mul<1 降低速率，mul>1 提高速率；默认值 1.0 = 无影响。
        // 均为运行时瞬态值，不随存档持久化（外部 Mod 每次自行重设，重载后回退默认）。
        private float extraStrainRateMult = 1f;     // 劳损积累速率额外乘区
        private float extraPhysiqueXpMult = 1f;     // 体魄经验额外乘区

        /// <summary>外部 Mod 设置劳损积累速率额外乘区（默认 1.0，无影响）。饮品生效时可设 0.75f 表示降低 25%。</summary>
        public void SetExtraStrainRateMultiplier(float mult) => extraStrainRateMult = mult;
        /// <summary>外部 Mod 重置劳损积累速率额外乘区为默认 1.0。</summary>
        public void ResetExtraStrainRateMultiplier() => extraStrainRateMult = 1f;
        /// <summary>读取劳损积累速率额外乘区（内部/跨类使用）。</summary>
        public float GetExtraStrainRateMultiplier() => extraStrainRateMult;

        // 劳损【恢复】速率额外乘区（由饮品等外部系统设置；淡盐水 0.75 = 恢复效率-25%）。
        private float extraStrainRecoveryMult = 1f;
        /// <summary>外部 Mod 设置劳损恢复速率额外乘区（默认 1.0）。饮品如淡盐水可设 0.75f 表示恢复效率降低 25%。</summary>
        public void SetExtraStrainRecoveryMultiplier(float mult) => extraStrainRecoveryMult = mult;
        /// <summary>外部 Mod 重置劳损恢复速率额外乘区为默认 1.0。</summary>
        public void ResetExtraStrainRecoveryMultiplier() => extraStrainRecoveryMult = 1f;
        /// <summary>读取劳损恢复速率额外乘区（内部/跨类使用）。</summary>
        public float GetExtraStrainRecoveryMultiplier() => extraStrainRecoveryMult;

        /// <summary>外部 Mod 设置体魄经验额外乘区（默认 1.0，无影响）。饮品如果蔬汁可设 1.25f 表示 +25%。</summary>
        public void SetExtraPhysiqueXpMultiplier(float mult) => extraPhysiqueXpMult = mult;
        /// <summary>外部 Mod 重置体魄经验额外乘区为默认 1.0。</summary>
        public void ResetExtraPhysiqueXpMultiplier() => extraPhysiqueXpMult = 1f;
        /// <summary>读取体魄经验额外乘区（内部/跨类使用）。</summary>
        public float GetExtraPhysiqueXpMultiplier() => extraPhysiqueXpMult;

        public override void SetInitialLevel()
        {
            CurLevel = MaxLevel;
        }

        public override void NeedInterval()
        {
            if (!IsFrozen)
            {
                float before = CurLevel; // 变化事件基准值

                // 神经衰弱覆盖效应：仅作用于损耗【恢复】(数值下降)方向，积累方向不受影响
                float cover = PhysiqueLgc.GetStrainCoverEffect(pawn);

                if (pawn.needs?.rest?.Resting == true)
                {
                    // 睡觉：恢复速度与【休息效率】和【神经衰弱覆盖效应】挂钩
                    // 休息效率 = 床的 BedRestEffectiveness（破床/地面低、好床好房高）；地面睡取 valueIfMissing(0.8)
                    float restEff = pawn.CurrentBed()?.GetStatValue(StatDefOf.BedRestEffectiveness)
                                    ?? StatDefOf.BedRestEffectiveness.valueIfMissing;
                    float recoveryRate = PhysiqueLgc.GetMuscleStrainRecoveryRate(pawn) * restEff * cover;
                    CurLevel += recoveryRate / 25f;
                    if (CurLevel > MaxLevel) CurLevel = MaxLevel;
                }
                else if (IsDoingJoy())
                {
                    // 娱乐(打桌球/看云/喝酒等)：按比例恢复，受神经衰弱覆盖效应影响
                    float recoveryRate = PhysiqueLgc.GetMuscleStrainRecoveryRate(pawn) * Define.MuscleStrainJoyRecoveryFactor * cover;
                    CurLevel += recoveryRate / 25f;
                    if (CurLevel > MaxLevel) CurLevel = MaxLevel;
                }

                if (before != CurLevel)
                    NeedChangeEvents.FireStrainChanged(pawn, before, CurLevel);
            }
        }

        public void AddStrain(float amount)
        {
            // 注：劳损【积累】方向不施加 strainCoverEffect（该效应只减慢恢复，不减慢积累）。

            // 外置 Mod 接口：应用劳损积累速率额外修正乘区（默认 1.0 = 无影响）。
            // 由饮品拓展等 Mod 通过 SetExtraStrainRateMultiplier/Reset 设置，主 Mod 不硬编码任何饮品 defName。
            amount *= extraStrainRateMult;

            float before = CurLevel; // 变化事件基准值
            CurLevel -= amount;
            if (CurLevel < 0f) CurLevel = 0f;

            if (before != CurLevel)
                NeedChangeEvents.FireStrainChanged(pawn, before, CurLevel);
        }

        /// <summary>直接降低劳损值（饮品即时效果，如功能饮品 -25%）。不施加积累速率乘区，与 AddStrain 方向相反。</summary>
        public void ReduceStrain(float amount)
        {
            float before = CurLevel;
            CurLevel += amount;
            if (CurLevel > MaxLevel) CurLevel = MaxLevel;
            if (before != CurLevel)
                NeedChangeEvents.FireStrainChanged(pawn, before, CurLevel);
        }
    }
}