using Verse;
using RimWorld;
using System.Collections.Generic;

namespace Hormones
{
    public class PhysiqueTraitExt : DefModExtension
    {
        public int physiqueOffset = 0;
        public float physiqueExpFactor = 1f;
        public float physiqueDecayFactor = 1f;
        public int physiqueCapOffset = 0;
    }

    public struct PhysiqueTraitData
    {
        public int offset;
        public float expFactor;
        public float decayFactor;
        public int capOffset;
    }

    public static class PhysiqueTraitUtility
    {
        private static Dictionary<string, PhysiqueTraitExt> traitCache = new Dictionary<string, PhysiqueTraitExt>();

        public static void ClearCache()
        {
            traitCache.Clear();
            Log.Message("[Hormones] PhysiqueTraitUtility cache cleared");
        }

        public static PhysiqueTraitExt GetPhysiqueExtension(this TraitDef traitDef)
        {
            if (traitDef == null) return null;
            return traitDef.GetModExtension<PhysiqueTraitExt>();
        }

        public static int GetTotalPhysiqueOffset(Pawn pawn)
        {
            if (pawn == null || pawn.story?.traits == null) return 0;
            
            int totalOffset = 0;
            foreach (var trait in pawn.story.traits.allTraits)
            {
                var extension = trait.def.GetPhysiqueExtension();
                if (extension != null)
                {
                    totalOffset += extension.physiqueOffset;
                }
            }
            return totalOffset;
        }

        public static float GetTotalExpFactor(Pawn pawn)
        {
            return 1f;
        }

        public static float GetTotalDecayFactor(Pawn pawn)
        {
            return 1f;
        }

        public static int GetTotalCapOffset(Pawn pawn)
        {
            if (pawn == null || pawn.story?.traits == null) return 0;
            
            int totalOffset = 0;
            foreach (var trait in pawn.story.traits.allTraits)
            {
                var extension = trait.def.GetPhysiqueExtension();
                if (extension != null)
                {
                    totalOffset += extension.physiqueCapOffset;
                }
            }
            return totalOffset;
        }
    }
}