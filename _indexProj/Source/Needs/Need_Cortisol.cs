using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Hormones;
using Hormones.Logic.PhysiqueLogic;

namespace Hormones
{
    /// <summary>
    /// 皮质醇需求类（新设计）
    /// Need 越高，造成的影响越大
    ///
    /// 衰减（每日，占最大值百分比）：
    /// | 档位     | 基础衰减 | 心情>0.8 | 美食  | 优质睡眠 |
    /// |---------|---------|---------|------|---------|
    /// | 正常波动 | -13%   | -8%    | -8%  | -8%     |
    /// | 承压     | -8%    | -8%    | -8%  | -8%     |
    /// | 高压     | -3%    | -8%    | -8%  | -8%     |
    ///
    /// 增长（每日，占最大值百分比）：
    /// | 条件        | 增长  |
    /// |------------|------|
    /// | 心情<0.3    | +10% |
    /// | 环境差      | +5%  |
    /// | 饥饿Hediff  | +12% |
    /// | 疼痛Hediff  | +5%  |
    /// | 得病        | +8%  |
    /// | 被侮辱Hediff| +3%  |
    ///
    /// 档位效果（随严重度 S = CurLevel/MaxLevel）：
    /// | 区间          | 状态   | 心情加成 | 冒犯权重 | 神经衰弱 |
    /// |--------------|-------|---------|---------|---------|
    /// | 0 ≤ S < 0.33  | 正常波动 | +2     | -50%    | 0%      |
    /// | 0.33 ≤ S<0.66 | 承压   | -1     | +200%   | 3%      |
    /// | 0.66 ≤ S≤1.0  | 高压   | -5     | +400%   | 8%      |
    /// 注：心情加成由 ThoughtWorker_CortisolMood + ThoughtDef CortisolMoodEffect 实现；
    ///     冒犯权重见 GetSocialFightChanceFactor，神经衰弱概率见 GetNeurastheniaProbability。
    ///     神经衰弱检测每 6000 tick 一次。
    ///
    /// 注：MaxLevel=10000，Define 中对应常量已 ×100 放大为绝对点数。
    /// </summary>
    public class Need_Cortisol : Need
    {
        // 神经衰弱检测节流计数器（每 6000 tick 检测一次）
        private int ticksSinceLastNeuroCheck = 0;

        // 流程日志节流计数器
        private int logFlowCounter = 0;

        // 当前档位
        private CortisolLevel lastLevel = CortisolLevel.Normal;

        // 上一次记录值（用于飘字显示）
        private float lastLoggedLevel = -1f;

        // 诊断日志标记
        private bool hasLoggedInit = false;
        private bool hasLoggedFirstTick = false;

        // 上一帧是否在睡眠（用于 asleep→awake 跳变检测苏醒）
        private bool wasAsleep = false;

        public override float MaxLevel => 10000f;

        public override bool ShowOnNeedList => true;

        public Need_Cortisol(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float> { 0.33f, 0.66f };
            Log.Warning($"[Cortisol-Init] 构造 Need_Cortisol: pawn={(pawn?.Name?.ToStringShort ?? "null")}");
        }

        public override void SetInitialLevel()
        {
            CurLevel = 0f;
            if (!hasLoggedInit)
            {
                hasLoggedInit = true;
                Log.Warning($"[Cortisol-Init] SetInitialLevel 调用: pawn={(pawn?.Name?.ToStringShort ?? "null")}, CurLevel={CurLevel}");
            }
        }

        // RimWorld 原生 NeedInterval 固定每 150 tick（≈2.5秒）调用一次，
        // 直接使用默认更新速度驱动皮质醇主循环（无需自定义补丁）。
        public override void NeedInterval()
        {
            CortisolTick();
        }

        /// <summary>
        /// 皮质醇主循环。由 RimWorld 原生 NeedInterval 每 150 tick 驱动一次。
        /// </summary>
        public void CortisolTick()
        {
            if (IsFrozen)
                return;

            if (!hasLoggedFirstTick)
            {
                hasLoggedFirstTick = true;
                Log.Warning($"[Cortisol-Tick] 首次 CortisolTick(NeedInterval 150tick 驱动): pawn={(pawn?.Name?.ToStringShort ?? "null")}");
            }

            try
            {
                // 检测苏醒（从睡眠状态醒来）
                CheckForWakeUp();

                // 计算当前严重度 (0-1)
                float severity = CurLevel / MaxLevel;

                // 计算本区间(NeedInterval=150 tick)的衰减与增长（单位：CurLevel 0~10000）
                float decay = GetDecayPerInterval(severity);
                float growth = GetGrowthPerInterval();
                // 体魄对皮质醇每日涨幅的修正（表C：体魄越强壮涨得越慢甚至下降）
                float physiqueGrowth = GetPhysiqueGrowthPerDay(severity) * 150f / 60000f;

                CurLevel -= decay;
                CurLevel += growth;
                CurLevel += physiqueGrowth;

                // 限制范围
                CurLevel = CurLevel < 0f ? 0f : (CurLevel > MaxLevel ? MaxLevel : CurLevel);

                // 流程日志：每 5 次调用（约 750 tick）打印一次，确认引擎工作
                LogCortisolFlow(severity, decay, growth, physiqueGrowth);

                // 神经衰弱检测（内部按 6000 tick 节流）
                TryTriggerNeurastheniaCheck();

                // 更新档位并通知变化
                UpdateLevel();

                // 飘字检查：每 150 tick（NeedInterval）一次，已去除阈值——只要有变化就显示
                CheckAndShowMote();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Cortisol-Tick] CortisolTick 异常: {ex}");
            }
        }

        /// <summary>
        /// 检测苏醒：从睡眠状态醒来时触发优质睡眠检测
        /// </summary>
        private void CheckForWakeUp()
        {
            // 用 asleep 状态跳变检测苏醒：上一帧在睡、这一帧醒了 = 刚苏醒
            bool isAsleep = pawn.jobs?.curDriver?.asleep == true;
            if (wasAsleep && !isAsleep)
            {
                TryApplyGoodSleep();
            }
            wasAsleep = isAsleep;
        }

        /// <summary>
        /// 检查并显示皮质醇变化飘字
        /// </summary>
        private void CheckAndShowMote()
        {
            if (pawn == null || pawn.Map == null)
                return;

            float currentLevel = CurLevel;

            // 首次记录基准值，不显示飘字
            if (lastLoggedLevel < 0f)
            {
                lastLoggedLevel = currentLevel;
                return;
            }

            float change = currentLevel - lastLoggedLevel;

            // 去除阈值：只要有任何变化就显示飘字（epsilon 仅用于过滤浮点噪声/无变化）
            if (System.Math.Abs(change) > 0.0001f)
            {
                ShowCortisolChangeMote(change);
                lastLoggedLevel = currentLevel;
            }
        }

        /// <summary>
        /// 显示皮质醇变化飘字
        /// 显示本次检测的绝对变化量（点数，MaxLevel 已放大到 10000，单步约 ±3 点，肉眼可见），保留 1 位小数，附涨跌箭头。
        /// </summary>
        private void ShowCortisolChangeMote(float change)
        {
            if (pawn == null || pawn.Map == null)
                return;

            float changeAbs = System.Math.Abs(change);
            string arrow = change > 0f ? "▲" : "▼";
            string text = $"皮质醇 {arrow}{changeAbs:F1}";

            // 使用与 MuscleStrainUtility 相同的飘字方式
            MoteText moteText = (MoteText)ThingMaker.MakeThing(ThingDefOf.Mote_Text);
            object vector3 = PhysiqueDatas.GetVector3(
                pawn.Position.x + 0.5f, 0.5f, pawn.Position.z + 0.5f);
            FieldInfo field = typeof(MoteText).GetField("exactPosition");
            if (field != null)
            {
                field.SetValue(moteText, vector3);
                moteText.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));
                moteText.text = text;
                GenSpawn.Spawn(moteText, pawn.Position, pawn.Map);
                PhysiqueDatas.ReturnVector3(vector3);
            }
        }

        /// <summary>
        /// 通用飘字：在 pawn 头顶显示一段文本（复用 MuscleStrainUtility 的 MoteText 方式）。
        /// 用于神经衰弱检测概率、神经衰弱触发、优质睡眠获取等事件反馈。
        /// </summary>
        private void ShowMote(string text)
        {
            if (pawn == null || pawn.Map == null)
                return;

            MoteText moteText = (MoteText)ThingMaker.MakeThing(ThingDefOf.Mote_Text);
            object vector3 = PhysiqueDatas.GetVector3(
                pawn.Position.x + 0.5f, 0.5f, pawn.Position.z + 0.5f);
            FieldInfo field = typeof(MoteText).GetField("exactPosition");
            if (field != null)
            {
                field.SetValue(moteText, vector3);
                moteText.SetVelocity(Rand.Range(5, 35), Rand.Range(0.42f, 0.45f));
                moteText.text = text;
                GenSpawn.Spawn(moteText, pawn.Position, pawn.Map);
                PhysiqueDatas.ReturnVector3(vector3);
            }
        }

        /// <summary>
        /// 获取当前档位
        /// </summary>
        public CortisolLevel GetCortisolLevel()
        {
            float severity = CurLevel / MaxLevel;
            if (severity < 0.33f)
                return CortisolLevel.Normal;
            if (severity < 0.66f)
                return CortisolLevel.Stressed;
            return CortisolLevel.HighStress;
        }

        /// <summary>
        /// 获取当前严重度 (0-1)
        /// </summary>
        public float GetSeverity()
        {
            return CurLevel / MaxLevel;
        }

        /// <summary>
        /// 获取衰减速率（每tick）
        /// 心情>0.8、美食、优质睡眠会额外增加衰减
        /// </summary>
        private float GetDecayPerInterval(float severity)
        {
            float decayPerDay;

            // 基础衰减根据档位
            if (severity < 0.33f)
            {
                decayPerDay = Define.CortisolDecayNormal;
            }
            else if (severity < 0.66f)
            {
                decayPerDay = Define.CortisolDecayStress;
            }
            else
            {
                decayPerDay = Define.CortisolDecayHighStress;
            }

            // 心情>0.8 时额外衰减
            if (pawn.needs?.mood != null && pawn.needs.mood.CurLevel > Define.CortisolMoodHighThreshold)
            {
                decayPerDay += Define.CortisolDecayHighMood;
            }

            // 美食Hediff 额外衰减
            if (HasDeliciousFoodHediff())
            {
                decayPerDay += Define.CortisolDecayDeliciousFood;
            }

            // 优质睡眠Hediff 额外衰减
            if (HasGoodSleepHediff())
            {
                decayPerDay += Define.CortisolDecayGoodSleep;
            }

            // 转换为每 150 tick 区间的量（60000 ticks = 1天；NeedInterval 每 150 tick 一次）
            return decayPerDay * 150f / 60000f;
        }

        /// <summary>
        /// 检测是否有美食（Thought检测：AteLavishMeal 或 AteFineMeal）
        /// </summary>
        private bool HasDeliciousFoodHediff()
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
                return false;

            return pawn.needs.mood.thoughts.memories.Memories
                .Any(t => t.def.defName == "AteLavishMeal" || t.def.defName == "AteFineMeal");
        }

        /// <summary>
        /// 检测是否有优质睡眠Hediff
        /// </summary>
        private bool HasGoodSleepHediff()
        {
            if (pawn.health?.hediffSet == null)
                return false;

            return pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("GoodSleep", false));
        }

        /// <summary>
        /// 尝试应用优质睡眠（苏醒时调用）
        /// 条件：Rest < 0.03 且 睡眠房间美观度 ≥ Beautiful 且 无 SleepDisturbed
        /// </summary>
        public void TryApplyGoodSleep()
        {
            // 已有优质睡眠不重复添加
            if (HasGoodSleepHediff())
                return;

            // 检查睡眠房间美观度
            if (!IsSleepRoomBeautiful())
                return;

            // 检查是否被打扰
            if (HasSleepDisturbed())
                return;

            // 添加优质睡眠 Hediff
            AddGoodSleepHediff();

            Log.Message($"[优质睡眠] {pawn.Name?.ToStringShort ?? "Unknown"} 在美观的房间中获得优质睡眠");
        }

        /// <summary>
        /// 检测睡眠房间是否美观（≥ Beautiful）
        /// </summary>
        private bool IsSleepRoomBeautiful()
        {
            // 获取pawn的床位
            Building_Bed bed = pawn?.ownership?.OwnedBed;
            if (bed == null)
                return false;

            // 获取房间
            Room room = bed.GetRoom(RegionType.Normal);
            if (room == null)
                return false;

            // 检查房间美观度
            float beauty = room.GetStat(RoomStatDefOf.Beauty);
            return beauty >= 0.5f; // Beautiful 档位约 0.5+
        }

        /// <summary>
        /// 检测是否被打扰（SleepDisturbed Thought）
        /// </summary>
        private bool HasSleepDisturbed()
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
                return false;

            return pawn.needs.mood.thoughts.memories.Memories
                .Any(t => t.def.defName == "SleepDisturbed");
        }

        /// <summary>
        /// 添加优质睡眠Hediff
        /// </summary>
        private void AddGoodSleepHediff()
        {
            HediffDef goodSleepDef = DefDatabase<HediffDef>.GetNamed("GoodSleep", false);
            if (goodSleepDef == null)
                return;

            Hediff goodSleep = HediffMaker.MakeHediff(goodSleepDef, pawn);
            goodSleep.Severity = 1.0f;
            pawn.health.AddHediff(goodSleep);

            ShowMote("优质睡眠 ✓");
        }

        /// <summary>
        /// 检测是否被侮辱（Thought检测：InsultedMood）
        /// </summary>
        private bool HasInsultedMood()
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
                return false;

            return pawn.needs.mood.thoughts.memories.Memories
                .Any(t => t.def.defName == "InsultedMood");
        }

        /// <summary>
        /// 获取增长速率（每tick）
        /// 包含各种应激源：新设计
        /// </summary>
        private float GetGrowthPerInterval()
        {
            float growthPerDay = 0f;

            // 心情<0.3
            if (pawn.needs?.mood != null && pawn.needs.mood.CurLevel < Define.CortisolMoodLowThreshold)
            {
                growthPerDay += Define.CortisolGrowthLowMood;
            }

            // 环境差 (beauty <= Ugly)
            if (pawn.needs?.beauty != null && (int)pawn.needs.beauty.CurCategory <= (int)BeautyCategory.Ugly)
            {
                growthPerDay += Define.CortisolGrowthUglyEnv;
            }

            // 饥饿Hediff
            if (HasHungerHediff())
            {
                growthPerDay += Define.CortisolGrowthHunger;
            }

            // 疼痛Hediff
            if (pawn.health?.hediffSet != null && pawn.health.hediffSet.PainTotal > 0f)
            {
                growthPerDay += Define.CortisolGrowthPain;
            }

            // 得病 (任何疾病)
            if (HasAnyIllness())
            {
                growthPerDay += Define.CortisolGrowthIllness;
            }

            // 被侮辱
            if (HasInsultedMood())
            {
                growthPerDay += Define.CortisolGrowthInsulted;
            }

            // 转换为每 150 tick 区间的量（60000 ticks = 1天；NeedInterval 每 150 tick 一次）
            return growthPerDay * 150f / 60000f;
        }

        /// <summary>
        /// 体魄对皮质醇每日涨幅的修正（点数/日，MaxLevel=10000，100点=1%）。
        /// 按当前 severity 档位 + 体魄阶段查表（设计表C：体魄越强壮，皮质醇涨得越慢甚至下降）。
        /// 表C（%/日 → 点数/日 ×100）：
        ///   档位\体魄   frail  average  fit    strong  peak
        ///   0≤S&lt;0.33   +200   0       -100   -600    -1000
        ///   0.33≤S&lt;0.66 +300   +100    0      -400    -700
        ///   0.66≤S≤1.0   +500   +300    +200   0       -500
        /// </summary>
        private float GetPhysiqueGrowthPerDay(float severity)
        {
            int tier = severity < 0.33f ? 0 : (severity < 0.66f ? 1 : 2);
            PhysiqueStage phys = PhysiqueLgc.GetPhysiqueStage(pawn);

            if (tier == 0)
            {
                switch (phys)
                {
                    case PhysiqueStage.Frail: return 200f;
                    case PhysiqueStage.Average: return 0f;
                    case PhysiqueStage.Fit: return -100f;
                    case PhysiqueStage.Strong: return -600f;
                    case PhysiqueStage.Peak: return -1000f;
                }
            }
            else if (tier == 1)
            {
                switch (phys)
                {
                    case PhysiqueStage.Frail: return 300f;
                    case PhysiqueStage.Average: return 100f;
                    case PhysiqueStage.Fit: return 0f;
                    case PhysiqueStage.Strong: return -400f;
                    case PhysiqueStage.Peak: return -700f;
                }
            }
            else
            {
                switch (phys)
                {
                    case PhysiqueStage.Frail: return 500f;
                    case PhysiqueStage.Average: return 300f;
                    case PhysiqueStage.Fit: return 200f;
                    case PhysiqueStage.Strong: return 0f;
                    case PhysiqueStage.Peak: return -500f;
                }
            }
            return 0f;
        }

        /// <summary>
        /// 检测是否有饥饿Hediff
        /// </summary>
        private bool HasHungerHediff()
        {
            // 检查是否有饥饿相关的Hediff
            // 原版中饥饿是通过 needs.food.CurLevel < 0.2 来判断的
            // 这里可以保留原逻辑作为后备
            if (pawn.needs?.food != null && pawn.needs.food.CurLevel < 0.2f)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检测是否有任何疾病（得病）
        /// </summary>
        private bool HasAnyIllness()
        {
            if (pawn.health?.hediffSet == null)
                return false;

            // 检查是否有疾病相关的Hediff
            // 可以检查hediffSet中的所有Hediff，判断是否为疾病
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                // 跳过非疾病类Hediff
                if (hediff.def == null)
                    continue;

                // 跳过常见的非疾病Hediff
                string defName = hediff.def.defName.ToLower();
                if (defName.Contains("cortisol") ||
                    defName.Contains("adrenaline") ||
                    defName.Contains("muscle") ||
                    defName.Contains("physique") ||
                    hediff is Hediff_Injury ||
                    hediff is Hediff_MissingPart)
                    continue;

                // 检查是否为疾病（通过类型名称判断）
                string hediffClassName = hediff.GetType().Name;
                if (hediffClassName.Contains("Disease") || hediffClassName.Contains("Sickness") ||
                    hediff.def.defName.ToLower().Contains("flu") ||
                    hediff.def.defName.ToLower().Contains("cold") ||
                    hediff.def.defName.ToLower().Contains("infection"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取肾上腺素浓度
        /// </summary>
        private float GetAdrenalineSeverity()
        {
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
            return adrenaline?.Severity ?? 0f;
        }

        /// <summary>
        /// 更新档位并通知变化
        /// </summary>
        private void UpdateLevel()
        {
            CortisolLevel currentLevel = GetCortisolLevel();
            if (currentLevel != lastLevel)
            {
                lastLevel = currentLevel;
                // 档位变化时通知
            }
        }

        /// <summary>
        /// 流程日志：每 5 次 NeedInterval（约 750 tick）打印一次，
        /// 直接展示引擎是否在工作、哪些应激源在生效、净变化量。
        /// </summary>
        private void LogCortisolFlow(float severityBefore, float decay, float growth, float physiqueGrowth)
        {
            logFlowCounter++;
            if (logFlowCounter % 5 != 0)
                return;

            string stressors = "";
            if (pawn.needs?.mood != null && pawn.needs.mood.CurLevel < Define.CortisolMoodLowThreshold) stressors += "心情差 ";
            if (pawn.needs?.beauty != null && (int)pawn.needs.beauty.CurCategory <= (int)BeautyCategory.Ugly) stressors += "环境差 ";
            if (HasHungerHediff()) stressors += "饥饿 ";
            if (pawn.health?.hediffSet != null && pawn.health.hediffSet.PainTotal > 0f) stressors += "疼痛 ";
            if (HasAnyIllness()) stressors += "疾病 ";
            if (HasInsultedMood()) stressors += "被辱 ";
            if (stressors == "") stressors = "无";


        }

        /// <summary>
        /// 尝试触发神经衰弱检测
        /// </summary>
        private void TryTriggerNeurastheniaCheck()
        {
            // 按 6000 tick 节流：每次 NeedInterval ≈ 150 tick
            ticksSinceLastNeuroCheck += 150;
            if (ticksSinceLastNeuroCheck < 6000)
                return;
            ticksSinceLastNeuroCheck = 0;

            float severity = CurLevel / MaxLevel;
            float probability = GetNeurastheniaProbability(severity);

            // 飘字：每次 roll 都显示当前概率（每 6000 tick 一次），便于确认检测在跑
            ShowMote($"神经衰弱检测 {probability:P0}");

            // 注：检测日志已按用户要求暂时关闭，飘字仍保留。

            if (Rand.Value < probability)
            {
                ApplyNeurasthenia();
            }
        }

        /// <summary>
        /// 获取神经衰弱触发概率
        /// 0-0.33: 0%
        /// 0.33-0.66: 3%
        /// 0.66-1.0: 8%
        /// </summary>
        private float GetNeurastheniaProbability(float severity)
        {
            if (severity < 0.33f)
                return 0f;
            if (severity < 0.66f)
                return 0.03f;
            return 0.08f;
        }

        /// <summary>
        /// 应用神经衰弱
        /// </summary>
        private void ApplyNeurasthenia()
        {
            HediffDef neurastheniaDef = DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false);
            if (neurastheniaDef == null)
                return;

            Hediff neurasthenia = HediffMaker.MakeHediff(neurastheniaDef, pawn);
            neurasthenia.Severity = 1.0f;
            pawn.health.AddHediff(neurasthenia);

            ShowMote("神经衰弱 触发!");

            Log.Warning($"[皮质醇-神经衰弱触发] 🔴 {pawn.Name?.ToStringFull ?? "Unknown"} 患上了神经衰弱！");
        }

        /// <summary>
        /// 获取冒犯权重修正
        /// 0-0.33: -50%
        /// 0.33-0.66: +200%
        /// 0.66-1.0: +400%
        /// </summary>
        public float GetSocialFightChanceFactor()
        {
            float severity = CurLevel / MaxLevel;
            if (severity < 0.33f)
                return 0.5f;  // -50%
            if (severity < 0.66f)
                return 3.0f;  // +200%
            return 5.0f;      // +400%
        }

        public override int GUIChangeArrow
        {
            get
            {
                float severity = CurLevel / MaxLevel;
                if (severity < 0.33f)
                    return 0;  // 正常波动
                return -1;  // 压力增加
            }
        }

    }

    /// <summary>
    /// 皮质醇档位枚举
    /// </summary>
    public enum CortisolLevel
    {
        Normal,      // 正常波动 (0 ≤ S < 0.33)
        Stressed,    // 承压 (0.33 ≤ S < 0.66)
        HighStress   // 高压 (0.66 ≤ S ≤ 1.0)
    }
}
