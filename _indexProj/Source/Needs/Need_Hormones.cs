using Verse;
using RimWorld;
using System.Collections.Generic;

namespace Hormones
{
    public class Need_Hormones : Need
    {
        public override int GUIChangeArrow
        {
            get
            {
                if (HormonesComp != null)
                {
                    if (CurLevelPercentage < HormonesComp.LastLevelPercentage)
                    {
                        return -1;
                    }
                    if (CurLevelPercentage > HormonesComp.LastLevelPercentage)
                    {
                        return 1;
                    }
                }
                return 0;
            }
        }

        public override float MaxLevel => 100f;

        public override bool ShowOnNeedList => true;

        private HormonesComponent HormonesComp => pawn.GetComp<HormonesComponent>();

        public HormonesStatus HormonesStatus => HormonesComp?.Status ?? HormonesStatus.Normal;

        public Need_Hormones(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float> { 0.2f, 0.5f, 0.8f };
        }

        public override void SetInitialLevel()
        {
            if (HormonesComp != null)
            {
                CurLevel = HormonesComp.CurLevel;
            }
            else
            {
                CurLevel = MaxLevel;
            }
        }

        public override void NeedInterval()
        {
            if (HormonesComp != null)
            {
                CurLevel = HormonesComp.CurLevel;
            }
        }
    }
}