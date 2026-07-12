using Verse;
using RimWorld;

namespace Hormones
{
    public class PhysiqueTraitExt : DefModExtension
    {
        public int physiqueOffset = 0;
        public int physiqueCapOffset = 0;
    }

    public static class PhysiqueTraitUtility
    {
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