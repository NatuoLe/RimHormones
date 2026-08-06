using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 背景故事 → 体魄偏移 配置（关键词子串匹配 defName）。
    ///
    /// 配置位于 ModRoot/Config/BackstoryPhysique.xml，在 Mod 构造函数里读一次并缓存为静态列表。
    /// 之后每次查询体魄等级时只做内存匹配，不触发任何文件 IO。
    ///
    /// 设计要点：
    /// - 偏移在【查询期】叠加到体魄等级上（与特质偏移同级），不修改技能本身，
    ///   因此不会被「用进废退」日常衰减系统吃掉，也不需要对每个背景故事写 XML 补丁。
    /// - 匹配童年+成年两个背景故事，命中关键词的偏移累加。
    /// </summary>
    public static class BackstoryPhysiqueConfig
    {
        private sealed class BackstoryModifier
        {
            public string keyword;
            public int offset;
        }

        private static readonly List<BackstoryModifier> modifiers = new List<BackstoryModifier>();
        private static bool loaded;
        private static string modRootDir;

        /// <summary>
        /// 在 Mod 构造函数里调用：记录 mod 根目录并加载配置。
        /// </summary>
        public static void Init(string rootDir)
        {
            modRootDir = rootDir;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            modifiers.Clear();

            if (string.IsNullOrEmpty(modRootDir)) return;

            string path = Path.Combine(modRootDir, "Config", "BackstoryPhysique.xml");
            if (!File.Exists(path))
            {
                Log.Warning($"[Hormones] 未找到背景故事体魄配置文件：{path}（将不应用任何背景故事修正）");
                return;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlElement root = doc.DocumentElement;
                if (root == null) return;

                foreach (XmlNode elem in root.SelectNodes("Modifier"))
                {
                    if (elem?.Attributes == null) continue;
                    string kw = elem.Attributes["keyword"]?.Value;
                    string offStr = elem.Attributes["offset"]?.Value;
                    if (string.IsNullOrEmpty(kw) || string.IsNullOrEmpty(offStr)) continue;
                    if (!int.TryParse(offStr, out int off)) continue;
                    modifiers.Add(new BackstoryModifier { keyword = kw, offset = off });
                }

                Log.Message($"[Hormones] 已加载 {modifiers.Count} 条背景故事体魄修正规则（来自 {path}）");
            }
            catch (Exception ex)
            {
                Log.Error($"[Hormones] 读取背景故事体魄配置失败：{ex}");
            }
        }

        /// <summary>
        /// 计算某 pawn 因背景故事（童年+成年）获得的体魄偏移总和。
        /// 对 Childhood/Adulthood 的 defName 做关键词子串匹配（不区分大小写），命中即累加。
        /// </summary>
        public static int GetBackstoryPhysiqueOffset(Pawn pawn)
        {
            EnsureLoaded();
            if (pawn?.story == null || modifiers.Count == 0) return 0;

            int total = 0;
            total += MatchBackstory(pawn.story.Childhood);
            total += MatchBackstory(pawn.story.Adulthood);
            return total;
        }

        private static int MatchBackstory(BackstoryDef bs)
        {
            if (bs == null) return 0;
            int sum = 0;
            string defName = bs.defName ?? string.Empty;
            for (int i = 0; i < modifiers.Count; i++)
            {
                BackstoryModifier m = modifiers[i];
                if (!string.IsNullOrEmpty(m.keyword)
                    && defName.IndexOf(m.keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sum += m.offset;
                }
            }
            return sum;
        }
    }
}
