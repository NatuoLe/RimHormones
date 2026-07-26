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
        public bool ShowAdrenalineMotes = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowAdrenalineMotes, "showAdrenalineMotes", true);
        }
    }
}
