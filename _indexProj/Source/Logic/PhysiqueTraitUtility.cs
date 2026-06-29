using Verse;
using RimWorld;
using System.Collections.Generic;

namespace Hormones
{
    /// <summary>
    /// 挂载在 Trait 上存储体魄相关参数
    /// </summary>
    public class PhysiqueTraitExt : DefModExtension
    {
        public int physiqueOffset = 0;
        public float physiqueExpFactor = 1f;
        public float physiqueDecayFactor = 1f;
        public int physiqueCapOffset = 0;
    }

    /// <summary>
    /// 简易数据载体
    /// </summary>
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

        /// <summary>
        /// 清除缓存（在扩展添加后调用）
        /// </summary>
        public static void ClearCache()
        {
            traitCache.Clear();
            Log.Message("[Hormones] PhysiqueTraitUtility cache cleared");
        }

        public static PhysiqueTraitExt GetPhysiqueExtension(this TraitDef traitDef)
        {
            if (traitDef == null) return null;
            
            // 不使用缓存，确保每次都获取最新的扩展
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
            if (pawn == null || pawn.story?.traits == null) return 1f;
            
            float factor = 1f;
            foreach (var trait in pawn.story.traits.allTraits)
            {
                var extension = trait.def.GetPhysiqueExtension();
                if (extension != null)
                {
                    factor *= extension.physiqueExpFactor;
                }
            }
            return factor;
        }

        public static float GetTotalDecayFactor(Pawn pawn)
        {
            if (pawn == null || pawn.story?.traits == null) return 1f;
            
            float factor = 1f;
            foreach (var trait in pawn.story.traits.allTraits)
            {
                var extension = trait.def.GetPhysiqueExtension();
                if (extension != null)
                {
                    factor *= extension.physiqueDecayFactor;
                }
            }
            return factor;
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
