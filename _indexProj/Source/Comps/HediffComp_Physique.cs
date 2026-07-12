using RimWorld;
using Verse;

namespace Hormones
{
    public class HediffCompProperties_Physique : HediffCompProperties
    {
        public HediffCompProperties_Physique()
        {
            compClass = typeof(HediffComp_Physique);
        }
    }

    public class HediffComp_Physique : HediffComp
    {
        public HediffCompProperties_Physique Props => (HediffCompProperties_Physique)props;
    }
}