using Verse;
using UnityEngine;

namespace Hormones
{
    public class RimHormonesMod : Mod
    {
        public static Settings Settings;

        public RimHormonesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("显示肾上腺素飘字", ref Settings.ShowAdrenalineMotes,
                "肾上腺素浓度变化时，在角色上方显示飘字通知（格式：肾上腺[原因]：±数值 [当前/100]）");
            listing.CheckboxLabeled("显示体魄飘字", ref Settings.ShowPhysiqueMotes,
                "体魄经验获取、肌肉劳损等事件时，在角色上方显示飘字通知");
            listing.CheckboxLabeled("显示皮质醇飘字", ref Settings.ShowCortisolMotes,
                "皮质醇浓度变化、神经衰弱检测/触发、失眠发作、优质睡眠等事件时，在角色上方显示飘字通知");
            listing.CheckboxLabeled("显示身体损伤飘字", ref Settings.ShowBodyDamageMotes,
                "肌肉拉伤、以及肾上腺素透支导致的身体损伤触发时，在角色上方显示飘字通知（部位 + 损伤名，按轻/中/重分色）");
            if (Settings.ShowBodyDamageMotes)
            {
                listing.CheckboxLabeled("    └ 同时显示敌方/非玩家的身体损伤飘字", ref Settings.ShowEnemyBodyDamageMotes,
                    "关闭时（默认）只显示玩家殖民者的身体损伤飘字；开启后，敌人、野生动物等非玩家阵营的身体损伤也会飘字");
            }

            listing.GapLine();
            listing.Label("肌肉拉伤设置");

            listing.Label($"拉伤触发体力门槛：{Settings.StrainTriggerThresholdPct * 100f:F0}%  （体力储备低于此百分比时才可能拉伤，调高更硬核，调 0 几乎不拉伤）");
            Settings.StrainTriggerThresholdPct = listing.Slider(Settings.StrainTriggerThresholdPct, 0f, 0.5f);

            listing.Label($"拉伤概率倍率：{Settings.StrainChanceMultiplier:F2}x  （统一缩放所有工作的拉伤概率，1.0 为默认）");
            Settings.StrainChanceMultiplier = listing.Slider(Settings.StrainChanceMultiplier, 0f, 3f);

            listing.GapLine();
            listing.Label("体魄日常衰减设置");
            listing.Label($"体魄衰减总倍率：{Settings.PhysiqueDecayGlobalMult:F2}x  （用进废退：当天无任何体力劳作/锻炼才按体魄阶段衰减；调 0 = 关闭衰减，调高更硬核）");
            Settings.PhysiqueDecayGlobalMult = listing.Slider(Settings.PhysiqueDecayGlobalMult, 0f, 3f);

            listing.End();
            Settings.Write();
        }

        public override string SettingsCategory()
        {
            return "Rim Hormones";
        }
    }

    public class Settings : ModSettings
    {
        public bool ShowAdrenalineMotes = false;
        public bool ShowPhysiqueMotes = false;
        public bool ShowCortisolMotes = false;
        public bool ShowBodyDamageMotes = true;  // 身体损伤飘字（肌肉拉伤 + 透支损伤），默认开启
        public bool ShowEnemyBodyDamageMotes = false;  // 是否显示非玩家阵营的身体损伤飘字，默认关闭（只显示自己殖民者）

        // D: 肌肉拉伤玩家可调项
        public float StrainTriggerThresholdPct = Define.DefaultStrainTriggerThresholdPct;
        public float StrainChanceMultiplier = Define.DefaultStrainChanceMultiplier;

        // 体魄日常衰减总倍率（0=关闭）
        public float PhysiqueDecayGlobalMult = Define.DefaultPhysiqueDecayGlobalMult;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowAdrenalineMotes, "showAdrenalineMotes", false);
            Scribe_Values.Look(ref ShowPhysiqueMotes, "showPhysiqueMotes", false);
            Scribe_Values.Look(ref ShowCortisolMotes, "showCortisolMotes", false);
            Scribe_Values.Look(ref ShowBodyDamageMotes, "showBodyDamageMotes", true);
            Scribe_Values.Look(ref ShowEnemyBodyDamageMotes, "showEnemyBodyDamageMotes", false);
            Scribe_Values.Look(ref StrainTriggerThresholdPct, "strainTriggerThresholdPct", Define.DefaultStrainTriggerThresholdPct);
            Scribe_Values.Look(ref StrainChanceMultiplier, "strainChanceMultiplier", Define.DefaultStrainChanceMultiplier);
            Scribe_Values.Look(ref PhysiqueDecayGlobalMult, "physiqueDecayGlobalMult", Define.DefaultPhysiqueDecayGlobalMult);
        }
    }
}
