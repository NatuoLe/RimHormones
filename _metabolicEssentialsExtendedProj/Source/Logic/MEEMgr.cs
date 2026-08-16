using RimWorld;
using Verse;
using UnityEngine;
using Hormones;
using System.Collections.Generic;

namespace MetabolicEssential
{
    /// <summary>
    /// MEE（代谢扩展）四个需求（水分 / 糖 / 电解质 / 蛋白质）数值变化时，在角色头顶抛出飘字。
    ///
    /// 机制 —— 双模式飘字：
    ///   1. 即时抛：当单次 |delta| &gt;= ImmediateThreshold（1%）时，立刻抛出飘字。
    ///      典型场景：喝水/进食 Satisfy(0.5) → +50%，或大额 Consume 扣除。
    ///   2. 累积抛：自然衰减等小变化（每 150 tick 仅 0.04%~0.15%）先按 pawn×需求累积，
    ///      每隔 AccumThrowIntervalTicks（~15 秒）检查一次，将累积值四舍五入为百分比，
    ///      若 &gt;= AccumDisplayMinPct（1%）则抛出汇总飘字（如「水 -1%」）并清零；若不足 1% 也清零避免脏数据。
    ///
    /// 开关控制（三层，任一关闭即不抛）：
    ///   - 总开关 RimHormonesMod.Settings.ShowMEEMotes
    ///   - 子开关 ShowMEEWaterMotes / ShowMEE*Sugar/Electrolytes/Protein*Motes
    ///   - MetaBolicLoadCtrl.Active（模块启用）
    ///
    /// 订阅：NeedChangeEvents.OnWaterChanged / OnSugarChanged / OnElectrolytesChanged / OnProteinChanged（飘字）
    ///       + OnDietEaten（饮食转化：营养 15%→水；素菜再 8%→糖）。
    /// Register() 先反注册再订阅，保证幂等。
    /// </summary>
    public static class MEEMgr
    {
        // ── 即时抛阈值 ──
        /// <summary>单次变化超过此值（占 MaxLevel 比例）立即抛飘字。MEE MaxLevel=1，故等于绝对量。饮水/进食通常 0.5 远超此值。</summary>
        private const float ImmediateThreshold = 0.01f;

        /// <summary>饮食获得营养时转化为水分的比例（占所获营养的比例）。暂定 0.15（15%），与「吃肉补 15% 电解质」对齐。</summary>
        private const float DietWaterFromNutrition = 0.15f;

        /// <summary>吃素菜（纯植物，无肉无蛋奶）时，营养转化为糖需求的比例（占所获营养的比例）。8%。</summary>
        private const float DietSugarFromNutrition = 0.08f;

        // ── 累积抛参数 ──
        /// <summary>每隔这么多游戏 tick 检查一次累积值是否该抛字。900 tick ≈ 15 秒，足够让自然衰减累积到可显示的量（水每 15s 约自然掉 0.9%）。</summary>
        private const int AccumThrowIntervalTicks = 900;
        /// <summary>累积抛的显示门槛：累积变化四舍五入 >= 此百分比才抛字，避免显示 "-0%"。</summary>
        private const int AccumDisplayMinPct = 1;

        // ── 状态字典 ──
        /// <summary>"pawnThingID|defName" → 累积 delta 之和。</summary>
        private static readonly Dictionary<string, float> accumDelta = new Dictionary<string, float>();
        /// <summary>"pawnThingID|defName" → 上次抛出累积飘字的 tick。</summary>
        private static readonly Dictionary<string, int> lastAccumThrowTick = new Dictionary<string, int>();

        // ════════════════════════════════════════════════
        //  公开接口
        // ════════════════════════════════════════════════

        public static void Register()
        {
            NeedChangeEvents.OnWaterChanged -= OnWaterChanged;
            NeedChangeEvents.OnSugarChanged -= OnSugarChanged;
            NeedChangeEvents.OnElectrolytesChanged -= OnElectrolytesChanged;
            NeedChangeEvents.OnProteinChanged -= OnProteinChanged;
            NeedChangeEvents.OnDietEaten -= OnDietEaten;

            NeedChangeEvents.OnWaterChanged += OnWaterChanged;
            NeedChangeEvents.OnSugarChanged += OnSugarChanged;
            NeedChangeEvents.OnElectrolytesChanged += OnElectrolytesChanged;
            NeedChangeEvents.OnProteinChanged += OnProteinChanged;
            NeedChangeEvents.OnDietEaten += OnDietEaten;
        }

        // ════════════════════════════════════════════════
        //  事件处理器
        // ════════════════════════════════════════════════

        private static void OnWaterChanged(Pawn pawn, float oldV, float newV)
            => TryThrow(pawn, oldV, newV, "MEEWater", new Color(0.35f, 0.65f, 1f));
        private static void OnSugarChanged(Pawn pawn, float oldV, float newV)
            => TryThrow(pawn, oldV, newV, "MEESugar", new Color(1f, 0.82f, 0.25f));
        private static void OnElectrolytesChanged(Pawn pawn, float oldV, float newV)
            => TryThrow(pawn, oldV, newV, "MEEElectrolytes", new Color(0.3f, 1f, 0.85f));
        private static void OnProteinChanged(Pawn pawn, float oldV, float newV)
            => TryThrow(pawn, oldV, newV, "MEEProtein", new Color(1f, 0.45f, 0.5f));

        /// <summary>
        /// 饮食（摄入食物）获得营养时的转化：
        ///   1) 无论荤素：所获营养的 15% → 水分需求（Satisfy）。
        ///   2) 若吃的是素菜（FoodKind.NonMeat = 纯植物，无肉无蛋奶）：所获营养的 8% → 糖需求（Satisfy）。
        ///      熟食（简单餐等）用 Thing 实例判断——实际食材记录在 CompIngredients 里，光看 def 无法区分荤素。
        /// 模块未启用时不转化；需求不存在（未加载）时安全跳过。糖变化会经 OnSugarChanged 飘字显示。
        /// </summary>
        private static void OnDietEaten(Pawn pawn, float nutritionGained, Thing food)
        {
            if (!MetaBolicLoadCtrl.Active) return;
            if (pawn?.needs == null) return;

            Need_MEE_Water water = pawn.needs.TryGetNeed<Need_MEE_Water>();
            if (water != null)
                water.Satisfy(nutritionGained * DietWaterFromNutrition);

            // 素菜 → 糖：用 FoodUtility.GetFoodKind(Thing) 判定（熟食按实际食材，生食按 def）
            if (food == null || FoodUtility.GetFoodKind(food) != FoodKind.NonMeat) return;

            Need_MEE_Sugar sugar = pawn.needs.TryGetNeed<Need_MEE_Sugar>();
            if (sugar != null)
                sugar.Satisfy(nutritionGained * DietSugarFromNutrition);
        }

        // ════════════════════════════════════════════════
        //  核心：双模式判断 + 抛字
        // ════════════════════════════════════════════════

        private static void TryThrow(Pawn pawn, float oldV, float newV, string defName, Color color)
        {
            // ── 开关检查（三层，任一不过直接返回）──
            if (RimHormonesMod.Settings == null) return;
            if (!RimHormonesMod.Settings.ShowMEEMotes) return;
            if (!MetaBolicLoadCtrl.Active) return;

            bool subOn = defName switch
            {
                "MEEWater" => RimHormonesMod.Settings.ShowMEEWaterMotes,
                "MEESugar" => RimHormonesMod.Settings.ShowMEESugarMotes,
                "MEEElectrolytes" => RimHormonesMod.Settings.ShowMEEElectrolytesMotes,
                "MEEProtein" => RimHormonesMod.Settings.ShowMEEProteinMotes,
                _ => true
            };
            if (!subOn) return;
            if (pawn?.Map == null) return;

            float delta = newV - oldV;
            string key = (pawn.ThingID ?? "?") + "|" + defName;
            int now = (Find.TickManager != null) ? Find.TickManager.TicksGame : 0;

            // ── 双模式分支 ──
            if (Mathf.Abs(delta) >= ImmediateThreshold)
            {
                // ★ 大跳变 → 立即抛，同时清零该 key 的累积值（避免重复计算）
                DoThrowMote(pawn, delta, defName, color);
                ResetAccum(key, now);
                return;
            }

            // ☆ 小变化 → 累积
            if (!accumDelta.TryGetValue(key, out float acc))
                acc = 0f;
            acc += delta;
            accumDelta[key] = acc;

            // 到点才结算
            bool timeToFlush = !lastAccumThrowTick.TryGetValue(key, out int throwLt)
                               || now - throwLt >= AccumThrowIntervalTicks;
            if (!timeToFlush) return;

            if (Mathf.RoundToInt(Mathf.Abs(acc) * 100f) >= AccumDisplayMinPct)
                DoThrowMote(pawn, acc, defName, color);

            // 无论是否显示都清零，避免脏数据持续累积
            accumDelta[key] = 0f;
            lastAccumThrowTick[key] = now;
        }

        // ════════════════════════════════════════════════
        //  内部工具
        // ════════════════════════════════════════════════

        /// <summary>实际执行 MoteMaker.ThrowText。</summary>
        private static void DoThrowMote(Pawn pawn, float value, string defName, Color color)
        {
            string label = DefDatabase<NeedDef>.GetNamedSilentFail(defName)?.label ?? defName;
            string sign = value >= 0f ? "+" : "-";
            int pct = Mathf.RoundToInt(Mathf.Abs(value) * 100f);
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"{label} {sign}{pct}%", color, -1f);
        }

        private static void ResetAccum(string key, int now)
        {
            accumDelta[key] = 0f;
            lastAccumThrowTick[key] = now;
        }
    }
}
