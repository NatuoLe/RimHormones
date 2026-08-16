using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Hormones.UI
{
    /// <summary>
    /// 健康面板顶部两态切换：原本 UI / 体魄 UI。
    /// 复用 ITab_Pawn_Health 的同一个 630×430 容器，不新开窗口。
    ///
    /// 只 patch ITab_Pawn_Health.FillTab —— 这样信息卡(Dialog_InfoCard)、
    /// 商队健康(WITab_Caravan_Health) 等其他调用 DrawPawnHealthCard 的地方
    /// 完全不受影响。
    /// </summary>
    public static class HealthTabUIState
    {
        public enum Mode { Vanilla, Physique }

        /// <summary>当前模式。静态 = 全局共享，切小人不重置（与原版 onOperationTab 行为一致）。</summary>
        public static Mode Current = Mode.Vanilla;

        /// <summary>toggle 条高度。原版在 DrawPawnHealthCard 里预留了 20px 顶部空间，这里再自己撑开。</summary>
        public const float ToggleRowHeight = 26f;
        private const float ToggleWidth = 96f;
        private const float ToggleGap = 4f;

        /// <summary>
        /// 健康绿按钮背景贴图（Textures/UI/Buttons/HealthBG.png）。
        /// 缺失时退回原版 DrawOptionBackground，保证不会因贴图丢失而崩 UI。
        /// </summary>
        private static Texture2D healthBG;
        private static Texture2D HealthBG
        {
            get
            {
                if (healthBG == null)
                    healthBG = ContentFinder<Texture2D>.Get("UI/Buttons/HealthBG", false);
                return healthBG;
            }
        }

        /// <summary>
        /// 画 toggle 条，返回它占掉的矩形。
        /// 放在容器顶部靠左，避开右上角关闭按钮（CloseButtonFor 占 x 最右 22px、y 顶部 22px）。
        /// </summary>
        public static Rect DrawToggleRow(Rect containerRect)
        {
            Rect row = new Rect(containerRect.x, containerRect.y, containerRect.width, ToggleRowHeight);

            Rect btn1 = new Rect(row.x, row.y + 2f, ToggleWidth, ToggleRowHeight - 4f);
            Rect btn2 = new Rect(btn1.xMax + ToggleGap, btn1.y, ToggleWidth, btn1.height);

            DrawToggleButton(btn1, "HormonesHealthTab_Vanilla".Translate(), Mode.Vanilla);
            DrawToggleButton(btn2, "HormonesHealthTab_Physique".Translate(), Mode.Physique);

            return row;
        }

        private static void DrawToggleButton(Rect rect, string label, Mode mode)
        {
            bool selected = Current == mode;

            // 健康绿按钮背景：有自定义贴图就用它（选中=亮绿、未选中=暗绿），否则退回原版选项底
            Texture2D bg = HealthBG;
            if (bg != null)
            {
                GUI.color = selected ? Color.white : new Color(0.72f, 0.80f, 0.72f);
                GUI.DrawTexture(rect, bg);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawOptionBackground(rect, selected);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            GUI.color = selected ? Color.white : new Color(0.85f, 0.95f, 0.85f);
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(rect))
            {
                if (Current != mode)
                {
                    Current = mode;
                    SoundDefOf.RowTabSelect.PlayOneShotOnCamera();
                }
            }
            if (Mouse.IsOver(rect) && !selected) Widgets.DrawHighlight(rect);
        }
    }

    [HarmonyPatch(typeof(ITab_Pawn_Health), "FillTab")]
    public static class ITab_Pawn_Health_FillTab_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ITab_Pawn_Health __instance)
        {
            Pawn pawn = ResolvePawn(__instance);
            // 拿不到 pawn 或非激素对象 → 完全交还原版，不画 toggle
            if (pawn == null || !PhysiqueLgc.IsHormoneSubject(pawn)) return true;

            // size 是 InspectTabBase 的 protected 字段，用 Traverse 读，避免反射写死
            Vector2 size = Traverse.Create(__instance).Field("size").GetValue<Vector2>();
            Rect container = new Rect(Vector2.zero, size);

            Rect toggleRow = HealthTabUIState.DrawToggleRow(container);

            Rect content = container;
            content.yMin = toggleRow.yMax;

            if (HealthTabUIState.Current == HealthTabUIState.Mode.Vanilla)
            {
                // 原版态：自己调一次原版绘制（区域下移了 toggle 高度），然后跳过原方法
                HealthCardUtility.DrawPawnHealthCard(
                    content,
                    pawn,
                    ShouldAllowOperations(__instance),
                    HealthCardUtility.ShowBloodLoss(Find.Selector.SingleSelectedThing),
                    Find.Selector.SingleSelectedThing);
            }
            else
            {
                PhysiqueHealthCard.Draw(content, pawn);
            }
            return false;
        }

        private static Pawn ResolvePawn(ITab_Pawn_Health tab)
        {
            // 复刻原版 PawnForHealth（private 属性）
            Thing t = Find.Selector.SingleSelectedThing;
            if (t is Pawn p) return p;
            if (t is Corpse c) return c.InnerPawn;
            return null;
        }

        private static bool ShouldAllowOperations(ITab_Pawn_Health tab)
        {
            // 原版是 private bool ShouldAllowOperations()，直接反射调，保证规则一致
            return Traverse.Create(tab).Method("ShouldAllowOperations").GetValue<bool>();
        }
    }
}
