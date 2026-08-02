using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// MuscleStrainRest（精疲力尽）的驱动 worker（2026-08-01 改版）。
    /// 情景型心情：劳损储备(Need_MuscleStrain) &lt; 10% 即激活，
    /// 不叠加、无持续时间，只看储备高低；恢复后自动消失。
    /// </summary>
    public class ThoughtWorker_MuscleStrainRest : ThoughtWorker
    {
        /// <summary>触发阈值：储备比例低于此值即激活。</summary>
        public const float RestThresholdPct = 0.10f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || !PhysiqueLgc.IsHormoneSubject(p)) return false;
            Need_MuscleStrain need = p.needs?.TryGetNeed<Need_MuscleStrain>();
            if (need == null) return false;
            float max = need.MaxLevel;
            if (max <= 0f) return false;
            return (need.CurLevel / max) < RestThresholdPct;
        }
    }

    /// <summary>
    /// StrainInjuryMood（浑身酸痛）的驱动 worker（2026-08-01 新增）。
    /// 身上带有任意劳损/透支损伤 hediff（Hediff_StrainPool.xml 损伤池）即激活；
    /// 损伤全部恢复后自动消失。情景型，不叠加、无持续时间。
    /// </summary>
    public class ThoughtWorker_StrainInjuryMood : ThoughtWorker
    {
        // 损伤池 defName（与 Hediff_StrainPool.xml 对齐；
        // 不含 PhysiqueStrainExhausted——那是软禁止状态标记，不是损伤）。
        private static readonly string[] StrainDamageDefNames =
        {
            "LaborMuscleStrain",
            "DiggingMuscleStrain",
            "CardioOverexert",
            "SuffocationStrain",
            "CombatJointStrain",
            "FallJointStrain",
            "CombatEnduranceExhaust",
            "MetabolicExhaust",
            "VisualStrain",
            "AuditoryStrain",
        };

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || !PhysiqueLgc.IsHormoneSubject(p)) return false;
            HediffSet set = p.health?.hediffSet;
            if (set == null) return false;
            for (int i = 0; i < set.hediffs.Count; i++)
            {
                string dn = set.hediffs[i].def?.defName;
                if (dn == null) continue;
                for (int j = 0; j < StrainDamageDefNames.Length; j++)
                {
                    if (dn == StrainDamageDefNames[j]) return true;
                }
            }
            return false;
        }
    }
}
