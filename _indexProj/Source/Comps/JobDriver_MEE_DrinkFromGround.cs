using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Hormones
{
    /// <summary>
    /// 直接饮用自然水面（池塘/河/海）的补水驱动器，参考 DBH 的 JobDriver_DrinkFromGround。
    /// 流程：走到水格相邻处（Touch）→ 计时 toil（1000 tick ≈ 16.7s）→ 每 tick 微量补水直到需求满；
    /// 完成后按概率致病（与「生水」同思路，复用 FoodPoisoning）。
    /// 补水走 Need_MEE_Water.Drink()，直接改 CurLevel、不触发 OnModified 事件，避免飘字刷屏。
    /// </summary>
    public class JobDriver_MEE_DrinkFromGround : JobDriver
    {
        private TargetIndex Cell => TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 自然水面可被多人共用，无需预约（与 DBH 一致，直接放行）。
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(Cell, PathEndMode.Touch);

            Toil drink = new Toil();
            drink.defaultDuration = 1000;
            drink.defaultCompleteMode = ToilCompleteMode.Delay;
            drink.tickAction = delegate
            {
                Need_MEE_Water water = pawn.needs?.TryGetNeed<Need_MEE_Water>();
                water?.Drink();
            };
            drink.AddEndCondition(delegate
            {
                Need_MEE_Water water = pawn.needs?.TryGetNeed<Need_MEE_Water>();
                if (water == null) return JobCondition.Incompletable;
                if (water.CurLevel >= 1f) return JobCondition.Succeeded;
                return JobCondition.Ongoing;
            });
            drink.AddFinishAction(delegate
            {
                // 池塘/野水风险：20% 概率食物性中毒（与生水致病同款，复用原版 FoodPoisoning）
                if (Rand.Chance(0.2f)
                    && pawn.health?.hediffSet != null
                    && !pawn.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning))
                {
                    pawn.health.AddHediff(HediffDefOf.FoodPoisoning);
                }
            });
            yield return drink;
        }
    }
}
