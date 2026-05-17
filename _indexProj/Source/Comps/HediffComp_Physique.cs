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

        private float lastEfficiency = 1f;

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent.pawn;
            if (pawn == null) return;

            float newEfficiency = HormonesLogic.GetWorkEfficiency(pawn);

            if (newEfficiency != lastEfficiency)
            {
                UpdateWorkEfficiency(newEfficiency);
                lastEfficiency = newEfficiency;
            }
        }

        private void UpdateWorkEfficiency(float efficiency)
        {
            HediffStage stage = parent.def.stages[0];
            if (stage.statFactors != null && stage.statFactors.ContainsKey(StatDefOf.WorkSpeedGlobal))
            {
                stage.statFactors[StatDefOf.WorkSpeedGlobal] = efficiency;
            }
            else
            {
                stage.statFactors = new System.Collections.Generic.Dictionary<StatDef, float>();
                stage.statFactors.Add(StatDefOf.WorkSpeedGlobal, efficiency);
            }

            parent.pawn.health.Notify_HediffChanged(parent);
            Log.Message($"[Hormones] Updated work efficiency for {parent.pawn.Name?.ToStringFull ?? "Unknown"}: {efficiency:F2}");
        }
    }
}