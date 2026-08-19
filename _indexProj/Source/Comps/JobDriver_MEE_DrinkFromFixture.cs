using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Hormones
{
    /// <summary>
    /// 在 DBH 取水设施（钢铁水池/水槽/浴缸等）旁补水，取的是 MEE 的水（Need_MEE_Water）。
    /// 与 JobDriver_MEE_DrinkFromGround 同源，区别：目标为建筑（TargetIndex.A）而非自然水格，
    /// 视为设施过滤水，不致病。用于 MEE 接管 DBH 后，让殖民者把 DBH 取水设施当作 MEE 取水点。
    /// </summary>
    public class JobDriver_MEE_DrinkFromFixture : JobDriver
    {
        private TargetIndex Fixture => TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 设施可被多人共用取水，不独占预约（与地形水一致）。
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(Fixture, PathEndMode.Touch);

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
            yield return drink;
        }
    }
}
