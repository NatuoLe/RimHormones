using RimWorld;
using Verse;
using UnityEngine;

namespace Hormones
{
    /// <summary>水分需求。消耗较快，代表日常饮水缺口。</summary>
    public class Need_MEE_Water : Need_MEE_Base
    {
        public Need_MEE_Water(Pawn pawn) : base(pawn) { }

        protected override float FallPerDay => 0.6f; // 每日约消耗 60%

        protected override void OnModified(float before, float after)
        {
            NeedChangeEvents.FireWaterChanged(pawn, before, after);
        }

        /// <summary>
        /// 直接从自然水面（池塘/河/海）饮水：每 tick 微量补水（默认 +0.006，约 1000 tick 喝满）。
        /// 直接改 CurLevel、不触发 OnModified 事件，避免飘字刷屏（参考 DBH Need_Thirst.Drink）。
        /// </summary>
        public void Drink(float amount = 0.006f)
        {
            CurLevel = Mathf.Min(CurLevel + amount, MaxLevel);
        }
    }
}
