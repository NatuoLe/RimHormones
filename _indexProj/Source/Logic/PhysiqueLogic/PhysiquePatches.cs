using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Hormones.Logic.PhysiqueLogic
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class Patch_Job_End_PhysiqueXP
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn_JobTracker __instance, JobCondition condition, ref Job __state)
        {
            if (condition == JobCondition.Succeeded)
            {
                __state = __instance.curJob;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn_JobTracker __instance, JobCondition condition, Job __state)
        {
            if (condition != JobCondition.Succeeded) return;

            Pawn pawn = GetPawn(__instance);
            if (pawn == null) return;

            Job curJob = __state;
            if (curJob == null) return;

            if (curJob.def == null) return;

            float physiqueXP = 0f;
            float muscleStrainAmount = 0f;
            float strainChance = 0f;

            string jobDefName = curJob.def.defName;

            switch (jobDefName)
            {
                case "Mine":
                    physiqueXP = Define.MiningXP;
                    muscleStrainAmount = Define.MiningMuscleStrain;
                    strainChance = Define.MiningStrainChance;
                    break;
                case "CutPlant":
                case "CutPlantDesignated":
                    {
                        Thing targetThing = curJob.GetTarget(TargetIndex.A).Thing;
                        bool isTree = targetThing != null && targetThing.def.plant != null && targetThing.def.plant.IsTree;
                        if (isTree)
                        {
                            physiqueXP = Define.TreeCutXP;
                            muscleStrainAmount = Define.TreeCutMuscleStrain;
                            strainChance = Define.TreeCutStrainChance;
                        }
                        else
                        {
                            physiqueXP = Define.PlantCutXP;
                            muscleStrainAmount = Define.PlantCutMuscleStrain;
                            strainChance = Define.PlantCutStrainChance;
                        }
                        break;
                    }
                case "Harvest":
                case "HarvestDesignated":
                    physiqueXP = Define.HarvestXP;
                    muscleStrainAmount = Define.HarvestMuscleStrain;
                    strainChance = Define.HarvestStrainChance;
                    break;
                case "Slaughter":
                    physiqueXP = Define.ButcherXP;
                    muscleStrainAmount = Define.ButcherMuscleStrain;
                    strainChance = Define.ButcherStrainChance;
                    break;
                case "HaulToCell":
                case "HaulToContainer":
                case "HaulToStorage":
                case "HaulToCaravan":
                case "HaulToTransporter":
                    physiqueXP = Define.HaulXP;
                    muscleStrainAmount = Define.HaulMuscleStrain;
                    strainChance = Define.HaulStrainChance;
                    break;
            }

            if (physiqueXP <= 0f) return;

            SkillDef physiqueDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueDef != null && pawn.skills != null)
            {
                pawn.skills.Learn(physiqueDef, physiqueXP);
            }

            Need_MuscleStrain muscleStrain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
            if (muscleStrain != null)
            {
                muscleStrain.AddStrain(muscleStrainAmount);

                float finalChance = strainChance * PhysiqueLgc.GetMuscleStrainChanceMultiplier(pawn);
                bool canTrigger = muscleStrain.CurLevel < muscleStrain.MaxLevel * 0.1f;
                bool triggered = false;
                
                if (canTrigger)
                {
                    triggered = Rand.Value < finalChance;
                }

                ShowMuscleStrainText(pawn.Position, pawn.Map, muscleStrain, muscleStrainAmount, strainChance, finalChance, canTrigger, triggered, curJob);

                if (triggered)
                {
                    MuscleStrainUtility.TryAddMuscleStrain(pawn);
                }
            }

            if (pawn.Map != null && pawn.Position.IsValid)
            {
                //ShowPhysiqueXPText(pawn.Position, pawn.Map, physiqueXP);
            }
        }

        private static void ShowPhysiqueXPText(IntVec3 pos, Map map, float xpAmount)
        {
            MoteText moteText = (MoteText)ThingMaker.MakeThing(ThingDefOf.Mote_Text);
            object vector3 = PhysiqueDatas.GetVector3(pos.x + 0.5f, 0.5f, pos.z + 0.5f);
            System.Reflection.FieldInfo field = typeof(MoteText).GetField("exactPosition");
            if (field != null)
            {
                field.SetValue(moteText, vector3);
                moteText.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));
                moteText.text = $"Physique +{xpAmount}";
                GenSpawn.Spawn(moteText, pos, map);
                PhysiqueDatas.ReturnVector3(vector3);
            }
        }

        private static void ShowMuscleStrainText(IntVec3 pos, Map map, Need_MuscleStrain muscleStrain, float strainAmount, float baseChance, float finalChance, bool canTrigger, bool triggered, Job curJob)
        {
            MoteText moteText = (MoteText)ThingMaker.MakeThing(ThingDefOf.Mote_Text);
            object vector3 = PhysiqueDatas.GetVector3(pos.x + 0.5f, 0.5f, pos.z + 0.5f);
            System.Reflection.FieldInfo field = typeof(MoteText).GetField("exactPosition");
            if (field != null)
            {
                field.SetValue(moteText, vector3);
                moteText.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));
                
                float strainPercent = (muscleStrain.CurLevel / muscleStrain.MaxLevel) * 100f;
                string status = canTrigger 
                    ? (triggered ? "肌肉拉伤!" : $"尝试拉伤 {finalChance * 100:F1}%") 
                    : "体力充足";

                string targetName = "";
                Thing targetThing = curJob.GetTarget(TargetIndex.A).Thing;
                if (targetThing != null)
                {
                    targetName = $"[{targetThing.LabelShort}] ";
                }
                
                moteText.text = $"{targetName}劳损: {strainPercent:F0}% ({muscleStrain.CurLevel:F0}/{muscleStrain.MaxLevel:F0}) | -{strainAmount:F0} | {status}";
                
                GenSpawn.Spawn(moteText, pos, map);
                PhysiqueDatas.ReturnVector3(vector3);
            }
        }

        private static Pawn GetPawn(Pawn_JobTracker jobTracker)
        {
            var field = typeof(Pawn_JobTracker).GetField("pawn",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(jobTracker) as Pawn;
        }
    }
}