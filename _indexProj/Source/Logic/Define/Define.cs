namespace Hormones
{
    public static class Define
    {
        public const string ModName = "RimHormones";

        public const float HormonesMaxLevel = 100f;
        public const float HormonesDecayRate = 0.5f;
        public const float HormonesBaseDamageReduction = 15f;
        public const float HormonesBleedingReductionFactor = 0.1f;

        public const float SevereBleedingThreshold = 2.5f;

        public const int PhysiqueMinLevel = 0;
        public const int PhysiqueMaxLevel = 20;

        public const float PhysiqueHormonesRecoveryBonusFactor = 0.5f;
        public const float PhysiqueHormonesDamageReductionFactor = 0.5f;

        public const float MetabolicRateBase = 0.85f;
        public const float MetabolicRatePerPhysique = 0.02f;

        public const float AppetiteBase = 0.66f;
        public const float AppetitePerPhysique = 0.067f;
        public const float AppetiteMinMultiplier = 0.66f;
        public const float AppetiteMaxMultiplier = 2.0f;

        public const int PhysiqueNegativeThresholdHigh = 5;
        public const int PhysiqueNegativeThresholdLow = 7;
        public const int PhysiquePositiveThreshold = 8;

        public const float PhysiqueLowPenalty = 0.7f;
        public const float PhysiqueMediumPenalty = 0.9f;
        public const float PhysiqueBonusPerLevel = 0.015f;

        public const float WorkEfficiencyBase = 0.8f;
        public const float WorkEfficiencyPerPhysique = 0.03f;
        public const float WorkEfficiencyMin = 0.8f;
        public const float WorkEfficiencyMax = 1.2f;

        public const float HungerRateBase = 0.66f;
        public const float HungerRatePerPhysique = 0.05f;
        public const float HungerRateMin = 0.66f;
        public const float HungerRateMax = 1.66f;
    }
}