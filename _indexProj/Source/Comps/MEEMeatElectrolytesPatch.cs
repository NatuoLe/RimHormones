using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 吃肉补充电解质与蛋白质：殖民者摄入「含肉」食物（生肉，或含肉食材的熟食）时，
    /// 按固定比例补充 MEE 电解质需求与蛋白质需求。
    /// 仅在代谢扩展模块启用（MetaBolicLoadCtrl.Active）时生效；其余食物（流体/植物/动物制品）不受影响。
    /// 由主 mod 的 HarmonyPatches.PatchAll 自动应用，无需手动注册。
    ///
    /// 肉类判定（IsMeatBased）覆盖：
    ///   - 生肉 / 纯肉制品：ingestible.foodType 含 FoodTypeFlags.Meat；
    ///   - 含肉熟食（精致餐/简单餐/火锅等）：其 ingestible.foodType 多为 Meal（不带 Meat 标记），
    ///     故额外扫描 CompIngredients 实际食材，只要任一食材是肉即算「含肉」。
    /// 以此修复旧版仅靠 foodType.HasFlag(Meat) 导致大量含肉熟食漏检的问题。
    /// </summary>
    [HarmonyPatch(typeof(Thing), "Ingested")]
    public static class Patch_Thing_Ingested_MeatElectrolytes
    {
        /// <summary>每份肉补充的电解质比例（占 MaxLevel）。暂定 0.15（15%）。</summary>
        private const float ElectrolytesPerMeat = 0.15f;
        /// <summary>每份肉补充的蛋白质比例（占 MaxLevel）。与电解质同比例，暂定 0.15（15%）。</summary>
        private const float ProteinPerMeat = 0.15f;

        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn __0, float __1)
        {
            if (!MetaBolicLoadCtrl.Active) return;
            if (__0 == null || __0.needs == null || __0.Dead) return;

            // 仅对「含肉」食物生效
            if (!IsMeatBased(__instance)) return;

            Need_MEE_Electrolytes elec = __0.needs.TryGetNeed<Need_MEE_Electrolytes>();
            if (elec != null) elec.Satisfy(ElectrolytesPerMeat);

            Need_MEE_Protein protein = __0.needs.TryGetNeed<Need_MEE_Protein>();
            if (protein != null) protein.Satisfy(ProteinPerMeat);
        }

        /// <summary>
        /// 判定食物是否「含肉」：
        ///  - 生食/纯肉制品：ingestible.foodType 含 FoodTypeFlags.Meat；
        ///  - 熟食：扫描 CompIngredients 实际食材，只要任一食材是肉即算。
        /// </summary>
        private static bool IsMeatBased(Thing food)
        {
            if (food?.def?.ingestible == null) return false;

            FoodTypeFlags ft = food.def.ingestible.foodType;
            if (ft.HasFlag(FoodTypeFlags.Meat)) return true;

            CompIngredients ci = food.TryGetComp<CompIngredients>();
            if (ci != null)
            {
                foreach (ThingDef ing in ci.ingredients)
                {
                    if (ing?.ingestible != null && ing.ingestible.foodType.HasFlag(FoodTypeFlags.Meat))
                        return true;
                }
            }
            return false;
        }
    }
}
