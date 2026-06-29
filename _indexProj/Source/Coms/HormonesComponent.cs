using Verse;
using RimWorld;

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
        if (Pawn == null) return 1;
        SkillDef physiqueSkillDef = DefDatabase<SkillDef>.GetNamed("Physique", false);
        if (physiqueSkillDef == null) return 1;
        SkillRecord skill = Pawn.skills?.GetSkill(physiqueSkillDef);
        int level = skill?.levelInt ?? 1;
        
        // 应用 Trait 的初始等级偏移
        int traitOffset = PhysiqueTraitUtility.GetTotalPhysiqueOffset(Pawn);
        level += traitOffset;
        
        // 计算修正后的最大等级
        int maxLevel = Define.PhysiqueMaxLevel + PhysiqueTraitUtility.GetTotalCapOffset(Pawn);
        
        return Helpers.Clamp(level, Define.PhysiqueMinLevel, maxLevel);
    }

    public float MetabolicRateMultiplier
    {
        get
        {
            int physiqueLevel = GetPhysiqueLevel();
            float metabolicRate = Define.MetabolicRateBase + Define.MetabolicRatePerPhysique * physiqueLevel;
            return metabolicRate;
        }
    }

    public float AppetiteMultiplier
    {
        get
        {
            float metabolicRate = MetabolicRateMultiplier;
            return Helpers.Clamp(metabolicRate, Define.AppetiteMinMultiplier, Define.AppetiteMaxMultiplier);
        }
    }

    public float WorkEfficiencyMultiplier
    {
        get
        {
            int physiqueLevel = GetPhysiqueLevel();
            float workEfficiency = Define.WorkEfficiencyBase + Define.WorkEfficiencyPerPhysique * physiqueLevel;
            return Helpers.Clamp(workEfficiency, Define.WorkEfficiencyMin, Define.WorkEfficiencyMax);
        }
    }

    public float HungerRateMultiplier
    {
        get
        {
            int physiqueLevel = GetPhysiqueLevel();
            float hungerRate = Define.HungerRateBase + Define.HungerRatePerPhysique * physiqueLevel;
            return Helpers.Clamp(hungerRate, Define.HungerRateMin, Define.HungerRateMax);
        }
    }

    public float PhysiqueOverallBonus
    {
        get
        {
            int physiqueLevel = GetPhysiqueLevel();

            if (physiqueLevel < Define.PhysiqueNegativeThresholdHigh)
            {
                return Define.PhysiqueLowPenalty;
            }
            else if (physiqueLevel <= Define.PhysiqueNegativeThresholdLow)
            {
                return Define.PhysiqueMediumPenalty;
            }
            else
            {
                float bonus = 1f + (physiqueLevel - Define.PhysiquePositiveThreshold + 1) * Define.PhysiqueBonusPerLevel;
                return bonus;
            }
        }
    }

    private float GetPhysiqueRecoveryBonus()
    {
        int physiqueLevel = GetPhysiqueLevel();
        return 1f + (physiqueLevel - 1f) / (Define.PhysiqueMaxLevel - 1) * Define.PhysiqueHormonesRecoveryBonusFactor;
    }

    private float GetPhysiqueDamageReductionFactor()
    {
        int physiqueLevel = GetPhysiqueLevel();
        return 1f - (physiqueLevel - 1f) / (Define.PhysiqueMaxLevel - 1) * Define.PhysiqueHormonesDamageReductionFactor;
    }

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        curLevelInt = MaxLevel;
        lastLevelInt = MaxLevel;
    }
    
    /// <summary>
    /// 添加皮质醇 Hediff 到殖民者
    /// 延迟到第一次 Tick 时添加，确保 HediffDef 已加载
    /// </summary>
    private bool cortisolAdded = false;
    private bool physiqueDisplayAdded = false;
    
    public override void CompTick()
    {
        base.CompTick();
        
        // 延迟添加皮质醇 Hediff（确保 HediffDef 已加载）
        if (!cortisolAdded && Pawn != null && Pawn.IsHashIntervalTick(60))
        {
            AddCortisolHediff();
        }
        
        // 延迟添加体魄可视化 Hediff
        if (!physiqueDisplayAdded && Pawn != null && Pawn.IsHashIntervalTick(60))
        {
            AddPhysiqueDisplayHediff();
        }
        
        // 原有的激素间隔逻辑
        if (Pawn != null && Pawn.IsHashIntervalTick(200))
        {
            HormonesInterval();
        }
    }
    
    /// <summary>
    /// 添加皮质醇 Hediff 到殖民者
    /// </summary>
    private void AddCortisolHediff()
    {
        if (cortisolAdded) return;
        if (Pawn == null) return;
        
        // 检查是否已经有皮质醇 Hediff
        if (Pawn.health?.hediffSet == null) return;
        
        HediffDef cortisolDef = DefDatabase<HediffDef>.GetNamed("Cortisol", false);
        if (cortisolDef == null)
        {
            Log.Warning("[Hormones] Cortisol HediffDef not found! Will retry next tick...");
            return;
        }
        
        // 检查是否已经有皮质醇 Hediff
        if (Pawn.health.hediffSet.HasHediff(cortisolDef))
        {
            cortisolAdded = true;
            return; // 已经有了，不再添加
        }
        
        // 添加皮质醇 Hediff，初始严重度为正常区间下限 0.15
        Hediff cortisolHediff = HediffMaker.MakeHediff(cortisolDef, Pawn);
        cortisolHediff.Severity = 0.15f; // 从正常区间开始
        Pawn.health.AddHediff(cortisolHediff);
        
        cortisolAdded = true;
        Log.Message($"[皮质醇-初始化] {Pawn?.Name?.ToStringFull ?? "Unknown"} 添加了皮质醇 Hediff，初始浓度: 0.15");
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

    private bool HasSevereBleedingThought()
    {
        if (Pawn?.needs?.mood?.thoughts?.memories == null) return false;

        var severeBleedingDef = DefDatabase<ThoughtDef>.GetNamed("SevereBleeding", false);
        if (severeBleedingDef == null)
        {
            Log.Message("[Hormones] SevereBleeding ThoughtDef not found in database");
            return false;
        }

        var memories = Pawn.needs.mood.thoughts.memories.Memories;
        foreach (var memory in memories)
        {
            if (memory.def == severeBleedingDef)
            {
                return true;
            }
        }
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