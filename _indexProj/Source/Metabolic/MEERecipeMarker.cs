using Verse;

namespace Hormones
{
    /// <summary>
    /// 标记一份食谱属于 Metabolic Essential 模块。
    /// 配合 RecipeDef_AvailableNow_MEE_Patch：模块未加载（MetabolicState.IsLoadedMME==false）时，
    /// 带此标记的食谱在“添加账单”菜单、健康卡手术列表等处全部隐藏且不可制作。
    /// 用法：在食谱 Def 的 &lt;modExtensions&gt; 里加
    ///   &lt;li Class="Hormones.MEERecipeMarker" /&gt;
    /// </summary>
    public class MEERecipeMarker : DefModExtension
    {
    }
}
