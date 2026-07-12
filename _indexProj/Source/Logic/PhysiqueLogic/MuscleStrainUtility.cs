using System.Collections.Generic;
using Verse;
using RimWorld;

namespace Hormones.Logic.PhysiqueLogic
{
    public static class MuscleStrainUtility
    {
        public static void TryAddMuscleStrain(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: pawn/health/hediffSet is null");
                return;
            }

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

            HediffDef strainDef = DefDatabase<HediffDef>.GetNamed("MuscleStrainHediff", false);
            if (strainDef == null)
            {
                Log.Message($"[Hormones] TryAddMuscleStrain: HediffDef MuscleStrainHediff not found");
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

            if (existingHediff != null)
            {
                if (existingHediff.Severity < Define.MuscleStrainMaxSeverity)
                {
                    existingHediff.Severity += 1f;
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
                hediff.Severity = 1f;
                pawn.health.AddHediff(hediff, targetPart);
                Log.Message($"[Hormones] TryAddMuscleStrain: Success! Added MuscleStrain to {targetPart.Label}");
            }

            if (pawn.Map != null && pawn.Position.IsValid)
            {
                ShowMuscleStrainText(pawn.Position, pawn.Map, targetPart.Label);
            }
        }

        private static void ShowMuscleStrainText(IntVec3 pos, Map map, string partLabel)
        {
            MoteText moteText = (MoteText)ThingMaker.MakeThing(ThingDefOf.Mote_Text);
            object vector3 = PhysiqueDatas.GetVector3(pos.x + 0.5f, 0.5f, pos.z + 0.5f);
            System.Reflection.FieldInfo field = typeof(MoteText).GetField("exactPosition");
            if (field != null)
            {
                field.SetValue(moteText, vector3);
                moteText.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));
                moteText.text = $"肌肉拉伤: {partLabel}";
                GenSpawn.Spawn(moteText, pos, map);
                PhysiqueDatas.ReturnVector3(vector3);
            }
        }
    }
}
