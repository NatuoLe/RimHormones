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

        public override void SetInitialLevel()
        {
            CurLevel = MaxLevel;
        }

        public override void NeedInterval()
        {
            if (!IsFrozen)
            {
                if (pawn.needs?.rest?.Resting == true)
                {
                    // 睡觉：全速恢复
                    float recoveryRate = PhysiqueLgc.GetMuscleStrainRecoveryRate(pawn);
                    CurLevel += recoveryRate / 25f;
                    if (CurLevel > MaxLevel) CurLevel = MaxLevel;
                }
                else if (IsDoingJoy())
                {
                    // 娱乐(打桌球/看云/喝酒等)：按睡觉恢复速率的一定比例恢复
                    float recoveryRate = PhysiqueLgc.GetMuscleStrainRecoveryRate(pawn) * Define.MuscleStrainJoyRecoveryFactor;
                    CurLevel += recoveryRate / 25f;
                    if (CurLevel > MaxLevel) CurLevel = MaxLevel;
                }
            }
        }

        public void AddStrain(float amount)
        {
            // 检查是否有饮品Buff (劳损积累降低)
            if (pawn.health != null)
            {
                Hediff electrolyteBuff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("DrinkElectrolyte"));
                Hediff energyBuff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("DrinkEnergyDrink"));
                Hediff fruitJuiceBuff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("DrinkFruitJuice"));

                if (fruitJuiceBuff != null)
                {
                    amount *= 0.7f; // 果蔬汁降低30%
                }
                else if (electrolyteBuff != null || energyBuff != null)
                {
                    amount *= 0.75f; // 电解质水/能量饮品降低25%
                }
            }

            CurLevel -= amount;
            if (CurLevel < 0f) CurLevel = 0f;
        }
    }
}