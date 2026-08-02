using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 战斗触发源。与 StrainHediffExt 的标签一一对应。
    /// </summary>
    public enum StrainTriggerSource
    {
        /// <summary>单位射击时（Verb_LaunchProjectile.TryCastShot）。</summary>
        Shoot,
        /// <summary>单位发起近战攻击时（Verb_MeleeAttack.TryCastShot）。</summary>
        Melee,
        /// <summary>单位被近战真实命中时（含被护甲格挡，不一定造成伤害）。</summary>
        MeleeHitTaken,
        /// <summary>肾上腺素长期堆积（持续高肾上腺素状态下每 10 秒检测一次）。</summary>
        AdrenalineBuildup,
    }

    /// <summary>损伤严重档位。决定「触发后抽到多严重的损伤」，比例随体魄变化。</summary>
    public enum StrainSeverityTier
    {
        Mild,
        Moderate,
        Severe,
    }

    /// <summary>
    /// 透支损伤 Hediff 的战斗触发标签（挂在 HediffDef 的 modExtensions 上）。
    /// 对应「文档/Hediff/损伤Hediff与触发逻辑.xlsx」→ Hediff总览 表的关键词列：
    ///   combatTriggered  ← 【战斗时触发】：总开关，false 则该损伤完全不参与战斗抽取
    ///   onShoot          ← 【单位射击触发】
    ///   onMelee          ← 【单位近战触发】
    ///   onMeleeHitTaken  ← 【单位被真实近战攻击到触发】（被击中即可，可能被护甲格挡）
    ///   onAdrenalineBuildup ← 【肾上腺素长期堆积造成的Hediff】
    ///   severityTier     ← 【损伤档位】Mild / Moderate / Severe（决定抽取比例与飘字颜色）
    /// 注：是否可治疗走原版 HediffDef.tendable 字段，不在此扩展中。
    /// </summary>
    public class StrainHediffExt : DefModExtension
    {
        public bool combatTriggered = false;
        public bool onShoot = false;
        public bool onMelee = false;
        public bool onMeleeHitTaken = false;
        public bool onAdrenalineBuildup = false;
        public StrainSeverityTier severityTier = StrainSeverityTier.Mild;

        /// <summary>
        /// 该标签集是否响应指定触发源。
        /// 注意：combatTriggered 只是「战斗类」触发源（射击/近战/被近战命中）的总开关；
        /// 肾上腺素长期堆积不属于战斗触发，由 onAdrenalineBuildup 独立控制。
        /// </summary>
        public bool RespondsTo(StrainTriggerSource source)
        {
            if (source == StrainTriggerSource.AdrenalineBuildup)
                return onAdrenalineBuildup;

            if (!combatTriggered) return false;
            switch (source)
            {
                case StrainTriggerSource.Shoot:         return onShoot;
                case StrainTriggerSource.Melee:         return onMelee;
                case StrainTriggerSource.MeleeHitTaken: return onMeleeHitTaken;
                default:                                return false;
            }
        }
    }

    /// <summary>
    /// 一个体魄档位下的轻/中/重抽取比例（供 StrainTierRulesDef 配置）。
    /// 判定：roll &lt; severeChance → 重度；roll &lt; moderateChance → 中度；否则轻度。
    /// </summary>
    public class StrainTierRule
    {
        /// <summary>适用体魄上界（不含）。例如 5 表示体魄 0~4 适用。</summary>
        public int maxPhysiqueLevel = 999;
        public float severeChance = 0.05f;
        public float moderateChance = 0.30f;
    }

    /// <summary>
    /// 战斗透支损伤的档位抽取比例配置（XML 可配，见 Defs/MiscDefs/StrainTierRules.xml）。
    /// 规则按 maxPhysiqueLevel 升序匹配第一条命中的。
    /// </summary>
    public class StrainTierRulesDef : Def
    {
        public List<StrainTierRule> rules = new List<StrainTierRule>();

        /// <summary>取适用于该体魄等级的规则；无匹配时返回 null。</summary>
        public StrainTierRule RuleFor(int physiqueLevel)
        {
            StrainTierRule best = null;
            int bestBound = int.MaxValue;
            for (int i = 0; i < rules.Count; i++)
            {
                StrainTierRule r = rules[i];
                if (physiqueLevel < r.maxPhysiqueLevel && r.maxPhysiqueLevel < bestBound)
                {
                    best = r;
                    bestBound = r.maxPhysiqueLevel;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// 肾上腺素某个阶段的长期堆积规则（供 StrainAdrenalineStageRulesDef 配置）。
    /// </summary>
    public class StrainAdrenalineStageRule
    {
        /// <summary>适用的肾上腺素阶段：Dormant / Low / Medium / High。</summary>
        public AdrenalineLevel level = AdrenalineLevel.High;
        /// <summary>该阶段是否会触发长期堆积损伤。</summary>
        public bool enabled = true;
        /// <summary>该阶段触发时抽取的损伤档位。</summary>
        public StrainSeverityTier tier = StrainSeverityTier.Severe;
        /// <summary>该阶段的概率额外倍率（在体魄公式之上再乘）。</summary>
        public float chanceFactor = 1f;
    }

    /// <summary>
    /// 肾上腺素长期堆积损伤的「阶段 → 档位」映射配置
    /// （XML 可配，见 Defs/MiscDefs/StrainAdrenalineStageRules.xml）。
    /// </summary>
    public class StrainAdrenalineStageRulesDef : Def
    {
        public List<StrainAdrenalineStageRule> rules = new List<StrainAdrenalineStageRule>();

        /// <summary>取该阶段的规则；未配置则返回 null（视为不触发）。</summary>
        public StrainAdrenalineStageRule RuleFor(AdrenalineLevel level)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].level == level) return rules[i];
            }
            return null;
        }
    }

    /// <summary>
    /// 按标签筛选战斗透支损伤池 + 读取档位比例配置。</summary>
    public static class StrainHediffTags
    {
        private const string TierRulesDefName = "Hormones_StrainTierRules";
        private const string StageRulesDefName = "Hormones_StrainAdrenalineStageRules";

        private static Dictionary<StrainTriggerSource, List<HediffDef>> cache;
        private static StrainTierRulesDef tierRulesCache;
        private static bool tierRulesResolved;
        private static StrainAdrenalineStageRulesDef stageRulesCache;
        private static bool stageRulesResolved;

        /// <summary>取该触发源下所有参与抽取的损伤 Def（可能为空列表）。</summary>
        public static List<HediffDef> GetPool(StrainTriggerSource source)
        {
            if (cache == null) BuildCache();
            return cache.TryGetValue(source, out List<HediffDef> pool) ? pool : new List<HediffDef>();
        }

        /// <summary>取某损伤的档位（未挂扩展时按轻度处理）。</summary>
        public static StrainSeverityTier GetTier(HediffDef def)
        {
            StrainHediffExt ext = def?.GetModExtension<StrainHediffExt>();
            return ext != null ? ext.severityTier : StrainSeverityTier.Mild;
        }

        /// <summary>取档位比例配置（XML 缺失时返回 null，调用方回退到内置默认值）。</summary>
        public static StrainTierRulesDef TierRules
        {
            get
            {
                if (!tierRulesResolved)
                {
                    tierRulesCache = DefDatabase<StrainTierRulesDef>.GetNamedSilentFail(TierRulesDefName);
                    tierRulesResolved = true;
                    if (tierRulesCache == null)
                        Log.Warning("[Hormones] StrainTierRulesDef '" + TierRulesDefName + "' not found; using built-in defaults.");
                }
                return tierRulesCache;
            }
        }

        /// <summary>取「阶段 → 档位」映射配置（XML 缺失时返回 null，调用方视为不触发）。</summary>
        public static StrainAdrenalineStageRulesDef StageRules
        {
            get
            {
                if (!stageRulesResolved)
                {
                    stageRulesCache = DefDatabase<StrainAdrenalineStageRulesDef>.GetNamedSilentFail(StageRulesDefName);
                    stageRulesResolved = true;
                    if (stageRulesCache == null)
                        Log.Warning("[Hormones] StrainAdrenalineStageRulesDef '" + StageRulesDefName + "' not found; adrenaline buildup damage disabled.");
                }
                return stageRulesCache;
            }
        }

        private static void BuildCache()
        {
            cache = new Dictionary<StrainTriggerSource, List<HediffDef>>();
            foreach (StrainTriggerSource src in new[]
                     { StrainTriggerSource.Shoot, StrainTriggerSource.Melee,
                       StrainTriggerSource.MeleeHitTaken, StrainTriggerSource.AdrenalineBuildup })
            {
                List<HediffDef> pool = new List<HediffDef>();
                foreach (HediffDef def in DefDatabase<HediffDef>.AllDefsListForReading)
                {
                    StrainHediffExt ext = def.GetModExtension<StrainHediffExt>();
                    if (ext != null && ext.RespondsTo(src)) pool.Add(def);
                }
                cache[src] = pool;
            }
        }
    }
}
