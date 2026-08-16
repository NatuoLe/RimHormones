using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Hormones
{
    // ============================================================
    // 体魄 → 一次取用多份食物（2026-08-16）
    // ============================================================
    // 背景：
    //   体魄提升食量上限的部分**早已实现** —— HormonesLogic.cs 里的
    //   `Need_Food_MaxLevel_Patch` 已把 Need_Food.MaxLevel 乘上
    //   PhysiqueLgc.GetAppetiteMultiplier（Frail 0.80 / Average 1.0 / Fit 1.20 /
    //   Strong 1.60 / Peak 2.0）。所以 Peak 小人的食量上限本来就是 ~2.0 营养。
    //
    // 但原版有两处硬上限把"一次拿几份"锁死在 1 份，导致大胃王要反复跑去吃：
    //
    //   1) FoodUtility.WillIngestStackCountOf（FoodUtility.cs:1273）
    //        num = StackCountForNutrition(needs.food.NutritionWanted, single);
    //        if (def.ingestible.maxNumToIngestAtOnce > 0)
    //            num = Min(num, def.ingestible.maxNumToIngestAtOnce);   // ← 卡点
    //      而所有餐食的抽象基 MealBaseIngredientless
    //      （Defs/Core/ThingDefs_Items/Items_Food.xml）硬写 maxNumToIngestAtOnce=1。
    //      这个函数是**全部取食路径的唯一份数入口**（JobGiver_GetFood、
    //      Toils_Ingest 的预留、喂病人/囚犯、宴会进食、右键菜单都走它），改它即可贯通。
    //
    //   2) Thing.IngestedCalculateAmounts（Thing.cs:2193）
    //        numTaken = CeilToInt(nutritionWanted / num);
    //        numTaken = Min(numTaken, stackCount);
    //        if (def.ingestible.maxNumToIngestAtOnce > 0)
    //            numTaken = Min(numTaken, def.ingestible.maxNumToIngestAtOnce);  // ← 同样的卡点
    //      **必须一起改**：只放开 (1) 会出现"拿了 2 份却只吃 1 份"，多余那份留在
    //      身上或掉地上。两处都放开才闭环。
    //
    // 设计取舍（用户 2026-08-16 定）：
    //   - 份数用**向上取整**（CeilToInt）而非原版 StackCountForNutrition 的四舍五入，
    //     即"缺多少补多少"：缺 1.4 也拿 2 份。代价是可能有营养溢出浪费，用户接受。
    //   - **不改饥饿速率**（GetHungerRate 保持原样，速率与食欲仍共用同一曲线）。
    //
    // 安全约束：
    //   - 只对人形（Humanlike）且体魄倍率 > 1 的小人放开，避免影响动物/机械体/囚犯逻辑。
    //   - 只放开到"食量缺口需要的份数"，不会无脑清空整堆。
    //   - 药物（IsDrug）与非食物一律不碰。
    //   - 上限兜底 MaxServings，防止某些极端 modded 食物（单份营养极小）导致一次搬走一大堆。
    // ============================================================

    /// <summary>体魄多份取食的共用计算。</summary>
    public static class PhysiqueEatingUtility
    {
        /// <summary>一次最多允许取用的份数硬上限（防极端 modded 低营养食物刷爆）。</summary>
        public const int MaxServings = 10;

        /// <summary>
        /// 是否对该 pawn + 该食物启用"多份取用"。
        /// 仅人形、且体魄食欲倍率 > 1（Fit 及以上）时启用；药物不参与。
        /// </summary>
        public static bool ShouldAllowMultiServing(Pawn ingester, ThingDef def)
        {
            if (ingester == null || def == null) return false;
            if (def.IsDrug) return false;
            if (def.ingestible == null) return false;
            if (!ingester.RaceProps.Humanlike) return false;
            if (ingester.needs?.food == null) return false;
            if (!PhysiqueLgc.IsHormoneSubject(ingester)) return false;

            // 只有食欲被体魄放大（Fit 及以上，倍率 1.20~2.0）才需要多拿。
            // Frail(0.80)/Average(1.0) 保持原版一份，避免无谓改动。
            return PhysiqueLgc.GetAppetiteMultiplier(ingester) > 1.001f;
        }

        /// <summary>
        /// 按"食量缺口"向上取整算出需要的份数。
        /// wantedNutrition 缺 1.4、单份 1.0 → 2 份（原版四舍五入只会给 1 份）。
        /// </summary>
        public static int ServingsForNutrition(float wantedNutrition, float singleNutrition)
        {
            if (wantedNutrition <= 0.0001f) return 1;
            // 单份营养异常（0 或负）时不做推算，交还原版语义
            if (singleNutrition <= 0.0001f) return 1;

            int n = Mathf.CeilToInt(wantedNutrition / singleNutrition);
            return Mathf.Clamp(n, 1, MaxServings);
        }
    }

    /// <summary>
    /// 放开"拿几份"的上限。见文件头注释的卡点 (1)。
    /// 用 Postfix 而非 Prefix：保留原版全部前置判断（药物/无 needs 等），
    /// 只在结果被 maxNumToIngestAtOnce 截断后按体魄重新放大。
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.WillIngestStackCountOf))]
    public static class FoodUtility_WillIngestStackCountOf_Physique_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref int __result, Pawn ingester, ThingDef def, float singleFoodNutrition)
        {
            if (!PhysiqueEatingUtility.ShouldAllowMultiServing(ingester, def)) return;

            Need_Food food = ingester.needs.food;
            if (food == null) return;

            // NutritionWanted = MaxLevel - CurLevel，MaxLevel 已含体魄食欲加成
            int want = PhysiqueEatingUtility.ServingsForNutrition(food.NutritionWanted, singleFoodNutrition);

            // 只放大、不缩小：避免与其它 mod 的加量逻辑打架
            if (want > __result) __result = want;
        }
    }

    /// <summary>
    /// 放开"实际吃几份"的上限。见文件头注释的卡点 (2)。
    /// 不改则会拿多份只吃一份。
    ///
    /// 注意：Thing.IngestedCalculateAmounts 是 protected virtual，且 Corpse / Plant
    /// 有 override。这里只 patch Thing 的基类实现 —— 普通餐食走基类，
    /// 尸体与植物走各自 override，天然不受影响。
    /// </summary>
    [HarmonyPatch(typeof(Thing), "IngestedCalculateAmounts")]
    public static class Thing_IngestedCalculateAmounts_Physique_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester, float nutritionWanted,
                                   ref int numTaken, ref float nutritionIngested)
        {
            if (__instance == null) return;
            if (!PhysiqueEatingUtility.ShouldAllowMultiServing(ingester, __instance.def)) return;

            float single = FoodUtility.NutritionForEater(ingester, __instance);
            if (single <= 0.0001f) return;

            int want = PhysiqueEatingUtility.ServingsForNutrition(nutritionWanted, single);

            // 不能超过手上这一堆的数量
            want = Mathf.Min(want, __instance.stackCount);

            if (want > numTaken)
            {
                numTaken = want;
                // 必须同步重算摄入营养，否则吃了多份却只回一份的饱食度
                nutritionIngested = (float)numTaken * single;
            }
        }
    }
}
