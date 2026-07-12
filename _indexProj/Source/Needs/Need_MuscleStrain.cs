using Verse;
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
                if (pawn.needs?.rest?.Resting == true)
                {
                    return 1;
                }
                return 0;
            }
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
                    float recoveryRate = PhysiqueLgc.GetMuscleStrainRecoveryRate(pawn);
                    CurLevel += recoveryRate / 25f;
                    if (CurLevel > MaxLevel) CurLevel = MaxLevel;
                }
            }
        }

        public void AddStrain(float amount)
        {
            CurLevel -= amount;
            if (CurLevel < 0f) CurLevel = 0f;
        }
    }
}