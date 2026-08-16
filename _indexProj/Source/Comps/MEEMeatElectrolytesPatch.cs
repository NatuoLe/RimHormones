using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 吃肉补充电解质：殖民者摄入任何 foodType 含 Meat 的食物时，按固定比例补充 MEE 电解质需求。
    /// 仅在代谢扩展模块启用（MetaBolicLoadCtrl.Active）时生效；其余食物（流体/植物/动物制品）不受影响。
    /// 由主 mod 的 HarmonyPatches.PatchAll 自动应用，无需手动注册。
    /// </summary>
    [HarmonyPatch(typeof(Thing), "Ingested")]
    public static class Patch_Thing_Ingested_MeatElectrolytes
    {
        /// <summary>每份肉补充的电解质比例（占 MaxLevel）。暂定 0.15（15%）。</summary>
        private const float ElectrolytesPerMeat = 0.15f;

        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn __0, float __1)
        {
            if (!MetaBolicLoadCtrl.Active) return;
            if (__0 == null || __0.needs == null || __0.Dead) return;

            FoodTypeFlags? foodType = __instance?.def?.ingestible?.foodType;
            if (foodType == null || !foodType.Value.HasFlag(FoodTypeFlags.Meat)) return;

            Need_MEE_Electrolytes elec = __0.needs.TryGetNeed<Need_MEE_Electrolytes>();
            if (elec == null) return;

            elec.Satisfy(ElectrolytesPerMeat);
        }
    }
}
