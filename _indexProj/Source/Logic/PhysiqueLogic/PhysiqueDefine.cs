using System.Collections.Generic;

namespace Hormones
{
    // ============================================================
    // 体魄相关常量定义
    // ============================================================
    // 本文件包含所有与体魄系统相关的静态常量配置
    // 通过 partial class 方式扩展 Define 类，避免修改现有引用
    // ============================================================

    public static partial class Define
    {
        // ============================================================
        // 体魄基础配置
        // ============================================================
        public const int PhysiqueMinLevel = 0;
        public const int PhysiqueMaxLevel = 20;

        public const float PhysiqueHormonesRecoveryBonusFactor = 0.5f;
        public const float PhysiqueHormonesDamageReductionFactor = 0.5f;

        // ============================================================
        // 代谢率配置
        // ============================================================
        public const float MetabolicRateBase = 0.85f;
        public const float MetabolicRatePerPhysique = 0.02f;

        // ============================================================
        // 食欲配置
        // ============================================================
        public const float AppetiteBase = 0.66f;
        public const float AppetitePerPhysique = 0.067f;
        public const float AppetiteMinMultiplier = 0.66f;
        public const float AppetiteMaxMultiplier = 2.0f;

        // ============================================================
        // 体魄战斗加成阈值配置
        // ============================================================
        public const int PhysiqueNegativeThresholdHigh = 5;
        public const int PhysiqueNegativeThresholdLow = 7;
        public const int PhysiquePositiveThreshold = 8;

        public const float PhysiqueLowPenalty = 0.7f;
        public const float PhysiqueMediumPenalty = 0.9f;
        public const float PhysiqueBonusPerLevel = 0.015f;

        // ============================================================
        // 工作效率配置
        // ============================================================
        public const float WorkEfficiencyBase = 0.8f;
        public const float WorkEfficiencyPerPhysique = 0.03f;
        public const float WorkEfficiencyMin = 0.8f;
        public const float WorkEfficiencyMax = 1.2f;

        // ============================================================
        // 饥饿速率配置
        // ============================================================
        public const float HungerRateBase = 0.66f;
        public const float HungerRatePerPhysique = 0.05f;
        public const float HungerRateMin = 0.66f;
        public const float HungerRateMax = 1.66f;

        // ============================================================
        // 肾上腺素体魄修正配置
        // ============================================================
        public const int PhysiqueAdrenalinePenaltyThreshold = 8;
        public const int PhysiqueAdrenalineExemptionThreshold = 13;
        public const float PhysiqueAdrenalinePenaltyFactor = 0.5f;

        // ============================================================
        // 皮质醇体魄修正配置
        // ============================================================
        public const int PhysiqueCortisolPenaltyThreshold = 8;
        public const int PhysiqueCortisolBonusThreshold = 13;
        public const float PhysiqueCortisolPenaltyFactor = 0.5f;
        public const float PhysiqueCortisolBonusFactor = 1.2f;

        // ============================================================
        // 体魄阶段阈值配置（对应 Hediff_PhysiqueDisplay.xml）
        // ============================================================
        // Frail:   0-4    (Severity 0.00-0.24)
        // Average: 5-7    (Severity 0.25-0.39)
        // Fit:     8-12   (Severity 0.40-0.64)
        // Strong:  13-16  (Severity 0.65-0.84)
        // Peak:    17-20  (Severity 0.85-1.0)
        public const int PhysiqueStageAverage = 5;
        public const int PhysiqueStageFit = 8;
        public const int PhysiqueStageStrong = 13;
        public const int PhysiqueStagePeak = 17;

        // ============================================================
        // 体魄经验获取配置
        // ============================================================
        public const float PhysiqueXPPerTreeCut = 25f;

        // ============================================================
        // 肌肉劳损基础配置
        // ============================================================
        public const float MuscleStrainBaseRecoveryPerHour = 100f;
        public const float MuscleStrainDurationHours = 4f;
        public const float MuscleStrainOrganEfficiencyPenalty = -0.05f;
        public const int MuscleStrainMaxSeverity = 3;

        // ============================================================
        // 肌肉劳损操作配置
        // ============================================================
        public const float MiningXP = 100f;
        public const float MiningMuscleStrain = 50f;
        public const float MiningStrainChance = 0.06f;

        public const float TreeCutXP = 50f;
        public const float TreeCutMuscleStrain = 20f;
        public const float TreeCutStrainChance = 0.03f;

        public const float PlantCutXP = 8f;
        public const float PlantCutMuscleStrain = 2f;
        public const float PlantCutStrainChance = 0.01f;

        public const float HarvestXP = 25f;
        public const float HarvestMuscleStrain = 2f;
        public const float HarvestStrainChance = 0.01f;

        public const float ButcherXP = 25f;
        public const float ButcherMuscleStrain = 10f;
        public const float ButcherStrainChance = 0.03f;

        public const float HaulXP = 25f;
        public const float HaulMuscleStrain = 20f;
        public const float HaulStrainChance = 0.01f;

        // ============================================================
        // 体魄阶段 - 肌肉劳损配置
        // ============================================================
        // frail 虚弱
        public const float PhysiqueStageFrailMuscleStrainMax = 650f;
        public const float PhysiqueStageFrailStrainChanceMultiplier = 1.25f;
        public const float PhysiqueStageFrailStrainRecoveryMultiplier = 0.9f;
        // average 一般
        public const float PhysiqueStageAverageMuscleStrainMax = 1000f;
        public const float PhysiqueStageAverageStrainChanceMultiplier = 1f;
        public const float PhysiqueStageAverageStrainRecoveryMultiplier = 1f;
        // fit 健康
        public const float PhysiqueStageFitMuscleStrainMax = 1250f;
        public const float PhysiqueStageFitStrainChanceMultiplier = 0.75f;
        public const float PhysiqueStageFitStrainRecoveryMultiplier = 1.25f;
        // strong 强壮
        public const float PhysiqueStageStrongMuscleStrainMax = 2000f;
        public const float PhysiqueStageStrongStrainChanceMultiplier = 0.5f;
        public const float PhysiqueStageStrongStrainRecoveryMultiplier = 2f;
        // peak 卓越
        public const float PhysiqueStagePeakMuscleStrainMax = 3000f;
        public const float PhysiqueStagePeakStrainChanceMultiplier = 0.25f;
        public const float PhysiqueStagePeakStrainRecoveryMultiplier = 3f;
    }
}