using RimWorld;
using Verse;
using System;

namespace Hormones
{
    /// <summary>
    /// 皮质醇 HediffComp 属性类
    /// 用于配置皮质醇组件的行为参数
    /// </summary>
    public class HediffCompProperties_Cortisol : HediffCompProperties
    {
        public HediffCompProperties_Cortisol()
        {
            compClass = typeof(HediffComp_Cortisol);
        }
    }

    /// <summary>
    /// 皮质醇 Hediff 组件
    /// 负责管理皮质醇的动态变化、档位判断、以及与其他系统的联动
    /// </summary>
    public class HediffComp_Cortisol : HediffComp
    {
        public HediffCompProperties_Cortisol Props => (HediffCompProperties_Cortisol)props;

        // 更新间隔（tick），每60tick更新一次皮质醇
        private int ticksSinceLastUpdate = 0;
        private const int UpdateInterval = 60;

        // 过载状态追踪
        private int overloadStartTick = -1;
        
        // 每日检查计时器（用于神经衰弱判定）
        private int ticksSinceLastDailyCheck = 0;
        
        // 肾上腺素峰值追踪（用于战后疲惫机制）
        private float adrenalinePeakLast5Minutes = 0f;
        private int ticksSinceLastCombatCheck = 0;

        /// <summary>
        /// 序列化保存/加载数据
        /// </summary>
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksSinceLastUpdate, "ticksSinceLastUpdate");
            Scribe_Values.Look(ref overloadStartTick, "overloadStartTick");
            Scribe_Values.Look(ref ticksSinceLastDailyCheck, "ticksSinceLastDailyCheck");
            Scribe_Values.Look(ref adrenalinePeakLast5Minutes, "adrenalinePeakLast5Minutes");
            Scribe_Values.Look(ref ticksSinceLastCombatCheck, "ticksSinceLastCombatCheck");
        }

        /// <summary>
        /// 每tick执行的更新逻辑
        /// 处理皮质醇变化、神经衰弱判定、战后疲惫检测
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            ticksSinceLastUpdate++;
            ticksSinceLastDailyCheck++;
            ticksSinceLastCombatCheck++;

            // 更新肾上腺素峰值追踪（用于战后疲惫）
            UpdateAdrenalinePeakTracking();

            // 每60tick更新皮质醇浓度
            if (ticksSinceLastUpdate >= UpdateInterval)
            {
                ticksSinceLastUpdate = 0;
                CortisolLogic.ProcessCortisolTick(parent.pawn, this);
            }

            // 每日检查神经衰弱
            if (ticksSinceLastDailyCheck >= Define.CortisolDailyCheckInterval)
            {
                ticksSinceLastDailyCheck = 0;
                TryDailyNeurastheniaCheck();
            }

            // 检查战后疲惫（肾上腺素→皮质醇联动）
            CheckPostCombatFatigue();
        }

        /// <summary>
        /// 更新肾上腺素峰值追踪
        /// 记录过去5分钟内的最高肾上腺素值
        /// </summary>
        private void UpdateAdrenalinePeakTracking()
        {
            float currentAdrenaline = CortisolLogic.GetAdrenalineSeverity(parent.pawn);
            if (currentAdrenaline > adrenalinePeakLast5Minutes)
            {
                adrenalinePeakLast5Minutes = currentAdrenaline;
            }

            // 每5分钟重置一次追踪
            if (ticksSinceLastCombatCheck >= 3000)
            {
                adrenalinePeakLast5Minutes = 0f;
                ticksSinceLastCombatCheck = 0;
            }
        }

        /// <summary>
        /// 战后疲惫检测
        /// 每1分钟检测：过去5分钟内是否有肾上腺素 > 0.5
        /// 若成立，皮质醇增加 0.02
        /// </summary>
        private void CheckPostCombatFatigue()
        {
            if (ticksSinceLastCombatCheck >= 6000)
            {
                ticksSinceLastCombatCheck = 0;
                if (adrenalinePeakLast5Minutes > Define.AdrenalineCortisolLinkThreshold)
                {
                    float oldSeverity = parent.Severity;
                    parent.Severity = Mathf.Clamp01(parent.Severity + 0.02f);
                    adrenalinePeakLast5Minutes = 0f;
                    
                    // ========================================
                    // 【场景2：战斗后】战后疲惫日志
                    // ========================================
                    Log.Message($"[皮质醇-战后疲惫] {parent.pawn.Name?.ToStringFull ?? "Unknown"} " +
                              $"肾上腺素峰值: {adrenalinePeakLast5Minutes:F3} → " +
                              $"皮质醇: {oldSeverity:F3} → {parent.Severity:F3} (+0.02 战后疲惫)");
                }
            }
        }

        /// <summary>
        /// 每日神经衰弱检查
        /// 当皮质醇 ≥ 0.5 时触发判定
        /// </summary>
        private void TryDailyNeurastheniaCheck()
        {
            if (parent.Severity >= Define.CortisolThresholdStress)
            {
                CortisolLogic.TryTriggerNeurasthenia(parent.pawn, parent.Severity);
            }
        }

        /// <summary>
        /// 获取当前皮质醇档位
        /// </summary>
        /// <returns>CortisolLevel 枚举值</returns>
        public CortisolLevel GetCortisolLevel()
        {
            return CortisolLogic.GetCortisolLevel(parent.Severity);
        }

        /// <summary>
        /// 获取体魄修正系数
        /// </summary>
        /// <returns>修正系数（0.5/1.0/1.2）</returns>
        public float GetPhysiqueModifier()
        {
            return CortisolLogic.GetCortisolPhysiqueModifier(parent.pawn);
        }

        /// <summary>
        /// 设置过载开始时间
        /// </summary>
        /// <param name="tick">游戏tick值</param>
        public void SetOverloadStartTick(int tick)
        {
            overloadStartTick = tick;
        }

        /// <summary>
        /// 获取过载开始时间
        /// </summary>
        /// <returns>游戏tick值，-1表示未过载</returns>
        public int GetOverloadStartTick()
        {
            return overloadStartTick;
        }

        /// <summary>
        /// 判断是否处于过载档位
        /// </summary>
        /// <returns>true表示过载状态</returns>
        public bool IsInOverloadZone()
        {
            return parent.Severity >= Define.CortisolThresholdOverload;
        }

        /// <summary>
        /// 调整皮质醇浓度
        /// </summary>
        /// <param name="amount">调整量（正数增加，负数减少）</param>
        public void AdjustSeverity(float amount)
        {
            parent.Severity = Mathf.Clamp01(parent.Severity + amount);
        }

        /// <summary>
        /// 获取当前皮质醇浓度
        /// </summary>
        /// <returns>0~1之间的数值</returns>
        public float GetCurrentSeverity()
        {
            return parent.Severity;
        }

        /// <summary>
        /// 静态方法：获取指定 pawn 的皮质醇组件
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>皮质醇组件实例，不存在则返回null</returns>
        public static HediffComp_Cortisol GetCortisolComp(Pawn pawn)
        {
            Hediff cortisol = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Cortisol", false));
            return cortisol?.TryGetComp<HediffComp_Cortisol>();
        }
    }

    /// <summary>
    /// 皮质醇档位枚举
    /// </summary>
    public enum CortisolLevel
    {
        SleepDeprivation,  // 休眠不足（0 ≤ S < 0.15）
        Normal,            // 正常区间（0.15 ≤ S < 0.5）
        ChronicStress,     // 持续承压（0.5 ≤ S < 0.75）
        Overload           // 过载透支（0.75 ≤ S ≤ 1.0）
    }

    /// <summary>
    /// 皮质醇核心逻辑类
    /// 包含浓度计算、体魄修正、神经衰弱判定、过载耗竭处理等核心逻辑
    /// </summary>
    public static class CortisolLogic
    {
        // 随机种子，用于基础增长的随机化
        private static int lastRandomSeed = 0;

        /// <summary>
        /// 根据浓度值获取皮质醇档位
        /// </summary>
        /// <param name="severity">皮质醇浓度（0~1）</param>
        /// <returns>对应的档位枚举</returns>
        public static CortisolLevel GetCortisolLevel(float severity)
        {
            if (severity < Define.CortisolThresholdNormal)
                return CortisolLevel.SleepDeprivation;
            if (severity < Define.CortisolThresholdStress)
                return CortisolLevel.Normal;
            if (severity < Define.CortisolThresholdOverload)
                return CortisolLevel.ChronicStress;
            return CortisolLevel.Overload;
        }

        /// <summary>
        /// 计算体魄修正系数
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>修正系数（体魄<8:0.5，8≤体魄<13:1.0，体魄≥13:1.2）</returns>
        public static float GetCortisolPhysiqueModifier(Pawn pawn)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            if (physiqueLevel < Define.PhysiqueCortisolPenaltyThreshold)
                return Define.PhysiqueCortisolPenaltyFactor;
            if (physiqueLevel >= Define.PhysiqueCortisolBonusThreshold)
                return Define.PhysiqueCortisolBonusFactor;
            return 1.0f;
        }

        /// <summary>
        /// 处理皮质醇每tick的变化
        /// 计算所有增长源和衰减，应用体魄修正，更新浓度值
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="cortisolComp">皮质醇组件</param>
        public static void ProcessCortisolTick(Pawn pawn, HediffComp_Cortisol cortisolComp)
        {
            if (pawn == null || cortisolComp == null)
                return;

            float currentSeverity = cortisolComp.GetCurrentSeverity();
            float change = 0f;

            // 累加所有增长源
            float baseGrowth = GetBaseDailyGrowth();           // 基础每日增长
            float neurastheniaGrowth = GetNeurastheniaGrowth(pawn);    // 神经衰弱加速增长
            float chronicStressGrowth = GetChronicStressGrowth(pawn);   // 慢性压力环境增长
            float adrenalineGrowth = GetAdrenalineLinkGrowth(pawn);  // 肾上腺素联动增长
            float lowMoodGrowth = GetLowMoodGrowth(pawn);         // 低心情增长

            change += baseGrowth;
            change += neurastheniaGrowth;
            change += chronicStressGrowth;
            change += adrenalineGrowth;
            change += lowMoodGrowth;

            // 减去衰减
            float decay = GetDecay(pawn);
            change -= decay;

            // 应用体魄修正
            float physiqueMod = GetCortisolPhysiqueModifier(pawn);
            change *= physiqueMod;

            // 更新浓度值
            cortisolComp.AdjustSeverity(change);
            float newSeverity = cortisolComp.GetCurrentSeverity();

            // 处理过载耗竭
            HandleOverloadExhaustion(pawn, cortisolComp, newSeverity);

            // 通知健康系统变化
            NotifyHediffChanged(pawn);

            // ========================================
            // 【皮质醇更新日志】每60tick输出一次详细变化
            // ========================================
            LogCortisolUpdate(pawn, currentSeverity, newSeverity, change,
                            baseGrowth, neurastheniaGrowth, chronicStressGrowth, 
                            adrenalineGrowth, lowMoodGrowth, decay, physiqueMod);
        }

        /// <summary>
        /// 皮质醇更新日志输出
        /// </summary>
        private static void LogCortisolUpdate(Pawn pawn, float oldSev, float newSev, float netChange,
                                           float baseG, float neurasG, float stressG, 
                                           float adrG, float moodG, float decay, float physiqueMod)
        {
            string pawnName = pawn.Name?.ToStringFull ?? "Unknown";
            CortisolLevel level = GetCortisolLevel(newSev);
            int physiqueLevel = GetPhysiqueLevel(pawn);
            
            // 构建变化源字符串
            string sources = "";
            if (baseG > 0) sources += $"基础+{baseG:F4} ";
            if (neurasG > 0) sources += $"神经衰弱+{neurasG:F4} ";
            if (stressG > 0) sources += $"慢性压力+{stressG:F4} ";
            if (adrG > 0) sources += $"肾上腺素联动+{adrG:F4} ";
            if (moodG > 0) sources += $"低心情+{moodG:F4} ";
            if (decay > 0) sources += $"衰减-{decay:F4}";

            // 根据场景判断
            string scene = DetermineScene(baseG, neurasG, stressG, adrG, moodG, decay);

            Log.Message($"[皮质醇-更新] {pawnName} | " +
                       $"皮质醇: {oldSev:F3} → {newSev:F3} ({netChange:+0.0000;-0.0000;0}) | " +
                       $"档位: {level} | 体魄:{physiqueLevel}(×{physiqueMod:F1}) | " +
                       $"触发源: {sources} | {scene}");
        }

        /// <summary>
        /// 判断当前场景类型
        /// </summary>
        private static string DetermineScene(float baseG, float neurasG, float stressG, float adrG, float moodG, float decay)
        {
            // 场景4：神经衰弱恶性循环
            if (neurasG > 0 && decay <= Define.CortisolBaseDecay * 60)
                return "🔴 场景4-神经衰弱恶性循环";

            // 场景3：持续高压
            if (stressG > 0 || moodG > 0)
                return "⚠️ 场景3-持续高压";

            // 场景2：肾上腺素联动
            if (adrG > 0)
                return "⚠️ 场景2-肾上腺素联动";

            // 场景1：正常/健康
            if (decay > Define.CortisolBaseDecay * 60)
                return "✅ 场景1-充足休息恢复";

            return "✅ 正常波动";
        }

        /// <summary>
        /// 获取基础每日增长值（随机范围）
        /// 模拟正常生理节律下皮质醇的缓慢上升
        /// </summary>
        /// <returns>增长值（已乘以60tick）</returns>
        private static float GetBaseDailyGrowth()
        {
            lastRandomSeed++;
            Rand.PushState(lastRandomSeed);
            float growth = Rand.Range(Define.CortisolBaseGrowthMin, Define.CortisolBaseGrowthMax);
            Rand.PopState();
            return growth * 60f;
        }

        /// <summary>
        /// 获取神经衰弱状态下的额外增长
        /// 睡不好→皮质醇更高，形成恶性循环
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>增长值（已乘以60tick）</returns>
        private static float GetNeurastheniaGrowth(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false)))
            {
                return Define.CortisolNeurastheniaGrowth * 60f;
            }
            return 0f;
        }

        /// <summary>
        /// 获取慢性压力环境下的增长
        /// 包括：饥饿、恶劣环境（低温）等
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>增长值（已乘以60tick）</returns>
        private static float GetChronicStressGrowth(Pawn pawn)
        {
            // 饥饿检测
            if (pawn.needs?.food != null && pawn.needs.food.CurLevel < 0.3f)
            {
                return Define.CortisolChronicStressGrowth * 60f;
            }

            // 低温环境检测
            if (pawn.Position != null && pawn.Map != null)
            {
                Room room = pawn.GetRoom();
                if (room != null && room.Temperature < 5f)
                {
                    return Define.CortisolChronicStressGrowth * 60f;
                }
            }

            return 0f;
        }

        /// <summary>
        /// 获取肾上腺素联动增长
        /// 每次肾上腺素飙升，皮质醇跟着缓慢堆积
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>增长值（已乘以60tick）</returns>
        private static float GetAdrenalineLinkGrowth(Pawn pawn)
        {
            float adrenaline = GetAdrenalineSeverity(pawn);
            if (adrenaline > Define.AdrenalineCortisolLinkThreshold)
            {
                return Define.CortisolAdrenalineLinkGrowth * 60f;
            }
            return 0f;
        }

        /// <summary>
        /// 获取低心情状态下的增长
        /// 心情 < -10 时触发
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>增长值（已乘以60tick）</returns>
        private static float GetLowMoodGrowth(Pawn pawn)
        {
            if (pawn.needs?.mood == null)
                return 0f;

            float moodValue = pawn.needs.mood.CurLevel * 100f;
            if (moodValue < Define.CortisolMoodLowThreshold)
            {
                return Define.CortisolLowMoodGrowth * 60f;
            }
            return 0f;
        }

        /// <summary>
        /// 获取皮质醇衰减值
        /// 根据休息状态和心情动态调整衰减速率
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>衰减值（已乘以60tick）</returns>
        private static float GetDecay(Pawn pawn)
        {
            float decay = Define.CortisolBaseDecay;

            // 充足睡眠时加速衰减
            if (IsRestingWell(pawn))
            {
                decay = Define.CortisolRestDecay;
            }
            // 心情极好时也加速衰减
            else
            {
                float moodValue = pawn.needs?.mood?.CurLevel * 100f ?? 0f;
                if (moodValue > Define.CortisolMoodHighThreshold)
                {
                    decay = Define.CortisolHighMoodDecay;
                }
            }

            return decay * 60f;
        }

        /// <summary>
        /// 判断是否处于良好休息状态
        /// 影响皮质醇衰减速率
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>true表示休息良好</returns>
        private static bool IsRestingWell(Pawn pawn)
        {
            if (pawn.needs?.rest == null)
                return false;

            // 呕吐状态
            if (pawn.CurJobDef == DefDatabase<JobDef>.GetNamed("Vomit", false))
                return false;

            // 失眠状态
            if (pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("Insomnia", false)))
                return false;

            // 神经衰弱状态
            if (pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false)))
                return false;

            // 休息值足够高
            return pawn.needs.rest.CurLevel > Define.CortisolGoodRestThreshold;
        }

        /// <summary>
        /// 尝试触发神经衰弱
        /// 当皮质醇 ≥ 0.5 时调用
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="severity">当前皮质醇浓度</param>
        public static void TryTriggerNeurasthenia(Pawn pawn, float severity)
        {
            // 已有神经衰弱则不再触发
            if (pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false)))
                return;

            // 计算触发概率
            float probability = CalculateNeurastheniaProbability(pawn, severity);
            
            // ========================================
            // 【神经衰弱判定日志】
            // ========================================
            int physiqueLevel = GetPhysiqueLevel(pawn);
            Log.Message($"[皮质醇-神经衰弱判定] {pawn.Name?.ToStringFull ?? "Unknown"} | " +
                       $"皮质醇: {severity:F3} | 体魄: {physiqueLevel} | " +
                       $"触发概率: {probability:P1}");
            
            if (Rand.Value < probability)
            {
                ApplyNeurasthenia(pawn);
            }
        }

        /// <summary>
        /// 计算神经衰弱触发概率
        /// 公式：P = min(0.15 + 0.03 × S × (13 - P), 0.6)
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="severity">当前皮质醇浓度</param>
        /// <returns>触发概率（0~0.6）</returns>
        public static float CalculateNeurastheniaProbability(Pawn pawn, float severity)
        {
            int physiqueLevel = GetPhysiqueLevel(pawn);
            float baseProb = Define.CortisolNeurastheniaBaseProb;
            float severityFactor = Define.CortisolNeurastheniaSeverityFactor * severity;
            float physiqueFactor = 13 - physiqueLevel;

            float probability = baseProb + severityFactor * physiqueFactor;
            return Mathf.Clamp(probability, 0f, Define.CortisolNeurastheniaMaxProb);
        }

        /// <summary>
        /// 应用神经衰弱状态
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        public static void ApplyNeurasthenia(Pawn pawn)
        {
            HediffDef neurastheniaDef = DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false);
            if (neurastheniaDef == null)
                return;

            Hediff neurasthenia = HediffMaker.MakeHediff(neurastheniaDef, pawn);
            neurasthenia.Severity = 1.0f;
            
            // 设置持续时间：1天
            if (neurasthenia is HediffWithComps hediffWithComps)
            {
                HediffComp_Disappears disappearsComp = hediffWithComps.TryGetComp<HediffComp_Disappears>();
                if (disappearsComp != null)
                {
                    disappearsComp.ticksToDisappear = (int)(GenDate.TicksPerDay * 1);
                }
            }
            
            pawn.health.AddHediff(neurasthenia);
            
            // ========================================
            // 【场景4：神经衰弱触发】日志
            // ========================================
            Log.Warning($"[皮质醇-神经衰弱触发] 🔴🔴🔴 {pawn.Name?.ToStringFull ?? "Unknown"} " +
                       $"患上了神经衰弱！即将进入恶性循环...");
        }

        /// <summary>
        /// 处理过载耗竭状态
        /// 当皮质醇≥0.75持续超过2游戏天时触发
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="cortisolComp">皮质醇组件</param>
        /// <param name="severity">当前皮质醇浓度</param>
        private static void HandleOverloadExhaustion(Pawn pawn, HediffComp_Cortisol cortisolComp, float severity)
        {
            if (severity >= Define.CortisolThresholdOverload)
            {
                // 记录过载开始时间
                if (cortisolComp.GetOverloadStartTick() < 0)
                {
                    cortisolComp.SetOverloadStartTick(Find.TickManager.TicksGame);
                    
                    // ========================================
                    // 【过载开始】日志
                    // ========================================
                    Log.Warning($"[皮质醇-过载开始] ⚠️⚠️⚠️ {pawn.Name?.ToStringFull ?? "Unknown"} " +
                               $"进入了过载状态！皮质醇: {severity:F3} | " +
                               $"开始计时：需持续2游戏天后触发过载耗竭");
                }
                else
                {
                    // 检查是否持续超过2天
                    int overloadDuration = Find.TickManager.TicksGame - cortisolComp.GetOverloadStartTick();
                    int requiredTicks = (int)(Define.CortisolOverloadDurationHours * GenDate.TicksPerHour);
                    float hoursRemaining = (requiredTicks - overloadDuration) / (GenDate.TicksPerHour);
                    
                    // 每30分钟输出一次倒计时日志
                    if (hoursRemaining <= 0 || (overloadDuration % (GenDate.TicksPerHour / 2) == 0))
                    {
                        if (hoursRemaining > 0)
                        {
                            Log.Message($"[皮质醇-过载倒计时] {pawn.Name?.ToStringFull ?? "Unknown"} | " +
                                       $"还需 {hoursRemaining:F1} 小时触发过载耗竭 | " +
                                       $"已过载: {(overloadDuration / GenDate.TicksPerHour):F1}h");
                        }
                    }
                    
                    if (overloadDuration >= requiredTicks)
                    {
                        ApplyOverloadExhaustion(pawn);
                    }
                }
            }
            else
            {
                // 退出过载状态，移除耗竭效果
                if (cortisolComp.GetOverloadStartTick() >= 0)
                {
                    int previousDuration = Find.TickManager.TicksGame - cortisolComp.GetOverloadStartTick();
                    float previousHours = previousDuration / GenDate.TicksPerHour;
                    
                    cortisolComp.SetOverloadStartTick(-1);
                    RemoveOverloadExhaustion(pawn);
                    
                    // ========================================
                    // 【过载解除】日志
                    // ========================================
                    Log.Message($"[皮质醇-过载解除] ✅ {pawn.Name?.ToStringFull ?? "Unknown"} " +
                               $"皮质醇降至安全水平 | " +
                               $"本次过载持续: {previousHours:F1}h | " +
                               $"已移除过载耗竭效果");
                }
            }
        }

        /// <summary>
        /// 应用过载耗竭状态
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        public static void ApplyOverloadExhaustion(Pawn pawn)
        {
            HediffDef exhaustionDef = DefDatabase<HediffDef>.GetNamed("CortisolOverloadExhaustion", false);
            if (exhaustionDef == null)
                return;

            // 已有耗竭状态则不再添加
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(exhaustionDef);
            if (existing != null)
                return;

            Hediff exhaustion = HediffMaker.MakeHediff(exhaustionDef, pawn);
            exhaustion.Severity = 1.0f;
            pawn.health.AddHediff(exhaustion);

            // ========================================
            // 【过载耗竭触发】日志
            // ========================================
            Log.Error($"[皮质醇-过载耗竭触发] 💀💀💀 {pawn.Name?.ToStringFull ?? "Unknown"} " +
                     $"触发了过载耗竭！所有属性效率-30%，心情-30！");
        }

        /// <summary>
        /// 移除过载耗竭状态
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        public static void RemoveOverloadExhaustion(Pawn pawn)
        {
            HediffDef exhaustionDef = DefDatabase<HediffDef>.GetNamed("CortisolOverloadExhaustion", false);
            Hediff exhaustion = pawn.health.hediffSet.GetFirstHediffOfDef(exhaustionDef);
            if (exhaustion != null)
            {
                pawn.health.RemoveHediff(exhaustion);
            }
        }

        /// <summary>
        /// 获取指定pawn的肾上腺素浓度
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>肾上腺素浓度（0~1）</returns>
        public static float GetAdrenalineSeverity(Pawn pawn)
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            return adrenaline?.Severity ?? 0f;
        }

        /// <summary>
        /// 获取指定pawn的体魄等级
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>体魄等级（0~20），默认返回8</returns>
        public static int GetPhysiqueLevel(Pawn pawn)
        {
            if (pawn == null) return 8;
            SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (physiqueSkillDef == null) return 8;
            SkillRecord skill = pawn.skills?.GetSkill(physiqueSkillDef);
            return skill?.levelInt ?? 8;
        }

        /// <summary>
        /// 通知健康系统Hediff发生变化
        /// 触发相关属性重新计算
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        private static void NotifyHediffChanged(Pawn pawn)
        {
            pawn.health.Notify_HediffChanged(null);
        }

        /// <summary>
        /// 外部接口：调整指定pawn的皮质醇浓度
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <param name="amount">调整量</param>
        public static void AdjustCortisol(Pawn pawn, float amount)
        {
            HediffComp_Cortisol cortisolComp = HediffComp_Cortisol.GetCortisolComp(pawn);
            if (cortisolComp != null)
            {
                cortisolComp.AdjustSeverity(amount);
            }
        }

        /// <summary>
        /// 外部接口：获取指定pawn的皮质醇浓度
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>皮质醇浓度（0~1）</returns>
        public static float GetCortisolSeverity(Pawn pawn)
        {
            HediffComp_Cortisol cortisolComp = HediffComp_Cortisol.GetCortisolComp(pawn);
            return cortisolComp?.GetCurrentSeverity() ?? 0f;
        }

        /// <summary>
        /// 外部接口：获取指定pawn的皮质醇档位
        /// </summary>
        /// <param name="pawn">目标pawn</param>
        /// <returns>CortisolLevel 枚举值</returns>
        public static CortisolLevel GetCortisolLevel(Pawn pawn)
        {
            float severity = GetCortisolSeverity(pawn);
            return GetCortisolLevel(severity);
        }
    }
}