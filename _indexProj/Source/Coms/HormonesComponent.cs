using Verse;
using RimWorld;
using Verse.AI;
using Hormones.Logic.PhysiqueLogic;

namespace Hormones
{

public static class Helpers
{
    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

public class CompProperties_Hormones : CompProperties
{
    public float decayRate = 0.5f;
    public float maxLevel = 100f;
    public float baseDamageHormonesReduction = 15f;

    public CompProperties_Hormones()
    {
        Log.Message("[Hormones] CompProperties_Hormones constructor called");
        compClass = typeof(HormonesComponent);
    }
}

public class HormonesComponent : ThingComp, IExposable
{
    private Pawn Pawn => parent as Pawn;

    private float curLevelInt;
    private float lastLevelInt;

    static HormonesComponent()
    {
        Log.Message("[Hormones] HormonesComponent class loaded");
    }

    public float MaxLevel => Define.HormonesMaxLevel;

    public CompProperties_Hormones Props => (CompProperties_Hormones)props;

    public float CurLevel
    {
        get => curLevelInt;
        set => curLevelInt = (value < 0f) ? 0f : ((value > MaxLevel) ? MaxLevel : value);
    }

    public float CurLevelPercentage => CurLevel / MaxLevel;

    public float LastLevelPercentage => lastLevelInt / MaxLevel;

    public HormonesStatus Status
    {
        get
        {
            if (CurLevelPercentage >= 0.8f) return HormonesStatus.Calm;
            if (CurLevelPercentage >= 0.5f) return HormonesStatus.Normal;
            if (CurLevelPercentage >= 0.2f) return HormonesStatus.Stressed;
            return HormonesStatus.Panicked;
        }
    }

    public bool IsStressed => Status <= HormonesStatus.Stressed;
    public bool IsPanicked => Status == HormonesStatus.Panicked;
    public bool IsCalm => Status == HormonesStatus.Calm;

    private int GetPhysiqueLevel()
    {
        return PhysiqueLgc.GetPhysiqueLevel(Pawn);
    }

    public float MetabolicRateMultiplier
    {
        get
        {
            return PhysiqueLgc.GetMetabolicRate(Pawn);
        }
    }

    public float AppetiteMultiplier
    {
        get
        {
            return PhysiqueLgc.GetAppetiteMultiplier(Pawn);
        }
    }

    public float WorkEfficiencyMultiplier
    {
        get
        {
            return PhysiqueLgc.GetWorkEfficiency(Pawn);
        }
    }

    public float HungerRateMultiplier
    {
        get
        {
            return PhysiqueLgc.GetHungerRate(Pawn);
        }
    }

    public float PhysiqueOverallBonus
    {
        get
        {
            return PhysiqueLgc.GetPhysiqueBonus(Pawn);
        }
    }

    private float GetPhysiqueRecoveryBonus()
    {
        return PhysiqueLgc.GetRecoveryBonus(Pawn);
    }

    private float GetPhysiqueDamageReductionFactor()
    {
        return PhysiqueLgc.GetDamageReductionFactor(Pawn);
    }

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        curLevelInt = MaxLevel;
        lastLevelInt = MaxLevel;
    }
    
    /// <summary>
    /// 添加体魄可视化 Hediff 到殖民者
    /// 延迟到第一次 Tick 时添加，确保 HediffDef 已加载
    /// </summary>
    private bool physiqueDisplayAdded = false;

    // A: 按工作时长累计结算 —— 累积“正在干白名单活”的 tick 数
    private float workTickAccumulator = 0f;

    // ============================================================
    // 【体魄日常衰减 2026-07-30】用进废退
    //   decayTickAccumulator：累计到 DecayTicksPerDay 触发一次日结算。
    //   activeToday：本结算周期内是否发生过体力劳作/锻炼（由 MarkActivityToday 置位）。
    // ============================================================
    private int decayTickAccumulator = 0;
    private bool activeToday = false;

    // 劳损封锁（每小人独立开关）：由「指派」面板的劳损封锁列控制，随存档持久化。
    private bool blockWorkWhenStrainLow = false;
    public bool BlockWorkWhenStrainLow
    {
        get => blockWorkWhenStrainLow;
        set => blockWorkWhenStrainLow = value;
    }

    /// <summary>
    /// 由劳作结算（PhysiqueWorkSettle.SettleWork）与锻炼（JobDriver_Exercise）调用，
    /// 标记该 pawn“今日已有体力活动”，本结算周期内免于体魄日常衰减。
    /// </summary>
    public void MarkActivityToday()
    {
        activeToday = true;
    }

    public override void CompTick()
    {
        base.CompTick();
        
        // 延迟添加体魄可视化 Hediff
        if (!physiqueDisplayAdded && Pawn != null && Pawn.IsHashIntervalTick(60))
        {
            AddPhysiqueDisplayHediff();
        }

        // A: 每 tick 判断是否在干白名单工作，累计并满周期结算
        PhysiqueWorkTick();

        // 体魄日常衰减：累计满一个游戏日结算一次（用进废退）
        PhysiqueDecayTick();

        // 劳损工作封锁：每 250 tick 检查一次储备是否触底
        if (Pawn != null && Pawn.IsHashIntervalTick(250))
        {
            StrainWorkBlockTick();
        }

        // 肾上腺素长期堆积损伤：每 600 tick（10 游戏秒）检测一次
        if (Pawn != null && Pawn.IsHashIntervalTick(Define.AdrenalineBuildupCheckIntervalTicks))
        {
            AdrenalineLogic.TryApplyAdrenalineBuildupDamage(Pawn);
        }

        // 原有的激素间隔逻辑
        if (Pawn != null && Pawn.IsHashIntervalTick(200))
        {
            HormonesInterval();
        }
    }

    // ============================================================
    // 【体魄日常衰减 2026-07-30】每 tick 累计，满一个游戏日结算一次。
    //   结算时：仅对类人生效；若本周期发生过体力活动(activeToday) → 跳过衰减；
    //   否则按体魄阶段扣 Physique 技能经验（× 玩家可调总倍率）。结算后重置标记。
    // ============================================================
    private void PhysiqueDecayTick()
    {
        if (Pawn == null || Pawn.Dead || Pawn.Suspended) return;

        decayTickAccumulator++;
        if (decayTickAccumulator < Define.DecayTicksPerDay) return;

        decayTickAccumulator = 0;
        DailyPhysiqueDecay();
        activeToday = false; // 进入下一个周期，重置活动标记
    }

    private void DailyPhysiqueDecay()
    {
        // 仅对类人生效（与整套激素/体魄系统一致）
        if (!PhysiqueLgc.IsHormoneSubject(Pawn)) return;

        // 本周期有过体力劳作/锻炼 → 用进，不衰减
        if (activeToday) return;

        // 【2026-08-04 饮品 Buff】功能饮品生效中：视为「当成一次锻炼」，
        // Buff 持续期间（12h）体魄不因缺乏活动而衰减。
        if (Pawn.health != null
            && Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("DrinkEnergyDrink"), false) != null)
            return;

        // 总倍率（玩家可调，0=关闭）
        float globalMult = RimHormonesMod.Settings != null
            ? RimHormonesMod.Settings.PhysiqueDecayGlobalMult
            : Define.DefaultPhysiqueDecayGlobalMult;
        if (globalMult <= 0f) return;

        float decayXP = PhysiqueLgc.GetDailyDecayXP(Pawn) * globalMult;
        if (decayXP <= 0f) return; // 虚弱阶段保底不衰减

        SkillDef physiqueDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
        SkillRecord rec = Pawn.skills?.GetSkill(physiqueDef);
        if (rec == null) return;

        // 用原版 Learn 传负经验来衰减：direct=true 表示不乘学习速率/研究等增益，
        // 净扣我们算好的 decayXP；内部会正确处理 xpSinceLastLevel 下溢与掉级。
        rec.Learn(-decayXP, direct: true);
    }

    // ============================================================
    // 【劳损工作封锁 2026-08-01】设置开启时：
    //   劳损储备(Need_MuscleStrain) ≤ 阈值 → 挂「体力不支」hediff
    //   （其 stage 的 disabledWorkTags 经原版机制禁用 采矿/搬运/建造/种植/狩猎）；
    //   恢复到 阈值+10% → 摘除（滞回防边界抖动）；设置关闭时确保摘除。
    // ============================================================
    private static HediffDef strainExhaustedDefCache;
    private const float StrainBlockReleaseBuffer = 0.10f;

    private void StrainWorkBlockTick()
    {
        Pawn pawn = Pawn;
        if (pawn == null || pawn.Dead || pawn.Suspended) return;
        if (!PhysiqueLgc.IsHormoneSubject(pawn)) return;

        if (strainExhaustedDefCache == null)
            strainExhaustedDefCache = DefDatabase<HediffDef>.GetNamed("PhysiqueStrainExhausted", false);
        if (strainExhaustedDefCache == null) return;

        Hediff exhausted = pawn.health?.hediffSet?.GetFirstHediffOfDef(strainExhaustedDefCache);

        // 每小人独立开关（指派面板列）：关闭时确保摘除后什么都不做
        if (!blockWorkWhenStrainLow)
        {
            if (exhausted != null) pawn.health.RemoveHediff(exhausted);
            return;
        }

        Need_MuscleStrain need = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
        if (need == null) return;
        float max = need.MaxLevel;
        if (max <= 0f) return;
        float pct = need.CurLevel / max;
        float threshold = RimHormonesMod.Settings != null ? RimHormonesMod.Settings.StrainBlockThresholdPct : 0.25f;

        if (exhausted == null && pct <= threshold)
        {
            pawn.health.AddHediff(strainExhaustedDefCache);
        }
        else if (exhausted != null && pct >= threshold + StrainBlockReleaseBuffer)
        {
            pawn.health.RemoveHediff(exhausted);
        }
    }

    /// <summary>
    /// 立即摘除「体力不支」（关闭指派面板开关时调用，不必等下一个检查周期）。
    /// </summary>
    public void RemoveStrainExhaustedNow()
    {
        if (strainExhaustedDefCache == null)
            strainExhaustedDefCache = DefDatabase<HediffDef>.GetNamed("PhysiqueStrainExhausted", false);
        if (strainExhaustedDefCache == null || Pawn == null) return;
        Hediff h = Pawn.health?.hediffSet?.GetFirstHediffOfDef(strainExhaustedDefCache);
        if (h != null) Pawn.health.RemoveHediff(h);
    }

    // ============================================================
    // A: 体魄工作时长累计
    //   判断 CurJob.def.defName 是否命中白名单；命中后再区分“赶路 vs 真正施工”：
    //     · 静态工作（挖矿/砍树/种植/建造…）：pawn 正在寻路移动 = 走去干活的路上 → 不累计；
    //       只有站定施工时才累计劳损/经验。
    //     · 移动型工作（搬运/打猎，见 IsMobileWork）：移动本身就是体力劳作 → 移动时照常累计。
    //   每累计满 WorkTicksPerSettle，就结算一份经验/劳损/拉伤，余数保留。
    // ============================================================
    private void PhysiqueWorkTick()
    {
        if (Pawn == null || Pawn.Dead || Pawn.Suspended) return;
        if (Pawn.needs == null) return;

        Job curJob = Pawn.CurJob;
        if (curJob?.def == null) return;

        if (!PhysiqueWorkSettle.TryGetWorkParams(curJob.def.defName, curJob, out float xp, out float strain, out float chance))
        {
            return;
        }

        // 赶路排除：静态工作在寻路移动阶段不算劳作（走去干活的路上不掉劳损）。
        // 搬运 / 打猎属于移动型劳作，移动时照常累计。
        if (!PhysiqueWorkSettle.IsMobileWork(curJob.def.defName)
            && Pawn.pather != null && Pawn.pather.Moving)
        {
            return;
        }

        workTickAccumulator += 1f;

        if (workTickAccumulator >= Define.WorkTicksPerSettle)
        {
            workTickAccumulator -= Define.WorkTicksPerSettle;
            PhysiqueWorkSettle.SettleWork(Pawn, xp, strain, chance, curJob, 1f);
        }
    }

    /// <summary>
    /// Job 结束时的处理。
    /// 【2026-07-26 修复】原先此处按"余数/周期"比例补算并清零，导致碎片化短 Job
    /// （种植 Sow、割除 CutPlant 等一格一个 Job）每次完成都被按极小 factor 补算后清零，
    /// 累计器永远攒不满 400，单次只扣 1~2 点几乎不可见。
    /// 现改为：Job 结束时【保留累计器余数，不清零】，让跨 Job 的连续劳作能正确累积，
    /// 唯一结算路径为 PhysiqueWorkTick 的"满 400 大结算"。这样种够约 5 格就满一次周期，
    /// 与挖矿/搬运等长 Job 的手感完全一致。
    /// 余数随存档持久化（见 PostExposeData），跨会话不丢。
    /// </summary>
    public void SettleWorkRemainder()
    {
        // 有意保留累计器，不做任何补算/清零。
        // 保留此方法仅为兼容 Patch_Job_End_PhysiqueXP 的调用点，未来若需"切到无关工作时衰减余数"可在此扩展。
    }
    
    /// <summary>
    /// 添加体魄可视化 Hediff 到殖民者
    /// </summary>
    private void AddPhysiqueDisplayHediff()
    {
        if (physiqueDisplayAdded) return;
        if (Pawn == null) return;

        // 检查是否已经有体魄可视化 Hediff
        if (Pawn.health?.hediffSet == null) return;

        HediffDef physiqueDef = DefDatabase<HediffDef>.GetNamed("PhysiqueBodyCondition", false);
        if (physiqueDef == null)
        {
            Log.Warning("[Hormones] PhysiqueBodyCondition HediffDef not found! Will retry next tick...");
            return;
        }

        // 检查是否已经有该 Hediff
        if (Pawn.health.hediffSet.HasHediff(physiqueDef))
        {
            physiqueDisplayAdded = true;
            return; // 已经有了，不再添加
        }

        // 添加体魄可视化 Hediff
        Hediff physiqueHediff = HediffMaker.MakeHediff(physiqueDef, Pawn);
        int physiqueLevel = GetPhysiqueLevel();
        // Severity 必须 > 0，否则 Hediff 会被自动移除
        physiqueHediff.Severity = System.Math.Max(0.01f, physiqueLevel / 20f);
        Pawn.health.AddHediff(physiqueHediff);

        physiqueDisplayAdded = true;
        Log.Message($"[体魄-初始化] {Pawn?.Name?.ToStringFull ?? "Unknown"} 添加了体魄可视化 Hediff，初始等级: {physiqueLevel}");
    }

    public void AddHormonesReduction(float baseAmount)
    {
        lastLevelInt = curLevelInt;
        float damageReductionFactor = GetPhysiqueDamageReductionFactor();
        float actualReduction = baseAmount * damageReductionFactor;
        CurLevel -= actualReduction;
        Log.Message($"[Hormones] {Pawn?.Name?.ToStringFull ?? "Unknown"} TookDamage: -{actualReduction:F1} (Physique={GetPhysiqueLevel()}, Factor={damageReductionFactor:F2}), Current: {CurLevel:F1} ({Status})");
    }

    public void HormonesInterval()
    {
        if (Pawn == null || Pawn.Suspended) return;

        lastLevelInt = curLevelInt;

        float moodFactor = Pawn.needs?.mood?.CurLevel ?? 0.5f;
        float recoveryBonus = GetPhysiqueRecoveryBonus();
        float recoveryRate = Define.HormonesDecayRate * recoveryBonus;

        bool hasSevereBleeding = HasSevereBleedingThought();

        if (hasSevereBleeding)
        {
            float damageReductionFactor = GetPhysiqueDamageReductionFactor();
            float baseBleedingReduction = Define.HormonesBaseDamageReduction * Define.HormonesBleedingReductionFactor;
            float bleedingReduction = baseBleedingReduction * damageReductionFactor;
            CurLevel -= bleedingReduction;
            Log.Message($"[Hormones] {Pawn?.Name?.ToStringFull ?? "Unknown"} SevereBleeding: -{bleedingReduction:F3} (Physique={GetPhysiqueLevel()}), Current: {CurLevel:F1}");
        }

        if (CurLevel < MaxLevel && !hasSevereBleeding)
        {
            float recoveryAmount = recoveryRate * moodFactor;
            CurLevel += recoveryAmount;
            Log.Message($"[Hormones] {Pawn?.Name?.ToStringFull ?? "Unknown"} Recovery: +{recoveryAmount:F3} (Physique={GetPhysiqueLevel()}, Bonus={recoveryBonus:F2}), Current: {CurLevel:F1} ({Status})");
        }
    }

    /// <summary>
    /// 废弃功能：SevereBleeding ThoughtDef 已从游戏中移除
    /// </summary>
    private bool HasSevereBleedingThought()
    {
        return false;
    }

    public override string CompInspectStringExtra()
    {
        return "Hormones".Translate() + ": " + CurLevelPercentage.ToStringPercent() + " (" + Status.ToString().Translate() + ")\n" +
               "Physique: " + GetPhysiqueLevel() + "\n" +
               "Metabolic Rate: " + MetabolicRateMultiplier.ToStringPercent() + "\n" +
               "Work Efficiency: " + WorkEfficiencyMultiplier.ToStringPercent() + "\n" +
               "Hunger Rate: " + HungerRateMultiplier.ToStringPercent();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref curLevelInt, "hormonesLevel", MaxLevel);
        Scribe_Values.Look(ref lastLevelInt, "hormonesLastLevel", MaxLevel);
        Scribe_Values.Look(ref workTickAccumulator, "physiqueWorkTickAccumulator", 0f);
        Scribe_Values.Look(ref decayTickAccumulator, "physiqueDecayTickAccumulator", 0);
        Scribe_Values.Look(ref activeToday, "physiqueActiveToday", false);
        Scribe_Values.Look(ref blockWorkWhenStrainLow, "blockWorkWhenStrainLow", false);
    }
}

public enum HormonesStatus
{
    Panicked,
    Stressed,
    Normal,
    Calm
}

}