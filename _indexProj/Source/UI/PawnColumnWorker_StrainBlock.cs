using RimWorld;
using UnityEngine;
using Verse;

namespace Hormones.UI
{
    /// <summary>
    /// 指派(Assign)面板自定义列：「劳损封锁」——每个小人独立的开关。
    /// 勾选后：该小人劳损储备(Need_MuscleStrain)低于设置阈值时自动挂「体力不支」，
    /// 经原版 disabledWorkTags 机制禁止 采矿/搬运/建造/种植/狩猎，休息恢复后自动解除。
    /// 状态存于 HormonesComponent.blockWorkWhenStrainLow（随存档持久化）。
    /// </summary>
    public class PawnColumnWorker_StrainBlock : PawnColumnWorker
    {
        private const float CheckboxSize = 22f;

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (pawn == null || pawn.Dead) return;
            HormonesComponent comp = pawn.TryGetComp<HormonesComponent>();
            if (comp == null) return;
            // 动物/机械体不是激素对象，不留控件（保持单元格空白）
            if (!PhysiqueLgc.IsHormoneSubject(pawn)) return;

            Rect cb = new Rect(
                rect.x + (rect.width - CheckboxSize) / 2f,
                rect.y + (rect.height - CheckboxSize) / 2f,
                CheckboxSize, CheckboxSize);

            bool val = comp.BlockWorkWhenStrainLow;
            Widgets.Checkbox(cb.position, ref val, CheckboxSize, false, true);
            if (val != comp.BlockWorkWhenStrainLow)
            {
                comp.BlockWorkWhenStrainLow = val;
                // 关闭时立即摘掉「体力不支」，不必等下一个 250 tick 检查周期
                if (!val) comp.RemoveStrainExhaustedNow();
            }

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect,
                    "劳损储备过低时，该小人不会主动接重体力工作（采矿/搬运/建造/种植/狩猎）。\n"
                    + "不锁工作能力，仍可右键手动优先指派。\n"
                    + "触发阈值与解除线在 mod 设置中调整。");
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return 64;
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return 64;
        }

        // 支持表头点击排序（开/关分组）
        public override int Compare(Pawn a, Pawn b)
        {
            bool va = a?.TryGetComp<HormonesComponent>()?.BlockWorkWhenStrainLow ?? false;
            bool vb = b?.TryGetComp<HormonesComponent>()?.BlockWorkWhenStrainLow ?? false;
            return va.CompareTo(vb);
        }
    }
}
