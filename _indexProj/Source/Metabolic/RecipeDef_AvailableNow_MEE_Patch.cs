using HarmonyLib;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 食谱显隐闸：与 Need_MEE_* 的 ShowOnNeedList 同思路。
    /// RecipeDef 没有逐实例 ShowOnNeedList，游戏改用 AvailableNow 决定是否在菜单显示/可制作，
    /// 故在 AvailableNow getter 上做 postfix：模块未加载时把带 MEERecipeMarker 的食谱翻成 false。
    /// 此 patch 必须在主 mod（常驻），因为模块关掉时 MEE DLL 未载入，仍需隐藏。
    /// </summary>
    [HarmonyPatch(typeof(RecipeDef), "AvailableNow", MethodType.Getter)]
    public static class RecipeDef_AvailableNow_MEE_Patch
    {
        public static void Postfix(RecipeDef __instance, ref bool __result)
        {
            if (__result && !MetaBolicLoadCtrl.IsLoadedMME && __instance.HasModExtension<MEERecipeMarker>())
            {
                __result = false;
            }
        }
    }
}
