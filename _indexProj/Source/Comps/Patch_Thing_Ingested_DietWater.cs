using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 饮食（摄入食物）获得营养时触发 OnDietEaten 事件，携带「实际获得的营养值」与「被吃的食物实例」。
    /// 代谢扩展模块（MEE）订阅此事件：营养 15% → 水分需求；若吃的是素菜（纯植物，无肉无蛋奶），再 8% → 糖需求。
    ///
    /// 关键点：
    ///  - Thing.Ingested 的返回值即为「实际摄入的营养」(nutritionIngested)，正是用户要的“当前获取的营养值”。
    ///  - __instance 即被吃的食物（Thing），携带它以便订阅方按食物类型（素食/肉食/杂食）分流处理；
    ///    熟食（MealSimple 等）必须用 Thing 实例判断——其食材类型存在 CompIngredients 里，光看 def 判不了。
    ///  - 由主 mod 的 HarmonyPatches.PatchAll 自动应用，无需手动注册。
    ///  - MEE 补剂 / 水瓶的 nutrition=0 → Ingested 返回 0 → 守卫跳过，不会与 Comp_MEE_Satisfier 的补水逻辑重复计算。
    ///  - 仅在 nutritionGained > 0（确实吃到营养）时触发，避免满腹进食或空食对象空转事件。
    /// </summary>
    [HarmonyPatch(typeof(Thing), "Ingested")]
    public static class Patch_Thing_Ingested_DietWater
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn __0, float __result)
        {
            float nutritionGained = __result; // Thing.Ingested 的返回值 = 实际摄入营养
            if (nutritionGained <= 0.0001f) return;
            if (__0 == null || __0.Dead) return;

            NeedChangeEvents.FireDietEaten(__0, nutritionGained, __instance);
        }
    }
}
