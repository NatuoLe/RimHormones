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
        // 背景故事体魄修正配置
        // ============================================================
        // 仅当角色的基础体魄(技能等级 + 特质偏移，尚未叠加背景故事偏移)低于此阈值时，
        // 才叠加背景故事偏移；体魄已达此值及以上者不再享受背景故事加成，避免重复加成。
        // 对应 Config/BackstoryPhysique.xml 的关键词→偏移表。
        public const int PhysiqueBackstoryApplyThreshold = 6;

        // ============================================================
        // 体魄经验获取配置
        // ============================================================
        public const float PhysiqueXPPerTreeCut = 25f;

        // ============================================================
        // 肌肉劳损基础配置
        // ============================================================
        public const float MuscleStrainBaseRecoveryPerHour = 100f;
        // 娱乐(Joy)活动时的劳损恢复系数：相对睡觉恢复速率的比例（0.5 = 睡觉的一半）
        public const float MuscleStrainJoyRecoveryFactor = 0.5f;
        public const float MuscleStrainDurationHours = 4f;
        public const float MuscleStrainOrganEfficiencyPenalty = -0.05f;
        public const int MuscleStrainMaxSeverity = 3;

        // ============================================================
        // 肌肉劳损操作配置
        // ============================================================
        // 【劳损基础值重新标定 2026-07-26】
        //   语义：每个结算周期（WorkTicksPerSettle=400tick≈6.6秒）扣一次。
        //   标定基准：一般体魄(上限1000, 扣速×1.0)持续中等劳作约一个白天(12游戏时≈75次结算)到底；
        //             虚弱(上限650, 扣速×2.0)持续劳作约半天(6游戏时≈37.5次)到底。
        //   → 中活基准 ≈13/次(75×13≈975≈1000)；虚弱37.5×(13×2)=975>650 ⇒ 半天前就到底 ✔
        //   工作分档：重活(挖矿/砍树/建造/拆除)=15、中活(搬运/宰杀)=13、轻活(收割/割草/播种)=6
        public const float MiningXP = 100f;
        public const float MiningMuscleStrain = 15f;
        public const float MiningStrainChance = 0.06f;

        public const float TreeCutXP = 50f;
        public const float TreeCutMuscleStrain = 15f;
        public const float TreeCutStrainChance = 0.03f;

        public const float PlantCutXP = 8f;
        public const float PlantCutMuscleStrain = 6f;
        public const float PlantCutStrainChance = 0.01f;

        public const float HarvestXP = 25f;
        public const float HarvestMuscleStrain = 6f;
        public const float HarvestStrainChance = 0.01f;

        public const float ButcherXP = 25f;
        public const float ButcherMuscleStrain = 13f;
        public const float ButcherStrainChance = 0.03f;

        public const float HaulXP = 25f;
        public const float HaulMuscleStrain = 13f;
        public const float HaulStrainChance = 0.01f;

        // ============================================================
        // B: 新增工作类型（建造/拆除/种植）
        // ============================================================
        public const float FinishFrameXP = 40f;
        public const float FinishFrameMuscleStrain = 15f;
        public const float FinishFrameStrainChance = 0.02f;

        public const float DeconstructXP = 30f;
        public const float DeconstructMuscleStrain = 15f;
        public const float DeconstructStrainChance = 0.02f;

        public const float SowXP = 10f;
        public const float SowMuscleStrain = 6f;
        public const float SowStrainChance = 0.01f;

        // ============================================================
        // 【2026-07-26 补齐】其它遗漏的体力工作
        //   分档沿用：重活=15、中活=13、轻活=6
        // ============================================================
        // 挖树桩（重活，等同砍树）
        public const float ExtractTreeXP = 50f;
        public const float ExtractTreeMuscleStrain = 15f;
        public const float ExtractTreeStrainChance = 0.02f;

        // 操作深钻（重活）
        public const float DeepDrillXP = 60f;
        public const float DeepDrillMuscleStrain = 15f;
        public const float DeepDrillStrainChance = 0.03f;

        // 拆卸/拆除变体（重活，等同拆除）
        public const float UninstallXP = 30f;
        public const float UninstallMuscleStrain = 15f;
        public const float UninstallStrainChance = 0.02f;

        // 打磨地板/墙（重活，重复性体力）
        public const float SmoothXP = 40f;
        public const float SmoothMuscleStrain = 15f;
        public const float SmoothStrainChance = 0.02f;

        // 打猎（中活，追击+射击）
        public const float HuntXP = 30f;
        public const float HuntMuscleStrain = 13f;
        public const float HuntStrainChance = 0.02f;

        // 修理/维修故障建筑（中活）
        public const float RepairXP = 25f;
        public const float RepairMuscleStrain = 13f;
        public const float RepairStrainChance = 0.01f;

        // 移植/播种（轻活，等同种植）
        public const float ReplantXP = 10f;
        public const float ReplantMuscleStrain = 6f;
        public const float ReplantStrainChance = 0.01f;

        // ============================================================
        // A: 按工作时长累计结算配置
        // ============================================================
        // 60 tick = 1 游戏秒。默认 400 tick ≈ 6.6 秒持续劳作结算一次。
        // 玩家不可调（结算节奏），只调门槛与概率（见 Settings）。
        public const int WorkTicksPerSettle = 400;

        // 拉伤触发体力门槛默认值（CurLevel < MaxLevel * 此值 时才 roll 拉伤）
        public const float DefaultStrainTriggerThresholdPct = 0.10f;
        // 拉伤概率总倍率默认值
        public const float DefaultStrainChanceMultiplier = 1.0f;

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

        // ============================================================
        // 【阶段劳损扣减倍率 2026-07-26】干活时基础strain × 此倍率
        //   虚弱扣得快(易累)、强壮扛得住。让"体魄成长=更能扛"手感明确。
        //   虚弱×2.0：上限650、每次中活扣26 ⇒ 约25次(≈4游戏时)就到底，半天内必垮
        // ============================================================
        public const float PhysiqueStageFrailStrainConsumeMultiplier = 2.0f;
        public const float PhysiqueStageAverageStrainConsumeMultiplier = 1.0f;
        public const float PhysiqueStageFitStrainConsumeMultiplier = 0.75f;
        public const float PhysiqueStageStrongStrainConsumeMultiplier = 0.5f;
        public const float PhysiqueStagePeakStrainConsumeMultiplier = 0.3f;

        // ============================================================
        // 【锻炼配置 2026-07-30】锻炼消耗体力储备（劳损），并设最低门槛
        // ============================================================
        // 锻炼每 tick 扣除的“劳损储备”基础值（会再乘体魄消耗倍率）。
        //   基准：一般体魄(上限1000,倍率×1.0)锻炼满一个疗程 5000tick 约扣 400，
        //   即一次完整锻炼消耗约 40% 体力储备。0.08/tick × 5000 = 400。
        public const float ExerciseStrainPerTick = 0.08f;
        // 锻炼所需的最低体力储备比例：CurLevel/MaxLevel 低于此值则无法开始锻炼。
        //   语义与拉伤门槛相反——这是“太累了练不动”的下限。
        public const float ExerciseMinStrainPct = 0.35f;

        // ============================================================
        // 【体魄日常衰减 2026-07-30】用进废退
        //   语义：每累计满 DecayTicksPerDay(=一个游戏日) 结算一次。
        //   当天若发生过任意体力劳作或锻炼 → 打“今日已活动”标记 → 本日不衰减；
        //   完全闲置的一天，才按体魄阶段扣 Physique 技能经验。
        //   越高等级衰减越多（顶级身材维护成本最高），虚弱阶段保底不衰减。
        //   最终扣减 = 阶段基础值 × PhysiqueDecayGlobalMult（玩家可调，0=关闭）。
        // ============================================================
        // 一个游戏日 = 60000 tick。
        public const int DecayTicksPerDay = 60000;
        // 各阶段每日闲置衰减的 Physique 经验（XP/天）。
        public const float PhysiqueDecayFrail = 0f;    // 虚弱 0-4：保底不衰减
        public const float PhysiqueDecayAverage = 2f;  // 一般 5-7
        public const float PhysiqueDecayFit = 4f;      // 健康 8-12
        public const float PhysiqueDecayStrong = 8f;   // 强壮 13-16
        public const float PhysiqueDecayPeak = 14f;    // 卓越 17-20
        // 玩家可调总倍率默认值（1.0=默认，0=完全关闭衰减）。
        public const float DefaultPhysiqueDecayGlobalMult = 1f;

        // ============================================================
        // 【肾上腺素长期堆积损伤 2026-08-02】
        //   语义：不同于「战斗透支」（每次攻击判定一次），这是持续高肾上腺素
        //   状态本身造成的慢性损伤——每 BuildupCheckIntervalTicks 检测一次。
        //   概率公式：chance = (Base − PerPhysique × 体魄等级) × 总倍率
        //     体魄 0  → 1.00%/次
        //     体魄 12 → 0.30%/次
        //     体魄 ≥13 → 0（复用 IsAdrenalineExempt 豁免）
        //   换算：持续高肾上腺素约十几分钟~近一小时吃一次损伤（长期堆积而非瞬时爆伤）。
        //   阶段 → 档位映射（Low 不触发 / Medium→中度 / High→重度）配置在
        //   Defs/MiscDefs/StrainAdrenalineStageRules.xml，改映射无需编译。
        //   参与抽取的损伤由 HediffDef 的 StrainHediffExt.onAdrenalineBuildup 决定。
        // ============================================================
        // 检测间隔：600 tick = 10 游戏秒。
        public const int AdrenalineBuildupCheckIntervalTicks = 600;
        // 体魄 0 时的单次触发概率。
        public const float AdrenalineBuildupBaseChance = 0.010f;
        // 每点体魄递减的概率（(0.010 − 0.003) / 12 ≈ 0.000583）。
        public const float AdrenalineBuildupChancePerPhysique = 0.000583f;
        // 玩家可调总倍率默认值（1.0=默认，0=完全关闭长期堆积损伤）。
        public const float DefaultAdrenalineBuildupGlobalMult = 1f;
    }
}