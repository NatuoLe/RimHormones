namespace Hormones
{
    public static partial class Define
    {
        public const string ModName = "RimHormones";

        public const float HormonesMaxLevel = 100f;
        public const float HormonesDecayRate = 0.5f;
        public const float HormonesBaseDamageReduction = 15f;
        public const float HormonesBleedingReductionFactor = 0.1f;

        public const float SevereBleedingThreshold = 2.5f;

        #region Adrenaline Constants

        public const float AdrenalineThresholdDormant = 0.15f;
        public const float AdrenalineThresholdLow = 0.5f;
        public const float AdrenalineThresholdMedium = 0.75f;
        public const float AdrenalineThresholdHigh = 1.0f;

        public const float AdrenalineRestMultiplierLow = 1.0f;
        public const float AdrenalineRestMultiplierMedium = 1.25f;
        public const float AdrenalineRestMultiplierHigh = 1.5f;

        public const float AdrenalineOverexertBaseChance = 0.2f;
        public const float AdrenalineOverexertChancePerPhysique = 0.04f;

        public const float AdrenalineMeleeAttackBase = 0.08f;
        public const float AdrenalineRangedAttackBase = 0.04f;
        public const float AdrenalineHitBase = 0.15f;

        public const float AdrenalineBaseDecay = 0.02f;
        public const float AdrenalineDecayPerPhysique = 0.003f;

        public const float AdrenalineCombatInterpolationBase = 0.03f;

        public const float AdrenalineCombatDetectionRange = 15f;

        public static class AdrenalineLow
        {
            public const float Consciousness = 0.03f;
            public const float MoveSpeed = 0.04f;
            public const float RespiratoryCirculatory = 0.07f;
            public const float Metabolism = 0.13f;
            public const float PainReduction = -0.07f;
            public const float VisionReduction = -0.08f;
            public const float HearingReduction = -0.08f;
            
            public const float MeleeDamage = 0.06f;
            public const float Dodge = 0.036f;
            public const float MeleeHitReduction = -0.024f;
        }

        public static class AdrenalineMedium
        {
            public const float Consciousness = 0.05f;
            public const float MoveSpeed = 0.07f;
            public const float RespiratoryCirculatory = 0.13f;
            public const float Metabolism = 0.26f;
            public const float PainReduction = -0.13f;
            public const float VisionReduction = -0.14f;
            public const float HearingReduction = -0.14f;
            
            public const float MeleeDamage = 0.12f;
            public const float Dodge = 0.072f;
            public const float MeleeHitReduction = -0.048f;
        }

        public static class AdrenalineHigh
        {
            public const float Consciousness = 0.08f;
            public const float MoveSpeed = 0.10f;
            public const float RespiratoryCirculatory = 0.20f;
            public const float Metabolism = 0.40f;
            public const float PainReduction = -0.20f;
            public const float VisionReduction = -0.20f;
            public const float HearingReduction = -0.20f;
            
            public const float MeleeDamage = 0.20f;
            public const float Dodge = 0.12f;
            public const float MeleeHitReduction = -0.08f;
        }

        #endregion

        #region Cortisol Constants
        // ========================================
        // 皮质醇档位阈值（Severity 范围 0~1）
        // ========================================
        // 休眠不足: 0 ≤ S < 0.15 - 基础唤醒缺失，精神萎靡
        // 正常区间: 0.15 ≤ S < 0.5 - 健康舒适区间
        // 持续承压: 0.5 ≤ S < 0.75 - 疲劳累积，效率下滑
        // 过载透支: 0.75 ≤ S ≤ 1.0 - 长期过载，身心双重损耗
        public const float CortisolThresholdSleepDeprivation = 0.15f;
        public const float CortisolThresholdNormal = 0.15f;
        public const float CortisolThresholdStress = 0.5f;
        public const float CortisolThresholdOverload = 0.75f;

        // ========================================
        // 皮质醇增长速率（增量/秒）
        // ========================================
        public const float CortisolBaseGrowthMin = 0.001f;      // 基础每日增长（随机范围最小值）
        public const float CortisolBaseGrowthMax = 0.003f;      // 基础每日增长（随机范围最大值）
        public const float CortisolNeurastheniaGrowth = 0.005f; // 神经衰弱时增长（睡不好→皮质醇更高）
        public const float CortisolChronicStressGrowth = 0.002f;// 慢性压力环境增长（饥饿、恶劣环境）
        public const float CortisolAdrenalineLinkGrowth = 0.001f;// 肾上腺素联动增长
        public const float CortisolLowMoodGrowth = 0.0015f;     // 低心情增长（心情 < -10）

        // ========================================
        // 皮质醇衰减速率（减量/秒）
        // ========================================
        // 设计意图：皮质醇一旦堆积，极难消退——模拟真实皮质醇的「压力残留」特性
        public const float CortisolBaseDecay = 0.008f;      // 基础衰减速率
        public const float CortisolRestDecay = 0.015f;      // 充足安稳睡眠时衰减
        public const float CortisolHighMoodDecay = 0.005f;  // 心情极好时衰减（心情 > 20）

        // ========================================
        // 状态判定阈值
        // ========================================
        public const float CortisolMoodLowThreshold = -10f;   // 低心情触发增长阈值
        public const float CortisolMoodHighThreshold = 20f;   // 高心情触发加速衰减阈值
        public const float CortisolGoodRestThreshold = 0.8f;  // 良好休息判定阈值

        // ========================================
        // 神经衰弱触发参数
        // ========================================
        // 触发概率公式: P = min(0.15 + 0.03 × S × (13 - P), 0.6)
        public const float CortisolNeurastheniaBaseProb = 0.15f;      // 基础触发概率
        public const float CortisolNeurastheniaMaxProb = 0.6f;        // 最大触发概率（封顶60%）
        public const float CortisolNeurastheniaSeverityFactor = 0.03f;// 浓度影响因子

        // ========================================
        // 肾上腺素联动参数
        // ========================================
        public const float AdrenalineCortisolLinkThreshold = 0.5f; // 肾上腺素联动阈值

        // ========================================
        // 过载耗竭参数
        // ========================================
        public const float CortisolOverloadDurationHours = 48f; // 过载持续2天后触发耗竭

        // ========================================
        // 定时检查间隔
        // ========================================
        public const int CortisolDailyCheckInterval = 60000; // 每日检查间隔（约1游戏天）

        #endregion

        #region Cortisol Stage Effects
        // ========================================
        // 皮质醇各档位属性效果（静态配置）
        // ========================================

        /// <summary>
        /// 持续承压档（0.5 ≤ S < 0.75）属性效果
        /// </summary>
        public static class CortisolChronicStress
        {
            public const float Consciousness = 0.03f;           // 意识增幅 +3%
            public const float Metabolism = 0.10f;              // 新陈代谢 +10%
            public const float BloodPumping = -0.05f;           // 血液循环 -5%
            public const float InjuryHealingFactor = 0.95f;     // 伤口愈合 95%
            public const float ImmunityGainSpeedFactor = 0.95f; // 免疫效率 95%
            public const float HungerRateFactor = 1.15f;        // 饥饿速率 ×1.15
        }

        /// <summary>
        /// 过载透支档（0.75 ≤ S ≤ 1.0）属性效果
        /// </summary>
        public static class CortisolOverload
        {
            public const float Consciousness = 0.05f;           // 意识增幅 +5%
            public const float Metabolism = 0.15f;              // 新陈代谢 +15%
            public const float BloodPumping = -0.10f;           // 血液循环 -10%
            public const float InjuryHealingFactor = 0.97f;     // 伤口愈合 97%
            public const float ImmunityGainSpeedFactor = 0.90f; // 免疫效率 90%
            public const float HungerRateFactor = 1.30f;        // 饥饿速率 ×1.30
        }

        /// <summary>
        /// 过载耗竭状态属性效果
        /// 触发条件：皮质醇≥0.75 持续超过2游戏天
        /// </summary>
        public static class CortisolOverloadExhaustion
        {
            public const float GlobalEfficiency = 0.70f;  // 所有属性效率 -30%
            public const float MoveSpeed = 0.80f;          // 移动速度 -20%
            public const float MoodOffset = -30f;          // 心情偏移 -30
            public const float Consciousness = -0.30f;     // 意识 -30%
        }

        #endregion
    }
}