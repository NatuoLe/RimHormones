using System.Collections.Generic;
using Verse;
using RimWorld;
using Hormones;

namespace MetabolicEssential
{
    /// <summary>
    /// 糖代谢逻辑：监听主 mod 的「皮质醇变化」「糖变化」「劳损变化」事件，实现糖与各生理指标的反馈。
    ///
    /// 解耦原则：本模块【只订阅】主 mod 的 NeedChangeEvents 公共事件，并【只通过主 mod 暴露的公共接口】写回效果：
    ///   - Need_Cortisol.SetSugarCortisolModulation(...)  调制皮质醇每日变化率（%/日，正=抑制/负=催高）
    ///   - Need_MEE_Sugar.SetExtraFallPerDay(...)        调制糖每日消耗速率（叠加量，可正可负）
    /// 绝不直接读取/写入主 mod 的私有字段——从而满足「避免 Metabolic 字段入侵主工程」。
    ///
    /// 触发链：
    ///   主 mod 在每 150 tick（NeedInterval）更新皮质醇 / 糖 / 劳损后，会触发对应事件；
    ///   本逻辑在事件回调里读取【当前】各项严重度（均为 0~1），重算调制并写回。
    ///   因各指标几乎每 interval 都在波动，事件回调频繁触发，调制最多滞后一个 interval（≈2.5s），可忽略。
    ///
    /// 已实现的反馈：
    ///   1) 糖 → 皮质醇：皮质醇&gt;20% 时，糖&gt;33% 抑制其增长 10%/日，糖≤33% 催高 13%/日。
    ///   2) 皮质醇 → 糖消耗：皮质醇&gt;20% 时，糖每日消耗由 40% 降为 30%。
    ///   3) 劳损 → 糖消耗：执行劳损（训练动作 AddStrain 把劳损储备往下打）的窗口期内，糖额外消耗 +20%/日。
    ///      （劳损储备 CurLevel 高=状态好、低=已劳损；故以「储备正在被消耗」这一动作判定，而非静态看高低。）
    /// </summary>
    public static class MetabolicLogic_Sugar
    {
        // ===== 设计参数 =====
        /// <summary>糖需求满足度阈值：高于此视为“糖分充足”。</summary>
        private const float SugarLevelThreshold = 0.33f;
        /// <summary>皮质醇严重度阈值：高于此（>20%）才触发糖↔皮质醇联动。</summary>
        private const float CortisolSeverityThreshold = 0.20f;

        // —— 糖 → 皮质醇（单位：%/日；正值=抑制增长，负值=额外增长/催高） ——
        /// <summary>糖分充足(&gt;33%) 且 皮质醇高(&gt;20%) → 抑制皮质醇增长 约 10%/日。</summary>
        private const float CortisolSuppressPerDay = 10f;
        /// <summary>糖分不足(&lt;33%) → 催高皮质醇 约 13%/日（以负值表示“额外增长”）。</summary>
        private const float CortisolSurgePerDay = -13f;

        // —— 皮质醇 / 劳损 → 糖消耗（单位：占 MaxLevel 的 0~1 比例，即 %/日） ——
        /// <summary>糖基础每日自然消耗速率（默认 40%/日，与 Need_MEE_Sugar.FallPerDay 基准保持一致）。</summary>
        private const float SugarFallBase = 0.40f;
        /// <summary>皮质醇&gt;20% 时糖每日消耗降为 30%/日。</summary>
        private const float SugarFallWhenCortisolHigh = 0.30f;
        /// <summary>执行劳损（训练窗口内）时，糖额外每日消耗 +20%/日。</summary>
        private const float SugarFallFromStrain = 0.20f;

        // —— 劳损 → 水 / 电解质 额外消耗（占 MaxLevel 的 0~1 比例，即 %/日） ——
        /// <summary>执行劳损（训练窗口内）时，水额外每日消耗最高 +40%/日（草稿 2.2）。</summary>
        private const float WaterFallFromStrain = 0.40f;
        /// <summary>执行劳损（训练窗口内）时，电解质额外每日消耗最高 +33%/日（草稿 4.2）。</summary>
        private const float ElectrolytesFallFromStrain = 0.33f;

        // —— 劳损窗口：每次训练动作(AddStrain，即劳损储备被消耗)刷新该窗；窗内视为“正在执行劳损” ——
        /// <summary>劳损生效窗口（游戏 tick）。≈50 秒，覆盖连续训练动作之间的间隔；停止训练后窗过期即不再加成。</summary>
        private const int StrainActiveWindowTicks = 3000;

        /// <summary>每 pawn 的“执行劳损”窗口截止 tick。仅在窗口内对糖消耗加成。</summary>
        private static readonly Dictionary<Pawn, int> strainActiveUntil = new Dictionary<Pawn, int>();

        /// <summary>在模块 Init 时调用，订阅主 mod 的需求变化事件。</summary>
        public static void Register()
        {
            NeedChangeEvents.OnCortisolChanged += HandleChange;
            NeedChangeEvents.OnSugarChanged += HandleChange;
            NeedChangeEvents.OnStrainChanged += HandleStrainChange;
        }

        // 糖 / 皮质醇 事件共用同一处理函数：无论哪一方变化，都基于“当前”双方状态重算双向调制。
        private static void HandleChange(Pawn pawn, float oldVal, float newVal)
        {
            Recompute(pawn);
        }

        // 劳损事件：判定“是否正在执行劳损”（储备被消耗 = newVal < oldVal = AddStrain 训练动作），
        // 是则刷新该 pawn 的生效窗口，然后重算糖消耗调制。
        private static void HandleStrainChange(Pawn pawn, float oldVal, float newVal)
        {
            if (pawn != null && newVal < oldVal) // 劳损储备下降 = 正在训练/执行劳损
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                strainActiveUntil[pawn] = now + StrainActiveWindowTicks;
            }
            Recompute(pawn);
        }

        /// <summary>
        /// 基于当前【皮质醇严重度】【糖满足度】【是否在执行劳损】重算调制。
        /// 皮质醇↔糖 仅在皮质醇&gt;20% 区间生效；劳损→糖 在训练窗口内生效。
        /// </summary>
        private static void Recompute(Pawn pawn)
        {
            if (pawn?.needs == null) return;

            Need_Cortisol cortisol = pawn.needs.TryGetNeed<Need_Cortisol>();
            Need_MEE_Sugar sugar = pawn.needs.TryGetNeed<Need_MEE_Sugar>();
            if (cortisol == null || sugar == null) return;

            float cortisolSeverity = cortisol.CurLevel / cortisol.MaxLevel; // 0~1
            float sugarSeverity = sugar.CurLevel / sugar.MaxLevel;          // 0~1

            // —— 糖 → 皮质醇 ——
            float cortisolMod = 0f;
            if (cortisolSeverity > CortisolSeverityThreshold)
            {
                cortisolMod = sugarSeverity > SugarLevelThreshold
                    ? CortisolSuppressPerDay   // 糖分充足 → 抑制增长
                    : CortisolSurgePerDay;     // 糖分不足 → 催高
            }
            cortisol.SetSugarCortisolModulation(cortisolMod);

            // —— 皮质醇 / 劳损 → 糖消耗 ——
            // 基础 40%/日；皮质醇>20% 降为 30%（叠加 -0.10）；执行劳损窗口内再额外 +20%（叠加 +0.20）。
            // Need_MEE_Sugar.FallPerDay = 0.4 + extraFallPerDay，故叠加量 = 各分项相对基础的偏移之和。
            float extra = 0f;
            if (cortisolSeverity > CortisolSeverityThreshold)
                extra += SugarFallWhenCortisolHigh - SugarFallBase; // -0.10

            int now = Find.TickManager?.TicksGame ?? 0;
            bool straining = strainActiveUntil.TryGetValue(pawn, out int until) && now < until;
            if (straining)
                extra += SugarFallFromStrain; // +0.20

            sugar.SetExtraFallPerDay(extra);

            // 劳损 → 水 / 电解质 额外消耗（复用同一劳损窗口）
            Need_MEE_Water water = pawn.needs.TryGetNeed<Need_MEE_Water>();
            if (water != null)
                water.SetExtraFallPerDay(straining ? WaterFallFromStrain : 0f);

            Need_MEE_Electrolytes electrolytes = pawn.needs.TryGetNeed<Need_MEE_Electrolytes>();
            if (electrolytes != null)
                electrolytes.SetExtraFallPerDay(straining ? ElectrolytesFallFromStrain : 0f);
            //
        }
    }
}
