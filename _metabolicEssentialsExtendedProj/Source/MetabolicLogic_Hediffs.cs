using System.Collections.Generic;
using RimWorld;
using Verse;
using Hormones;

namespace MetabolicEssential
{
    /// <summary>
    /// 代谢负面状态触发逻辑（对应设计草稿 二/四/五 的 Hediff 副作用）。
    ///
    /// 解耦原则同其余 MetabolicLogic_*：仅订阅主 mod 的 NeedChangeEvents 公共事件、仅经公共接口写回。
    ///
    /// 已实现的触发：
    ///   1) 脱水（MEE_Dehydration）：Water 满足度 &lt; 25% 持续游戏内 12 小时后触发，Hediff 自身 8h 后消退（可叠加/刷新）。
    ///   2) 水中毒（MEE_WaterPoisoning）：饮用 MEE 水瓶前 Water 已 &gt; 90% 时按概率触发，Hediff 24h 后消退（无法医疗）。
    ///   3) 浑身无力（MEE_Weakness）：电解质满足度 &lt; 30% 时持续施加（含「工作效率-20%」与心情下降），恢复后解除。
    ///
    /// 触发事件来源：
    ///   OnWaterChanged      ← Need_MEE_Water.NeedInterval（每 150 tick）
    ///   OnDrinkMEEWater     ← Comp_MEE_Satisfier 饮水前（带饮用前满足度）
    ///   OnElectrolytesChanged ← Need_MEE_Electrolytes.NeedInterval（每 150 tick）
    /// </summary>
    public static class MetabolicLogic_Hediffs
    {
        // ===== 设计参数（草稿未全给数值，暂定，后续可调） =====
        private const float DehydrationThreshold = 0.25f;     // Water < 25%
        private const int DehydrationSustainTicks = 30000;     // 持续 12h（60000/24*12）才触发
        private const float WaterPoisoningThreshold = 0.90f;   // 饮用前 Water > 90%
        private const float WaterPoisoningChance = 0.60f;      // 触发概率 60%
        private const float WeaknessThreshold = 0.30f;         // 电解质 < 30%

        /// <summary>每 pawn 的“缺水持续 tick”累计器，用于脱水判定。</summary>
        private static readonly Dictionary<int, int> lowWaterTicks = new Dictionary<int, int>();

        public static void Register()
        {
            NeedChangeEvents.OnWaterChanged += OnWaterChanged;
            NeedChangeEvents.OnElectrolytesChanged += OnElectrolytesChanged;
            NeedChangeEvents.OnDrinkMEEWater += OnDrinkMEEWater;
            NeedChangeEvents.OnSugarEaten += OnSugarEaten;
        }

        private static void OnWaterChanged(Pawn pawn, float oldV, float newV)
        {
            if (pawn?.needs == null || !MetaBolicLoadCtrl.Active) return;
            Need_MEE_Water water = pawn.needs.TryGetNeed<Need_MEE_Water>();
            if (water == null) return;

            // 缺水心情 Buff（双档）：重度(<15%)我需要水、轻度(<30%)我渴了
            if (water.Severity < 0.15f)
            {
                EnsureThought(pawn, "MEE_NeedWater");
                RemoveThought(pawn, "MEE_Thirsty");
            }
            else if (water.Severity < 0.30f)
            {
                EnsureThought(pawn, "MEE_Thirsty");
                RemoveThought(pawn, "MEE_NeedWater");
            }
            else
            {
                RemoveThought(pawn, "MEE_Thirsty");
                RemoveThought(pawn, "MEE_NeedWater");
            }

            // 脱水 Hediff：持续缺水 12h 触发
            int id = pawn.thingIDNumber;
            if (water.Severity < DehydrationThreshold)
            {
                int t = lowWaterTicks.TryGetValue(id, out var cur) ? cur : 0;
                t += 150; // NeedInterval 周期
                lowWaterTicks[id] = t;
                if (t >= DehydrationSustainTicks && !HasHediff(pawn, "MEE_Dehydration"))
                {
                    AddHediff(pawn, "MEE_Dehydration");
                    lowWaterTicks[id] = 0; // 重新计时，避免短时间内重复触发
                }
            }
            else
            {
                lowWaterTicks[id] = 0;
            }
        }

        /// <summary>摄入葡萄糖原浆 → 「吃了糖」心情 +1（短时记忆 Thought）。</summary>
        private static void OnSugarEaten(Pawn pawn, float levelBeforeEat)
        {
            if (pawn == null || !MetaBolicLoadCtrl.Active) return;
            AddThought(pawn, "MEE_AteSugar");
        }

        private static void OnDrinkMEEWater(Pawn pawn, float levelBeforeDrink)
        {
            if (pawn == null || !MetaBolicLoadCtrl.Active) return;
            if (levelBeforeDrink > WaterPoisoningThreshold && Rand.Value < WaterPoisoningChance)
            {
                AddHediff(pawn, "MEE_WaterPoisoning");
                AddThought(pawn, "MEE_WaterPoisoningMood");
            }
        }

        private static void OnElectrolytesChanged(Pawn pawn, float oldV, float newV)
        {
            if (pawn?.needs == null || !MetaBolicLoadCtrl.Active) return;
            Need_MEE_Electrolytes elec = pawn.needs.TryGetNeed<Need_MEE_Electrolytes>();
            if (elec == null) return;

            if (elec.Severity < WeaknessThreshold)
            {
                EnsureHediff(pawn, "MEE_Weakness");
                EnsureThought(pawn, "MEE_WeaknessMood");
            }
            else
            {
                RemoveHediff(pawn, "MEE_Weakness");
                RemoveThought(pawn, "MEE_WeaknessMood");
            }
        }

        // ===== 通用 Hediff / Thought 辅助 =====
        private static bool HasHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(defName, false);
            return def != null && pawn.health?.hediffSet != null && pawn.health.hediffSet.HasHediff(def);
        }

        private static void AddHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(defName, false);
            if (def != null && pawn.health?.hediffSet != null && !pawn.health.hediffSet.HasHediff(def))
                pawn.health.AddHediff(def);
        }

        private static void EnsureHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(defName, false);
            if (def != null && pawn.health?.hediffSet != null && !pawn.health.hediffSet.HasHediff(def))
                pawn.health.AddHediff(def);
        }

        private static void RemoveHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamed(defName, false);
            if (def != null && pawn.health?.hediffSet != null)
            {
                Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (h != null) pawn.health.RemoveHediff(h);
            }
        }

        private static void AddThought(Pawn pawn, string defName)
        {
            ThoughtDef def = ThoughtDef.Named(defName);
            if (def != null && pawn.needs?.mood?.thoughts?.memories != null)
                pawn.needs.mood.thoughts.memories.TryGainMemory(def);
        }

        private static void EnsureThought(Pawn pawn, string defName)
        {
            ThoughtDef def = ThoughtDef.Named(defName);
            if (def != null && pawn.needs?.mood?.thoughts?.memories != null)
            {
                // 若已存在同名记忆则不重复添加（避免刷屏）
                if (pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def) == null)
                    pawn.needs.mood.thoughts.memories.TryGainMemory(def);
            }
        }

        private static void RemoveThought(Pawn pawn, string defName)
        {
            ThoughtDef def = ThoughtDef.Named(defName);
            if (def != null && pawn.needs?.mood?.thoughts?.memories != null)
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(def);
        }
    }
}
