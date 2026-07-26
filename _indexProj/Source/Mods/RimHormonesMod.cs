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

            listing.GapLine();
            listing.Label("肌肉拉伤设置");

            listing.Label($"拉伤触发体力门槛：{Settings.StrainTriggerThresholdPct * 100f:F0}%  （体力储备低于此百分比时才可能拉伤，调高更硬核，调 0 几乎不拉伤）");
            Settings.StrainTriggerThresholdPct = listing.Slider(Settings.StrainTriggerThresholdPct, 0f, 0.5f);

            listing.Label($"拉伤概率倍率：{Settings.StrainChanceMultiplier:F2}x  （统一缩放所有工作的拉伤概率，1.0 为默认）");
            Settings.StrainChanceMultiplier = listing.Slider(Settings.StrainChanceMultiplier, 0f, 3f);

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

        // D: 肌肉拉伤玩家可调项
        public float StrainTriggerThresholdPct = Define.DefaultStrainTriggerThresholdPct;
        public float StrainChanceMultiplier = Define.DefaultStrainChanceMultiplier;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowAdrenalineMotes, "showAdrenalineMotes", false);
            Scribe_Values.Look(ref ShowPhysiqueMotes, "showPhysiqueMotes", false);
            Scribe_Values.Look(ref ShowCortisolMotes, "showCortisolMotes", false);
            Scribe_Values.Look(ref StrainTriggerThresholdPct, "strainTriggerThresholdPct", Define.DefaultStrainTriggerThresholdPct);
            Scribe_Values.Look(ref StrainChanceMultiplier, "strainChanceMultiplier", Define.DefaultStrainChanceMultiplier);
        }
    }
}
