using RimWorld;
using Verse;

namespace Hormones
{
    // ============================================================
    // 体魄逻辑模块设计文档
    // ============================================================
    // 模块职责：
    //   1. 统一管理体魄等级的计算与查询（包含特质偏移和上限修正）
    //   2. 提供体魄对各项游戏属性的影响计算（工作效率、代谢率、食欲等）
    //   3. 提供体魄对激素系统的修正系数（肾上腺素惩罚豁免、皮质醇修正）
    //   4. 作为体魄逻辑的唯一权威来源（Single Source of Truth），消除代码冗余
    //
    // 核心设计原则：
    //   - 单一职责：只处理体魄相关的计算逻辑
    //   - 无状态：所有方法均为静态方法，不维护实例状态
    //   - 可测试：计算逻辑独立，易于单元测试
    //   - 集中配置：所有常量通过 Define 类集中管理，便于调参
    //
    // 依赖关系：
    //   - 输入依赖：Define（常量配置）、PhysiqueTraitUtility（特质偏移计算）、Helpers（通用工具方法）
    //   - 输出消费者：HormonesLogic、AdrenalineLogic、AdrenalineProducer、HormonesComponent、
    //                 HediffComp_PhysiqueDisplay
    //
    // 体魄等级计算模型：
    //   PhysiqueLevel = BaseSkillLevel + TraitOffset
    //   最终等级 = Clamp(PhysiqueLevel, PhysiqueMinLevel, PhysiqueMaxLevel + TraitCapOffset)
    //   其中：BaseSkillLevel 来自技能系统的 "Physique" 技能
    //
    // 数值设计说明：
    //   - 体魄范围：0~20（基础），受特质影响可扩展上限
    //   - 负面阈值：<5 重度惩罚，5~7 轻度惩罚
    //   - 正面阈值：>=8 开始获得正向加成，每级 +1.5% 战斗加成
    // ============================================================

    /// <summary>
    /// 体魄阶段枚举
    /// 根据 Hediff_PhysiqueDisplay.xml 中定义的阶段划分
    /// </summary>
    public enum PhysiqueStage
    {
        /// <summary>
        /// 虚弱：体魄 0-4 (Severity 0.00-0.24)
        /// 移动 -10%，操作 -10%，工作速度 85%，饥饿速率 -15%
        /// </summary>
        Frail,
        
        /// <summary>
        /// 一般：体魄 5-7 (Severity 0.25-0.39)
        /// 工作速度 90%，饥饿速率 -5%
        /// </summary>
        Average,
        
        /// <summary>
        /// 健康：体魄 8-12 (Severity 0.40-0.64)
        /// 移动 +5%，工作速度 105%
        /// </summary>
        Fit,
        
        /// <summary>
        /// 强壮：体魄 13-16 (Severity 0.65-0.84)
        /// 移动 +10%，操作 +5%，工作速度 110%，饥饿速率 +10%
        /// </summary>
        Strong,
        
        /// <summary>
        /// 卓越：体魄 17-20 (Severity 0.85-1.0)
        /// 移动 +15%，操作 +10%，呼吸 +5%，工作速度 119.5%，饥饿速率 +20%
        /// </summary>
        Peak
    }

    /// <summary>
    /// 体魄逻辑核心类
    /// 提供体魄等级计算、属性影响、激素修正等所有体魄相关的计算方法
    /// </summary>
    public static class PhysiqueLgc
    {
        /// <summary>
        /// 获取角色的体魄等级
        /// 计算公式：基础技能等级 + 特质偏移，结果限制在 [PhysiqueMinLevel, PhysiqueMaxLevel + 特质上限偏移] 范围内
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>计算后的体魄等级（默认返回1）</returns>
        /// <summary>
        /// 是否为激素/体魄系统的作用对象。
        /// 整套系统（肾上腺素、皮质醇、体魄、肌肉拉伤、透支损伤）仅对【类人生物】生效，
        /// 动物、机械体等非类人 pawn 一律排除，避免在它们身上产生身体损伤 Hediff。
        /// </summary>
        public static bool IsHormoneSubject(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return false;
            return true;
        }

        public static int GetPhysiqueLevel(Pawn pawn)
        {
            if (pawn == null) return 1;

            SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueSkillDef == null) return 1;

            SkillRecord skill = pawn.skills?.GetSkill(physiqueSkillDef);
            int level = skill?.levelInt ?? 1;

            int traitOffset = PhysiqueTraitUtility.GetTotalPhysiqueOffset(pawn);
            level += traitOffset;

            int maxLevel = Define.PhysiqueMaxLevel + PhysiqueTraitUtility.GetTotalCapOffset(pawn);
            return Helpers.Clamp(level, Define.PhysiqueMinLevel, maxLevel);
        }

        /// <summary>
        /// 开局一次性把背景故事体魄偏移烤进 Physique 技能等级。
        /// 仅作用于【开局自选殖民民】（Find.GameInitData.startingAndOptionalPawns），
        /// 且仅当角色的【基础体魄(技能+特质)】低于阈值时才叠加；已强健者不享受。
        /// 由 GameComponent.StartedNewGame 调用，只在开新局时触发一次，
        /// 之后偏移就是角色真实的体魄，会随训练/自然衰减正常变化。
        /// 查询期（GetPhysiqueLevel）不再处理背景故事偏移，避免重复叠加。
        /// </summary>
        public static void BakeBackstoryBonusForStartingPawns()
        {
            if (Find.GameInitData == null) return;

            SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueSkillDef == null) return;

            foreach (Pawn pawn in Find.GameInitData.startingAndOptionalPawns)
            {
                if (!IsHormoneSubject(pawn)) continue;

                SkillRecord skill = pawn.skills?.GetSkill(physiqueSkillDef);
                if (skill == null) continue;

                int baseLevel = skill.levelInt + PhysiqueTraitUtility.GetTotalPhysiqueOffset(pawn);
                if (baseLevel >= Define.PhysiqueBackstoryApplyThreshold) continue;

                int backstoryOffset = BackstoryPhysiqueConfig.GetBackstoryPhysiqueOffset(pawn);
                if (backstoryOffset == 0) continue;

                int newLevel = Helpers.Clamp(skill.levelInt + backstoryOffset, Define.PhysiqueMinLevel, Define.PhysiqueMaxLevel);
                if (newLevel != skill.levelInt)
                {
                    skill.levelInt = newLevel;
                    Log.Message($"[Hormones] 开局背景故事体魄加成：{pawn.LabelShort} 体魄 {skill.levelInt} → {newLevel}（{(backstoryOffset >= 0 ? "+" : "")}{backstoryOffset}，仅开局殖民民且基础<{Define.PhysiqueBackstoryApplyThreshold}）");
                }
            }
        }

        /// <summary>
        /// 获取角色的背景故事体魄修正值（委托给 BackstoryPhysiqueConfig 的配置驱动实现）。
        /// 保留此方法仅为兼容既有调用约定；实际计算已转到 BackstoryPhysiqueConfig。
        /// </summary>
        public static int GetBackstoryPhysiqueOffset(Pawn pawn)
        {
            return BackstoryPhysiqueConfig.GetBackstoryPhysiqueOffset(pawn);
        }

        /// <summary>
        /// 获取角色的体魄阶段
        /// 根据 Hediff_PhysiqueDisplay.xml 中定义的五个阶段划分
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>体魄阶段枚举值</returns>
        public static PhysiqueStage GetPhysiqueStage(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);

            if (physiqueLevel < Define.PhysiqueStageAverage)
            {
                return PhysiqueStage.Frail;
            }
            else if (physiqueLevel < Define.PhysiqueStageFit)
            {
                return PhysiqueStage.Average;
            }
            else if (physiqueLevel < Define.PhysiqueStageStrong)
            {
                return PhysiqueStage.Fit;
            }
            else if (physiqueLevel < Define.PhysiqueStagePeak)
            {
                return PhysiqueStage.Strong;
            }
            else
            {
                return PhysiqueStage.Peak;
            }
        }

        /// <summary>
        /// 获取体魄对战斗命中的加成倍率
        /// 根据 Hediff_PhysiqueDisplay.xml 中的阶段设计
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>体魄战斗加成倍率</returns>
        public static float GetPhysiqueBonus(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return 0.90f;
                case PhysiqueStage.Average:
                    return 1.0f;
                case PhysiqueStage.Fit:
                    return 1.10f;
                case PhysiqueStage.Strong:
                    return 1.10f;
                case PhysiqueStage.Peak:
                    return 1.20f;
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// 获取体魄对工作效率的影响
        /// 根据 Hediff_PhysiqueDisplay.xml 中的阶段设计
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>工作效率倍率</returns>
        public static float GetWorkEfficiency(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return 0.90f;
                case PhysiqueStage.Average:
                    return 0.95f;
                case PhysiqueStage.Fit:
                    return 1.0f;
                case PhysiqueStage.Strong:
                    return 1.03f;
                case PhysiqueStage.Peak:
                    return 1.08f;
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// 获取体魄对饥饿速率的影响
        /// 根据 Hediff_PhysiqueDisplay.xml 中的阶段设计
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>饥饿速率倍率</returns>
        public static float GetHungerRate(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return 0.80f;
                case PhysiqueStage.Average:
                    return 1.0f;
                case PhysiqueStage.Fit:
                    return 1.20f;
                case PhysiqueStage.Strong:
                    return 1.60f;
                case PhysiqueStage.Peak:
                    return 2.0f;
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// 获取体魄对代谢率的影响
        /// 根据 Hediff_PhysiqueDisplay.xml 中的阶段设计
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>代谢率倍率</returns>
        public static float GetMetabolicRate(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return 0.95f;
                case PhysiqueStage.Average:
                    return 1.0f;
                case PhysiqueStage.Fit:
                    return 1.03f;
                case PhysiqueStage.Strong:
                    return 1.05f;
                case PhysiqueStage.Peak:
                    return 1.15f;
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// 获取体魄对食欲的影响
        /// 根据 Hediff_PhysiqueDisplay.xml 中的阶段设计
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>食欲倍率</returns>
        public static float GetAppetiteMultiplier(Pawn pawn)
        {
            return GetHungerRate(pawn);
        }

        /// <summary>
        /// 将体魄战斗加成应用到命中几率
        /// 通过引用参数直接修改原始命中几率
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <param name="hitChance">命中几率（引用传递，会被修改）</param>
        public static void ApplyPhysiqueCombatBonus(Pawn pawn, ref float hitChance)
        {
            float physiqueBonus = GetPhysiqueBonus(pawn);
            hitChance *= physiqueBonus;
        }

        /// <summary>
        /// 获取体魄对激素恢复速度的加成
        /// 计算公式：1 + (level - 1) / (maxLevel - 1) × PhysiqueHormonesRecoveryBonusFactor
        /// 设计意图：体魄越高，身体恢复激素的能力越强
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>激素恢复加成倍率（范围：1.0 ~ 1.5）</returns>
        public static float GetRecoveryBonus(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            return 1f + (float)(physiqueLevel - 1) / (float)(Define.PhysiqueMaxLevel - 1) * Define.PhysiqueHormonesRecoveryBonusFactor;
        }

        /// <summary>
        /// 获取体魄对激素伤害的减免因子
        /// 计算公式：1 - (level - 1) / (maxLevel - 1) × PhysiqueHormonesDamageReductionFactor
        /// 设计意图：体魄越高，身体抵抗激素带来的负面影响能力越强
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>伤害减免因子（范围：0.5 ~ 1.0）</returns>
        public static float GetDamageReductionFactor(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            return 1f - (float)(physiqueLevel - 1) / (float)(Define.PhysiqueMaxLevel - 1) * Define.PhysiqueHormonesDamageReductionFactor;
        }

        /// <summary>
        /// 获取体魄对肾上腺素效果的修正系数
        /// 体魄 < 8: 0.5（惩罚：肾上腺素负面影响加重）
        /// 8 ≤ 体魄 < 13: 1.0（正常）
        /// 体魄 ≥ 13: 1.0（豁免：无额外修正）
        /// 
        /// 设计说明：中间区间（8~12）与正常区间（≥13）均返回1.0，
        /// 这是因为豁免判定由 IsAdrenalineExempt 单独处理，
        /// 该方法主要用于对肾上腺素效果强度的缩放修正。
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>肾上腺素体魄修正系数</returns>
        public static float GetAdrenalinePhysiqueModifier(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            
            if (physiqueLevel < Define.PhysiqueAdrenalinePenaltyThreshold)
            {
                return Define.PhysiqueAdrenalinePenaltyFactor;
            }
            
            return 1f;
        }

        /// <summary>
        /// 判断角色是否豁免肾上腺素惩罚效果
        /// 豁免条件：体魄等级 ≥ PhysiqueAdrenalineExemptionThreshold（13）
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>true 表示豁免，false 表示受影响</returns>
        public static bool IsAdrenalineExempt(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            return physiqueLevel >= Define.PhysiqueAdrenalineExemptionThreshold;
        }

        /// <summary>
        /// 获取体魄对皮质醇的修正系数
        /// 体魄 < 8: 0.5（惩罚：皮质醇上升更快、下降更慢）
        /// 8 ≤ 体魄 < 13: 1.0（正常）
        /// 体魄 ≥ 13: 1.2（增益：皮质醇积聚较慢、消退较快）
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>皮质醇体魄修正系数</returns>
        public static float GetCortisolPhysiqueModifier(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            
            if (physiqueLevel < Define.PhysiqueCortisolPenaltyThreshold)
            {
                return Define.PhysiqueCortisolPenaltyFactor;
            }
            
            if (physiqueLevel >= Define.PhysiqueCortisolBonusThreshold)
            {
                return Define.PhysiqueCortisolBonusFactor;
            }
            
            return 1f;
        }

        /// <summary>
        /// 获取肌肉劳损最大值
        /// 根据体魄阶段返回对应的肌肉劳损上限
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>肌肉劳损最大值</returns>
        public static float GetMuscleStrainMax(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return Define.PhysiqueStageFrailMuscleStrainMax;
                case PhysiqueStage.Average:
                    return Define.PhysiqueStageAverageMuscleStrainMax;
                case PhysiqueStage.Fit:
                    return Define.PhysiqueStageFitMuscleStrainMax;
                case PhysiqueStage.Strong:
                    return Define.PhysiqueStageStrongMuscleStrainMax;
                case PhysiqueStage.Peak:
                    return Define.PhysiqueStagePeakMuscleStrainMax;
                default:
                    return Define.PhysiqueStageAverageMuscleStrainMax;
            }
        }

        /// <summary>
        /// 获取肌肉劳损【扣减】倍率：干活时基础strain × 此倍率。
        /// 虚弱扣得快(易累)、强壮扛得住。体现"体魄越强越能持续劳作"。
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>劳损扣减倍率</returns>
        public static float GetMuscleStrainConsumeMultiplier(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);

            float stageMult;
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    stageMult = Define.PhysiqueStageFrailStrainConsumeMultiplier; break;
                case PhysiqueStage.Average:
                    stageMult = Define.PhysiqueStageAverageStrainConsumeMultiplier; break;
                case PhysiqueStage.Fit:
                    stageMult = Define.PhysiqueStageFitStrainConsumeMultiplier; break;
                case PhysiqueStage.Strong:
                    stageMult = Define.PhysiqueStageStrongStrainConsumeMultiplier; break;
                case PhysiqueStage.Peak:
                    stageMult = Define.PhysiqueStagePeakStrainConsumeMultiplier; break;
                default:
                    stageMult = Define.PhysiqueStageAverageStrainConsumeMultiplier; break;
            }

            // 【2026-08-04 饮品 Buff】电解质水/功能饮品生效中：劳损扣减效率 -25%。
            // 与 Need_Cortisol 的 DrinkMilkTea 检测同一惯例（按 defName 查饮品 hediff）。
            if (pawn?.health != null)
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("DrinkElectrolyte"), false) != null
                    || pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("DrinkEnergyDrink"), false) != null)
                {
                    stageMult *= 0.75f;
                }
            }
            return stageMult;
        }

        /// <summary>
        /// 获取肌肉拉伤概率倍率
        /// 倍率 > 1 表示更容易拉伤，< 1 表示更不容易拉伤
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>肌肉拉伤概率倍率</returns>
        public static float GetMuscleStrainChanceMultiplier(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return Define.PhysiqueStageFrailStrainChanceMultiplier;
                case PhysiqueStage.Average:
                    return Define.PhysiqueStageAverageStrainChanceMultiplier;
                case PhysiqueStage.Fit:
                    return Define.PhysiqueStageFitStrainChanceMultiplier;
                case PhysiqueStage.Strong:
                    return Define.PhysiqueStageStrongStrainChanceMultiplier;
                case PhysiqueStage.Peak:
                    return Define.PhysiqueStagePeakStrainChanceMultiplier;
                default:
                    return Define.PhysiqueStageAverageStrainChanceMultiplier;
            }
        }

        /// <summary>
        /// 获取肌肉劳损恢复速率（每小时）
        /// 基础恢复速率 × 体魄阶段恢复倍率
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>每小时恢复的肌肉劳损值</returns>
        public static float GetMuscleStrainRecoveryRate(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);
            float multiplier;
            
            switch (stage)
            {
                case PhysiqueStage.Frail:
                    multiplier = Define.PhysiqueStageFrailStrainRecoveryMultiplier;
                    break;
                case PhysiqueStage.Average:
                    multiplier = Define.PhysiqueStageAverageStrainRecoveryMultiplier;
                    break;
                case PhysiqueStage.Fit:
                    multiplier = Define.PhysiqueStageFitStrainRecoveryMultiplier;
                    break;
                case PhysiqueStage.Strong:
                    multiplier = Define.PhysiqueStageStrongStrainRecoveryMultiplier;
                    break;
                case PhysiqueStage.Peak:
                    multiplier = Define.PhysiqueStagePeakStrainRecoveryMultiplier;
                    break;
                default:
                    multiplier = Define.PhysiqueStageAverageStrainRecoveryMultiplier;
                    break;
            }
            
            return Define.MuscleStrainBaseRecoveryPerHour * multiplier;
        }

        /// <summary>
        /// 获取体魄【日常衰减】的基础 XP（用进废退）。
        /// 仅在“当天完全无体力活动”时才由 HormonesComponent 结算扣除。
        /// 越高阶段衰减越多（维护顶级身材成本更高），虚弱阶段返回 0（保底）。
        /// 注意：此处不含玩家可调的总倍率，倍率在结算处统一乘。
        /// </summary>
        /// <param name="pawn">目标角色</param>
        /// <returns>该阶段每日闲置衰减的 Physique 经验</returns>
        public static float GetDailyDecayXP(Pawn pawn)
        {
            PhysiqueStage stage = GetPhysiqueStage(pawn);

            switch (stage)
            {
                case PhysiqueStage.Frail:
                    return Define.PhysiqueDecayFrail;
                case PhysiqueStage.Average:
                    return Define.PhysiqueDecayAverage;
                case PhysiqueStage.Fit:
                    return Define.PhysiqueDecayFit;
                case PhysiqueStage.Strong:
                    return Define.PhysiqueDecayStrong;
                case PhysiqueStage.Peak:
                    return Define.PhysiqueDecayPeak;
                default:
                    return Define.PhysiqueDecayAverage;
            }
        }
    }
}