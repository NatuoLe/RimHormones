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
            // 启动时加载「背景故事 → 体魄偏移」配置（Config/BackstoryPhysique.xml）
            BackstoryPhysiqueConfig.Init(content.RootDir);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("启用 Metabolic Essential（代谢扩展模块）", ref Settings.EnableMetabolicEssential,
                "实验性：启用 MetabolicEssential.dll 提供的额外代谢机制。勾选/取消后需重启游戏客户端才能生效。");
            if (!MetabolicLoader.IsModulePresent)
            {
                listing.Label("⚠ 未检测到 MetabolicEssential.dll（应位于本 mod 的 Assemblies 目录）。");
            }
            else if (Settings.EnableMetabolicEssential != MetabolicLoader.IsLoaded)
            {
                listing.Label("⚠ 该选项的更改需重启游戏客户端后才能生效。");
            }
            listing.CheckboxLabeled("显示肾上腺素飘字", ref Settings.ShowAdrenalineMotes,
                "肾上腺素浓度变化时，在角色上方显示飘字通知（格式：肾上腺[原因]：±数值 [当前/100]）");
            listing.CheckboxLabeled("显示体魄飘字", ref Settings.ShowPhysiqueMotes,
                "体魄经验获取、肌肉劳损等事件时，在角色上方显示飘字通知");
            listing.CheckboxLabeled("显示皮质醇飘字", ref Settings.ShowCortisolMotes,
                "皮质醇浓度变化、神经衰弱检测/触发、症状触发、优质睡眠等事件时，在角色上方显示飘字通知");
            if (Settings.ShowCortisolMotes)
            {
                listing.CheckboxLabeled("    └ 皮质醇社交影响飘字", ref Settings.ShowCortisolSocialMotes,
                    "社交互动时（冒犯/侮辱倾向、负面社交冲突概率）在角色头顶显示皮质醇带来的权重倍率飘字（含档位与新增倍率）");
                listing.CheckboxLabeled("    └ 症状触发飘字", ref Settings.ShowCortisolInsomniaMotes,
                    "高皮质醇症状组（神经衰弱/快感缺失）加权触发时，在角色头顶显示抽中的症状名飘字");
            }
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

            listing.Label($"劳损封锁触发阈值：{Settings.StrainBlockThresholdPct * 100f:F0}%  （在「指派」面板逐个小人勾选「劳损封锁」后生效：储备低于此比例即不再主动接重体力工作（不锁能力、可手动指派），恢复到 {Settings.StrainBlockThresholdPct * 100f + 10f:F0}% 时解除）");
            Settings.StrainBlockThresholdPct = listing.Slider(Settings.StrainBlockThresholdPct, 0.05f, 0.6f);

            listing.GapLine();
            listing.Label("体魄日常衰减设置");
            listing.Label($"体魄衰减总倍率：{Settings.PhysiqueDecayGlobalMult:F2}x  （用进废退：当天无任何体力劳作/锻炼才按体魄阶段衰减；调 0 = 关闭衰减，调高更硬核）");
            Settings.PhysiqueDecayGlobalMult = listing.Slider(Settings.PhysiqueDecayGlobalMult, 0f, 3f);

            listing.GapLine();
            listing.Label("肾上腺素长期堆积损伤设置");
            listing.Label($"长期堆积损伤总倍率：{Settings.AdrenalineBuildupGlobalMult:F2}x  （持续高肾上腺素时每 10 秒检测一次；体魄 0 约 1%/次、体魄 12 约 0.3%/次，体魄 13+ 豁免；调 0 = 关闭）");
            Settings.AdrenalineBuildupGlobalMult = listing.Slider(Settings.AdrenalineBuildupGlobalMult, 0f, 3f);

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
        public bool ShowCortisolSocialMotes = false;  // 皮质醇社交影响飘字（"显示皮质醇飘字"的子级）
        public bool ShowCortisolInsomniaMotes = false;  // 失眠检测/发作飘字（"显示皮质醇飘字"的子级）
        public bool ShowBodyDamageMotes = true;  // 身体损伤飘字（肌肉拉伤 + 透支损伤），默认开启
        public bool ShowEnemyBodyDamageMotes = false;  // 是否显示非玩家阵营的身体损伤飘字，默认关闭（只显示自己殖民者）

        // D: 肌肉拉伤玩家可调项
        public float StrainTriggerThresholdPct = Define.DefaultStrainTriggerThresholdPct;
        public float StrainChanceMultiplier = Define.DefaultStrainChanceMultiplier;

        // 体魄日常衰减总倍率（0=关闭）
        public float PhysiqueDecayGlobalMult = Define.DefaultPhysiqueDecayGlobalMult;

        // 劳损封锁阈值（储备比例，解除=阈值+10% 滞回）。
        // 注意：开/关是每个小人独立的，存在 HormonesComponent（指派面板「劳损封锁」列控制），此处只有全局阈值。
        public float StrainBlockThresholdPct = 0.25f;

        // 肾上腺素长期堆积损伤总倍率（0=关闭）
        public float AdrenalineBuildupGlobalMult = Define.DefaultAdrenalineBuildupGlobalMult;

        // 代谢扩展模块（MetabolicEssential.dll）开关；仅在游戏启动期生效，运行期更改需重启
        public bool EnableMetabolicEssential = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowAdrenalineMotes, "showAdrenalineMotes", false);
            Scribe_Values.Look(ref ShowPhysiqueMotes, "showPhysiqueMotes", false);
            Scribe_Values.Look(ref ShowCortisolMotes, "showCortisolMotes", false);
            Scribe_Values.Look(ref ShowCortisolSocialMotes, "showCortisolSocialMotes", false);
            Scribe_Values.Look(ref ShowCortisolInsomniaMotes, "showCortisolInsomniaMotes", false);
            Scribe_Values.Look(ref ShowBodyDamageMotes, "showBodyDamageMotes", true);
            Scribe_Values.Look(ref ShowEnemyBodyDamageMotes, "showEnemyBodyDamageMotes", false);
            Scribe_Values.Look(ref StrainTriggerThresholdPct, "strainTriggerThresholdPct", Define.DefaultStrainTriggerThresholdPct);
            Scribe_Values.Look(ref StrainChanceMultiplier, "strainChanceMultiplier", Define.DefaultStrainChanceMultiplier);
            Scribe_Values.Look(ref PhysiqueDecayGlobalMult, "physiqueDecayGlobalMult", Define.DefaultPhysiqueDecayGlobalMult);
            Scribe_Values.Look(ref StrainBlockThresholdPct, "strainBlockThresholdPct", 0.25f);
            Scribe_Values.Look(ref AdrenalineBuildupGlobalMult, "adrenalineBuildupGlobalMult", Define.DefaultAdrenalineBuildupGlobalMult);
            Scribe_Values.Look(ref EnableMetabolicEssential, "enableMetabolicEssential", false);
        }
    }
}
