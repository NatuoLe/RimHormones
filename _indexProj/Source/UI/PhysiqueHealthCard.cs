using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;   // PlayOneShotOnCamera 是 SoundStarter 的扩展方法，缺此 using 报 CS1061

namespace Hormones.UI
{
    /// <summary>体魄 UI 的内容绘制。布局刻意模仿原版健康页，降低违和感。</summary>
    public static class PhysiqueHealthCard
    {
        private static Vector2 scrollPos = Vector2.zero;
        private static float viewHeight = 0f;

        // ============================================================
        // 主从联动：左侧行可点击，点了就切换右侧显示的分类。
        // 用 enum 而不是 int/string —— 编译期就能查出漏写的分支。
        // 静态字段 = 切小人不重置，与原版 onOperationTab 行为一致。
        // ============================================================
        public enum DetailTab
        {
            Physique,   // 体魄总览
            Strain,     // 肌肉劳损
            Cortisol,   // 皮质醇
            MEEWater,           // 代谢：水分
            MEESugar,           // 代谢：糖
            MEEElectrolytes,    // 代谢：电解质
            MEEProtein,         // 代谢：蛋白质
            Adrenaline,         // 肾上腺素
        }

        private static DetailTab curTab = DetailTab.Physique;

        /// <summary>切换右侧分类。切换时重置滚动位置，否则新内容会停在上一页的滚动偏移。</summary>
        private static void SelectTab(DetailTab tab)
        {
            if (curTab == tab) return;
            curTab = tab;
            scrollPos = Vector2.zero;
            viewHeight = 0f;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        public static void Draw(Rect outRect, Pawn pawn)
        {
            outRect = outRect.Rounded();

            // 左 37.5% / 右 62.5%，与原版同比例
            Rect left  = new Rect(outRect.x, outRect.y, outRect.width * 0.375f, outRect.height).Rounded();
            Rect right = new Rect(left.xMax, outRect.y, outRect.width - left.width, outRect.height);

            DrawSummary(left, pawn);
            DrawDetailList(right.ContractedBy(10f), pawn);
        }

        /// <summary>原版体魄技能每升一级所需经验（与 SkillRecord.Learn 内部 1000 阈值一致）。</summary>
        private const float PhysiqueXpPerLevel = 1000f;

        /// <summary>取体魄技能 SkillRecord（defName=Physique），用于读取经验值。取不到返回 null。</summary>
        private static SkillRecord GetPhysiqueSkill(Pawn pawn)
        {
            SkillDef def = DefDatabase<SkillDef>.GetNamed("Physique", false);
            if (def == null || pawn?.skills == null) return null;
            return pawn.skills.GetSkill(def);
        }

        private static void DrawSummary(Rect rect, Pawn pawn)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(9f);
            Widgets.BeginGroup(inner);
            float curY = 4f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // ---- 可点击行：点了切右侧。有 Need 的用带条版本 ----
            // 体魄没有 Need 对象，用 等级/上限 折算百分比（特质可突破上限，故 Clamp01 兜底）
            int lv = PhysiqueLgc.GetPhysiqueLevel(pawn);
            RowButtonBar(inner, ref curY, "Physique".Translate(), lv.ToString(), Color.white,
                DetailTab.Physique, (float)lv / Define.PhysiqueMaxLevel, "查看体魄详细数据");

            // 体魄经验值（仅本 mod 持有该技能的对象显示）
            SkillRecord physSkill = GetPhysiqueSkill(pawn);
            if (physSkill != null)
            {
                Row(inner, ref curY, "体魄经验",
                    $"{physSkill.xpSinceLastLevel:F0} / {PhysiqueXpPerLevel:F0}", Color.white);
            }

            Need_MuscleStrain strain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
            if (strain != null)
            {
                RowButtonBar(inner, ref curY, strain.def.LabelCap,
                    strain.CurLevelPercentage.ToStringPercent(), Color.white,
                    DetailTab.Strain, strain, "查看劳损详细数据");
            }

            Need_Cortisol cortisol = pawn.needs?.TryGetNeed<Need_Cortisol>();
            if (cortisol != null)
            {
                RowButtonBar(inner, ref curY, cortisol.def.LabelCap,
                    cortisol.CurLevelPercentage.ToStringPercent(), Color.white,
                    DetailTab.Cortisol, cortisol, "查看皮质醇详细数据");
            }

            // ---- 肾上腺素（Hediff，Severity 0~1）----
            Hediff adrenalineHediff = GetAdrenalineHediff(pawn);
            float adrSeverity = adrenalineHediff != null ? adrenalineHediff.Severity : 0f;
            RowButtonBar(inner, ref curY, "肾上腺素",
                (adrSeverity * 100f).ToString("F0") + "%", Color.white,
                DetailTab.Adrenaline, adrSeverity, "查看肾上腺素详细数据",
                new List<float> { Define.AdrenalineThresholdDormant, Define.AdrenalineThresholdLow, Define.AdrenalineThresholdMedium },
                0);

            // ---- MEE 四需求：仅当模块加载时显示，并绘制分隔线 ----
            if (MetaBolicLoadCtrl.IsLoadedMME)
            {
                curY += 10f;
                Widgets.DrawLineHorizontal(0f, curY, inner.width, Color.gray);
                curY += 10f;

                foreach (Need need in GetMeeNeedsOrdered(pawn))
                {
                    // 每个代谢需求都是独立按钮（defName 与 DetailTab 枚举名一致），点哪个就看哪个
                    DetailTab tab = (DetailTab)System.Enum.Parse(typeof(DetailTab), need.def.defName);
                    RowButtonBar(inner, ref curY, need.def.LabelCap,
                        need.CurLevelPercentage.ToStringPercent(), Color.white,
                        tab, need, "查看" + need.def.LabelCap + "详细数据");
                }
            }

            // 注：工作效率/代谢率/饥饿速率 已在体魄 Hediff 悬停 tooltip 内展示，此处不再重复。
            Widgets.EndGroup();
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// MEE 四需求的稳定显示顺序：水 → 糖 → 电解质 → 蛋白质。
        /// 用 defName 匹配来排，跨语言都保持一致。
        /// </summary>
        private static readonly string[] MeeNeedDefOrder =
            { "MEEWater", "MEESugar", "MEEElectrolytes", "MEEProtein" };

        /// <summary>
        /// 取 MEE 四需求，按 <see cref="MeeNeedDefOrder"/> 的顺序返回（仅用于左侧概览展示）。
        /// 非 MEE 对象或没有 MEE 需求时返回空列表。
        /// </summary>
        private static List<Need> GetMeeNeedsOrdered(Pawn pawn)
        {
            List<Need> list = new List<Need>();
            if (pawn?.needs == null) return list;
            foreach (Need n in pawn.needs.AllNeeds)
                if (n is Need_MEE_Base) list.Add(n);
            list.Sort((a, b) =>
            {
                int ia = Array.IndexOf(MeeNeedDefOrder, a.def.defName);
                int ib = Array.IndexOf(MeeNeedDefOrder, b.def.defName);
                if (ia < 0) ia = 999;
                if (ib < 0) ib = 999;
                return ia.CompareTo(ib);
            });
            return list;
        }

        /// <summary>
        /// 纯展示行。和原版 HealthCardUtility.DrawLeftRow 一样，
        /// 左右两段用同一个 rect 靠 Text.Anchor 区分，不必切两个 Rect。
        /// </summary>
        private static void Row(Rect rect, ref float curY, string left, string right, Color rightColor)
        {
            Rect r = new Rect(0f, curY, rect.width, 22f);
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(r, left);
            GUI.color = rightColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(r, right);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            curY += r.height;
        }

        /// <summary>格式化为带符号的 %/日 文本（正=带 +，负=自动 -）。用于按天变化的速率量（皮质醇结算、代谢需求结算）。</summary>
        private static string FormatSigned(float v)
        {
            return (v >= 0f ? "+" : "") + v.ToString("F1") + "%/日";
        }

        /// <summary>
        /// 格式化为带符号的 % 文本（正=带 +，负=自动 -）。用于瞬时效果修正（如 +8% 意识 / -8% 视力），
        /// 不含时间维度——肾上腺素效果是直接乘到 stat 上的百分比修正，不是按天/秒变化的速率。
        /// 速率量（%/日、%/秒）请用 FormatSigned / 各自内联格式。
        /// </summary>
        private static string FormatPct(float v)
        {
            return (v >= 0f ? "+" : "") + v.ToString("F1") + "%";
        }

        /// <summary>
        /// 皮质醇结算明细配色：效应为正(皮质醇上升)=红，为负(下降)=绿；bold=true 时用更亮的高饱和色强调净变化。
        /// </summary>
        private static Color ColorForEffect(float effect, bool bold = false)
        {
            if (effect >= 0f)
                return bold ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.48f, 0.48f);
            return bold ? new Color(0.35f, 1f, 0.35f) : new Color(0.48f, 1f, 0.48f);
        }

        /// <summary>
        /// 代谢需求(水/糖/电解质/蛋白)结算明细配色：需求等级上升(被补充)=绿(好)，下降(被消耗)=红(坏)。
        /// 与皮质醇 ColorForEffect 极性相反（皮质醇上升=坏=红）。
        /// </summary>
        private static Color ColorForLevelChange(float effect, bool bold = false)
        {
            if (effect >= 0f)
                return bold ? new Color(0.35f, 1f, 0.35f) : new Color(0.48f, 1f, 0.48f);
            return bold ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.48f, 0.48f);
        }

        /// <summary>
        /// 可点击行。视觉上与 Row 一致，额外具备：
        ///   - 选中态持久高亮（DrawHighlightSelected，原版选中同款贴图）
        ///   - 悬停高亮 + 手型提示 + tooltip
        ///   - 点击切换右侧分类
        /// 注意 ButtonInvisible 必须在 Label 之后调用，否则 GUI.Button 会吃掉点击、
        /// 而且它绘制的空样式会盖在文字上。
        /// </summary>
        private static void RowButton(Rect rect, ref float curY, string left, string right,
                                      Color rightColor, DetailTab tab, string tip = null)
        {
            Rect r = new Rect(0f, curY, rect.width, 22f);
            bool selected = curTab == tab;

            if (selected) Widgets.DrawHighlightSelected(r);
            else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

            Text.Anchor = TextAnchor.MiddleLeft;
            // 选中时标签给个亮色，未选中用略暗的白，形成层次
            GUI.color = selected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(r, left);
            GUI.color = rightColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(r, right);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r)) SelectTab(tab);

            curY += r.height;
        }

        // ============================================================
        // RowButtonBar —— 带迷你 Need 进度条的可点击行
        //
        // 行高比 RowButton 高一点（24f），布局：
        //   ┌──────────────────────────────────────────┐
        //   │ 标签                              数值   │  ← 上半，文字
        //   │ ▓▓▓▓▓▓▓▓░░░░░│░░░░░░░░░░░░░░            │  ← 下半，迷你条
        //   └──────────────────────────────────────────┘
        //
        // 「风格是 need 但比正常小」的实现要点（均源自原版 Need.DrawOnGUI）：
        //   1. 填充用 Widgets.FillableBar + Widgets.BarFullTexHor —— 与原版需求条同一贴图
        //   2. 条高设为 BarHeight=6f。原版 FillableBar 内部 doBorder 判定是
        //      `rect.height > 15f && rect.width > 20f`，8f < 15f 所以自动不画 3px 黑边，
        //      正是"更小更紧凑"想要的效果，不需要自己关。
        //   3. 阈值刻度线自己重画一份：原版 Need.DrawBarThreshold 是 protected，
        //      外部调不到；threshPercents 也是 protected，用 Harmony AccessTools 反射读。
        // ============================================================
        private const float BarRowHeight = 24f;
        private const float BarHeight = 6f;

        /// <summary>
        /// 反射读 Need.threshPercents（原版是 protected，外部不可见）。
        /// 用纯 BCL 反射而非 Harmony AccessTools —— 签名确定、不受 Harmony 版本影响。
        /// FieldInfo 静态缓存，避免每帧反射查找。
        /// 失败返回 null，调用方会跳过阈值线绘制，不影响主体。
        /// </summary>
        private static FieldInfo threshField;
        private static bool threshFieldResolved;

        private static List<float> GetThreshPercents(Need need)
        {
            if (need == null) return null;
            if (!threshFieldResolved)
            {
                threshFieldResolved = true;
                threshField = typeof(Need).GetField("threshPercents",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (threshField == null) return null;
            return threshField.GetValue(need) as List<float>;
        }

        /// <summary>
        /// 可点击行 + 迷你 Need 风格进度条。
        /// need 传入时自动取 CurLevelPercentage / 阈值 / 变化箭头；
        /// 若只想画一个纯数值条（无 Need 对象），用下面的重载传 fillPercent。
        /// </summary>
        private static void RowButtonBar(Rect rect, ref float curY, string left, string right,
                                         Color rightColor, DetailTab tab, Need need, string tip = null)
        {
            float pct = need != null ? Mathf.Clamp01(need.CurLevelPercentage) : 0f;
            RowButtonBar(rect, ref curY, left, right, rightColor, tab, pct, tip,
                         GetThreshPercents(need), need != null ? need.GUIChangeArrow : 0);
        }

        /// <summary>裸百分比版本：给没有 Need 对象的数据（如体魄等级 / 20）用。</summary>
        private static void RowButtonBar(Rect rect, ref float curY, string left, string right,
                                         Color rightColor, DetailTab tab, float fillPercent,
                                         string tip = null, List<float> threshPercents = null,
                                         int changeArrow = 0)
        {
            Rect r = new Rect(0f, curY, rect.width, BarRowHeight);
            bool selected = curTab == tab;

            if (selected) Widgets.DrawHighlightSelected(r);
            else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

            // ---- 上半：文字 ----
            Rect textRect = new Rect(r.x, r.y, r.width, r.height - BarHeight - 2f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = selected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(textRect, left);
            if (!right.NullOrEmpty())
            {
                GUI.color = rightColor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(textRect, right);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // ---- 下半：迷你条。左右各留 1px，避免贴到高亮框边缘 ----
            Rect barRect = new Rect(r.x + 1f, textRect.yMax, r.width - 2f, BarHeight);
            fillPercent = Mathf.Clamp01(fillPercent);
            // 高度 8f < 15f → FillableBar 内部不画黑边，天然比原版需求条紧凑
            Widgets.FillableBar(barRect, fillPercent, Widgets.BarFullTexHor);

            if (threshPercents != null)
            {
                for (int i = 0; i < threshPercents.Count; i++)
                    DrawMiniBarThreshold(barRect, threshPercents[i], fillPercent);
            }
            if (changeArrow != 0)
                Widgets.FillableBarChangeArrows(barRect, changeArrow);

            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r)) SelectTab(tab);

            curY += r.height;
        }

        /// <summary>
        /// 迷你阈值刻度线。复刻原版 Need.DrawBarThreshold 的配色规则：
        /// 已越过的阈值画黑（不透明度 0.9），未到的画灰（0.5）。
        /// 与原版差别：线宽固定 1px、且占满条高（原版占一半高，在 8px 条上会看不见）。
        /// </summary>
        private static void DrawMiniBarThreshold(Rect barRect, float threshPct, float curPct)
        {
            Rect line = new Rect(barRect.x + barRect.width * threshPct - 0.5f, barRect.y, 1f, barRect.height);
            Texture2D tex;
            if (threshPct < curPct)
            {
                tex = BaseContent.BlackTex;
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
            }
            else
            {
                tex = BaseContent.GreyTex;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
            }
            GUI.DrawTexture(line, tex);
            GUI.color = Color.white;
        }

        // ============================================================
        // 右侧：按当前分类画内容
        // ============================================================
        private static void DrawDetailList(Rect rect, Pawn pawn)
        {
            // 标题条，让用户明确当前看的是哪一类
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 26f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(titleRect, TabTitle(curTab, pawn));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(rect.x, titleRect.yMax - 2f, rect.width, Color.gray);

            Rect body = rect;
            body.yMin = titleRect.yMax + 4f;

            Widgets.BeginGroup(body);
            Rect outRect = new Rect(0f, 0f, body.width, body.height);
            Rect view = new Rect(0f, 0f, body.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPos, view);

            float curY = 0f;
            Rect content = new Rect(0f, 0f, view.width, view.height);

            switch (curTab)
            {
                case DetailTab.Physique:  DrawPhysiqueDetail(content, ref curY, pawn);  break;
                case DetailTab.Strain:    DrawStrainDetail(content, ref curY, pawn);    break;
                case DetailTab.Cortisol:  DrawCortisolDetail(content, ref curY, pawn);  break;
                case DetailTab.MEEWater:
                case DetailTab.MEESugar:
                case DetailTab.MEEElectrolytes:
                case DetailTab.MEEProtein:
                    DrawMetabolicDetail(content, ref curY, pawn, curTab.ToString()); break;
                case DetailTab.Adrenaline:
                    DrawAdrenalineDetail(content, ref curY, pawn); break;
            }

            if (Event.current.type == EventType.Repaint) viewHeight = curY;
            else if (Event.current.type == EventType.Layout) viewHeight = Mathf.Max(viewHeight, curY);

            Widgets.EndScrollView();
            Widgets.EndGroup();
        }

        private static string TabTitle(DetailTab tab, Pawn pawn)
        {
            switch (tab)
            {
                case DetailTab.Physique:  return "体魄";
                case DetailTab.Strain:    return "肌肉劳损";
                case DetailTab.Cortisol:  return "皮质醇";
                case DetailTab.Adrenaline: return "肾上腺素";
                case DetailTab.MEEWater:
                case DetailTab.MEESugar:
                case DetailTab.MEEElectrolytes:
                case DetailTab.MEEProtein:
                    Need n = FindMeeNeed(pawn, tab.ToString());
                    return n != null ? n.def.LabelCap : "代谢需求";
            }
            return "";
        }

        /// <summary>小节标题，用于右侧内容分组。suffix 可选，右对齐显示在标题行（用于结算明细带净变化）。</summary>
        private static void Section(Rect rect, ref float curY, string label, string suffix = null, Color suffixColor = default)
        {
            curY += 6f;
            Rect r = new Rect(0f, curY, rect.width, 24f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.75f, 0.85f, 1f);
            Widgets.Label(r, label);
            if (!suffix.NullOrEmpty())
            {
                GUI.color = (suffixColor == default) ? Color.white : suffixColor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(r, suffix);
                Text.Anchor = TextAnchor.MiddleLeft;
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            curY += r.height;
        }

        private static void DrawPhysiqueDetail(Rect rect, ref float curY, Pawn pawn)
        {
            Section(rect, ref curY, "等级与阶段");
            Row(rect, ref curY, "当前等级", PhysiqueLgc.GetPhysiqueLevel(pawn).ToString(), Color.white);
            Row(rect, ref curY, "阶段", PhysiqueLgc.GetPhysiqueStage(pawn).ToString(), Color.white);
            Row(rect, ref curY, "背景故事偏移",
                PhysiqueLgc.GetBackstoryPhysiqueOffset(pawn).ToString("+0;-0;0"), Color.white);
            Row(rect, ref curY, "每日经验衰减",
                PhysiqueLgc.GetDailyDecayXP(pawn).ToString("F1"), Color.white);

            // 体魄经验值（技能 defName=Physique 的 xpSinceLastLevel）
            SkillRecord physSkill = GetPhysiqueSkill(pawn);
            if (physSkill != null)
            {
                float xp = physSkill.xpSinceLastLevel;
                Row(rect, ref curY, "当前经验", xp.ToString("F0"), Color.white);
                Row(rect, ref curY, "距下一级", (PhysiqueXpPerLevel - xp).ToString("F0"), Color.white);
                Row(rect, ref curY, "升级进度", (xp / PhysiqueXpPerLevel).ToStringPercent(), Color.white);
            }

            Section(rect, ref curY, "属性影响");
            Row(rect, ref curY, "食欲",     PhysiqueLgc.GetAppetiteMultiplier(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "战斗加成", PhysiqueLgc.GetPhysiqueBonus(pawn).ToStringPercent(), Color.white);

            Section(rect, ref curY, "激素与恢复");
            Row(rect, ref curY, "恢复加成",
                (PhysiqueLgc.GetRecoveryBonus(pawn) - 1f).ToStringPercent(), Color.white);
            Row(rect, ref curY, "伤害减免",
                (1f - PhysiqueLgc.GetDamageReductionFactor(pawn)).ToStringPercent(), Color.white);
            Row(rect, ref curY, "肾上腺素修正",
                PhysiqueLgc.GetAdrenalinePhysiqueModifier(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "皮质醇修正",
                PhysiqueLgc.GetCortisolPhysiqueModifier(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "肾上腺素惩罚豁免",
                PhysiqueLgc.IsAdrenalineExempt(pawn) ? "是" : "否", Color.white);
        }

        private static void DrawStrainDetail(Rect rect, ref float curY, Pawn pawn)
        {
            Need_MuscleStrain strain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
            if (strain == null)
            {
                Row(rect, ref curY, "（无劳损需求）", "", Color.gray);
                return;
            }

            Section(rect, ref curY, "当前状态");
            Row(rect, ref curY, "储备", strain.CurLevelPercentage.ToStringPercent(), Color.white);
            Row(rect, ref curY, "上限", strain.MaxLevel.ToString("0.00"), Color.white);
            Row(rect, ref curY, "当前值", strain.CurLevel.ToString("F1"), Color.white);

            Section(rect, ref curY, "速率");
            Row(rect, ref curY, "消耗倍率",
                PhysiqueLgc.GetMuscleStrainConsumeMultiplier(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "恢复速率",
                PhysiqueLgc.GetMuscleStrainRecoveryRate(pawn).ToString("F2"), Color.white);
            Row(rect, ref curY, "触发概率倍率",
                PhysiqueLgc.GetMuscleStrainChanceMultiplier(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "护甲缓解", 
                PhysiqueLgc.GetStrainCoverEffect(pawn).ToStringPercent(), Color.white);

            Section(rect, ref curY, "外部乘区（饮品等）");
            Row(rect, ref curY, "劳损速率×",
                strain.GetExtraStrainRateMultiplier().ToString("F2"), Color.white);
            Row(rect, ref curY, "恢复速率×",
                strain.GetExtraStrainRecoveryMultiplier().ToString("F2"), Color.white);
            Row(rect, ref curY, "体魄经验×",
                PhysiqueXpMultUtility.GetExtraXpMult(pawn).ToString("F2"), Color.white);
        }

        private static void DrawCortisolDetail(Rect rect, ref float curY, Pawn pawn)
        {
            Need_Cortisol c = pawn.needs?.TryGetNeed<Need_Cortisol>();
            if (c == null)
            {
                Row(rect, ref curY, "（无皮质醇需求）", "", Color.gray);
                return;
            }

            Section(rect, ref curY, "当前状态");
            Row(rect, ref curY, "水平", c.CurLevelPercentage.ToStringPercent(), Color.white);
            Row(rect, ref curY, "分级", c.GetCortisolLevel().ToString(), Color.white);
            Row(rect, ref curY, "严重度", c.GetSeverity().ToStringPercent(), Color.white);
            Row(rect, ref curY, "变化趋势", c.GetChangeTrend(), Color.white);

            Section(rect, ref curY, "风险");
            Row(rect, ref curY, "神经衰弱概率",
                c.GetNeurastheniaProbability(c.GetSeverity()).ToStringPercent(), Color.white);
            var fight = c.GetSocialFightChanceInfo();
            Row(rect, ref curY, "社交冲突（" + fight.tierLabel + "）",
                fight.factor.ToStringPercent(), Color.white);

            // ===== 结算明细：仅显示非零分量 + 净变化 =====
            var b = c.GetCortisolBreakdown();
            // 各分量对皮质醇的净效应：正=上升(红)，负=下降(绿)
            float eBase    = -b.baseDecay;   // 自然衰减 → 下降
            float eExtra   = -b.extraDecay;  // 饮品额外衰减 → 下降
            float ePhysique = b.physique;     // 体魄修正 → 可正可负
            float eSugar   = -b.sugarMod;     // 糖调制：正=抑制 → 下降

            // 结算明细：净变化并入标题（%/日，红=上升·绿=下降）
            Section(rect, ref curY, "结算明细（%/日）", "净 " + FormatSigned(b.net), ColorForEffect(b.net, true));
            if (System.Math.Abs(eBase) > 0.001f)
                Row(rect, ref curY, "自然衰减",       FormatSigned(eBase),     ColorForEffect(eBase));
            if (System.Math.Abs(eExtra) > 0.001f)
                Row(rect, ref curY, "饮品额外衰减",    FormatSigned(eExtra),    ColorForEffect(eExtra));
            // 应激增长：拆成各源头（仅显示当前激活且非零项，红=上升）
            foreach (var src in c.GetCortisolGrowthSources())
            {
                if (System.Math.Abs(src.perDay) > 0.001f)
                    Row(rect, ref curY, src.label, FormatSigned(src.perDay), ColorForEffect(src.perDay));
            }
            if (System.Math.Abs(ePhysique) > 0.001f)
                Row(rect, ref curY, "体魄修正",        FormatSigned(ePhysique), ColorForEffect(ePhysique));
            if (System.Math.Abs(eSugar) > 0.001f)
                Row(rect, ref curY, "糖↔皮质醇调制",  FormatSigned(eSugar),    ColorForEffect(eSugar));
        }

        /// <summary>取肾上腺素 Hediff（defName=Adrenaline），取不到返回 null。</summary>
        private static Hediff GetAdrenalineHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            return pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("Adrenaline", false));
        }

        /// <summary>肾上腺素阶段中文标签。</summary>
        private static string AdrenalineLevelLabel(AdrenalineLevel level)
        {
            switch (level)
            {
                case AdrenalineLevel.Dormant: return "休眠";
                case AdrenalineLevel.Low:     return "低浓度";
                case AdrenalineLevel.Medium:  return "中浓度";
                case AdrenalineLevel.High:    return "高浓度";
            }
            return "";
        }

        private static void DrawAdrenalineDetail(Rect rect, ref float curY, Pawn pawn)
        {
            Hediff adrenaline = GetAdrenalineHediff(pawn);
            float severity = adrenaline != null ? adrenaline.Severity : 0f;
            AdrenalineLevel level = AdrenalineLogic.GetAdrenalineLevel(severity);
            AdrenalineEffects effects = AdrenalineLogic.CalculateAdrenalineEffects(pawn);

            Section(rect, ref curY, "当前状态");
            Row(rect, ref curY, "阶段", AdrenalineLevelLabel(level), Color.white);
            Row(rect, ref curY, "浓度", (severity * 100f).ToString("F0") + "%", Color.white);

            // 净变化/秒（生成−衰减），正数=浓度上升
            float netPerSec = AdrenalineProducer.CalculateNetChangePerSecond(pawn);
            Row(rect, ref curY, "净变化",
                (netPerSec >= 0f ? "+" : "") + (netPerSec * 100f).ToString("F1") + "%/秒",
                netPerSec >= 0f ? new Color(1f, 0.6f, 0.3f) : new Color(0.5f, 0.8f, 1f));

            // 迷你条（阶段阈值刻度）
            Rect barRect = new Rect(0f, curY, rect.width - 2f, 8f);
            Widgets.FillableBar(barRect, Mathf.Clamp01(severity), Widgets.BarFullTexHor);
            DrawMiniBarThreshold(barRect, Define.AdrenalineThresholdDormant, severity);
            DrawMiniBarThreshold(barRect, Define.AdrenalineThresholdLow, severity);
            DrawMiniBarThreshold(barRect, Define.AdrenalineThresholdMedium, severity);
            curY += barRect.height + 4f;

            Section(rect, ref curY, "当前效果");
            if (level == AdrenalineLevel.Dormant)
            {
                Row(rect, ref curY, "（休眠，无加成/惩罚）", "", Color.gray);
            }
            else
            {
                // 配色：增益=绿，惩罚=红（与代谢需求一致）。
                // 注意：这些是瞬时百分比修正（直接乘到 stat 上），单位用 % 而非 %/日（%/日 用于速率量）。
                Row(rect, ref curY, "意识",     FormatPct(effects.Consciousness * 100f),     ColorForLevelChange(effects.Consciousness));
                Row(rect, ref curY, "移动速度", FormatPct(effects.MoveSpeed * 100f),         ColorForLevelChange(effects.MoveSpeed));
                Row(rect, ref curY, "呼吸/循环",FormatPct(effects.Respiratory * 100f),       ColorForLevelChange(effects.Respiratory));
                Row(rect, ref curY, "代谢",     FormatPct(effects.Metabolism * 100f),         ColorForLevelChange(effects.Metabolism));
                Row(rect, ref curY, "近战伤害", FormatPct(effects.MeleeDamage * 100f),        ColorForLevelChange(effects.MeleeDamage));
                Row(rect, ref curY, "闪避",     FormatPct(effects.Dodge * 100f),              ColorForLevelChange(effects.Dodge));
                Row(rect, ref curY, "近战命中", FormatPct(effects.MeleeHitReduction * 100f),  ColorForLevelChange(effects.MeleeHitReduction));
                Row(rect, ref curY, "视力",     FormatPct(effects.VisionReduction * 100f),    ColorForLevelChange(effects.VisionReduction));
                Row(rect, ref curY, "听力",     FormatPct(effects.HearingReduction * 100f),   ColorForLevelChange(effects.HearingReduction));
            }

            Section(rect, ref curY, "体魄与透支");
            Row(rect, ref curY, "体魄修正",
                FormatPct((effects.PhysiqueModifier - 1f) * 100f), ColorForLevelChange(effects.PhysiqueModifier - 1f));
            Row(rect, ref curY, "透支豁免", PhysiqueLgc.IsAdrenalineExempt(pawn) ? "是" : "否", Color.white);
            if (level != AdrenalineLevel.Dormant)
            {
                float rest = effects.RestMultiplier;
                Row(rect, ref curY, "休息需求", (rest >= 1f ? "+" : "") + ((rest - 1f) * 100f).ToString("F0") + "%",
                    ColorForLevelChange(rest - 1f));
            }
        }

        /// <summary>按 defName 在 pawn 身上找指定的 MEE 需求（四个独立代谢按钮各自指向自己的详情）。</summary>
        private static Need FindMeeNeed(Pawn pawn, string defName)
        {
            if (pawn?.needs == null) return null;
            foreach (Need n in pawn.needs.AllNeeds)
                if (n.def.defName == defName) return n;
            return null;
        }

        private static void DrawMetabolicDetail(Rect rect, ref float curY, Pawn pawn, string defName)
        {
            if (!MetaBolicLoadCtrl.IsLoadedMME)
            {
                Row(rect, ref curY, "（Metabolic Essential 模块未启用）", "", Color.gray);
                return;
            }
            Need need = FindMeeNeed(pawn, defName);
            if (need == null)
            {
                Row(rect, ref curY, "（未找到该代谢需求）", "", Color.gray);
                return;
            }

            Section(rect, ref curY, need.def.LabelCap);
            Row(rect, ref curY, "体魄阶段", PhysiqueLgc.GetPhysiqueStage(pawn).ToString(), Color.white);
            Row(rect, ref curY, "储备",   need.CurLevelPercentage.ToStringPercent(), Color.white);
            Row(rect, ref curY, "上限",   need.MaxLevel.ToString("0.00"),         Color.white);
            Row(rect, ref curY, "当前值", need.CurLevel.ToString("F2"),           Color.white);
            Row(rect, ref curY, "满足度", ((Need_MEE_Base)need).Severity.ToStringPercent(), Color.white);

            // 结算明细：自然消耗 + 外部调节，净变化并入标题（%/日，绿=补充·红=消耗）
            var mee = (Need_MEE_Base)need;
            var mb = mee.GetMEEBreakdown();
            float mNet = mb.NetPerDay * 100f; // 占 MaxLevel 的 %/日
            Section(rect, ref curY, "结算明细（%/日）", "净 " + FormatSigned(mNet), ColorForLevelChange(mNet, true));
            if (System.Math.Abs(mb.naturalFall) > 0.0001f)
                Row(rect, ref curY, "自然消耗", FormatSigned(-mb.naturalFall * 100f), ColorForLevelChange(-mb.naturalFall * 100f));
            if (System.Math.Abs(mb.extraFall) > 0.0001f)
                Row(rect, ref curY, "外部调节", FormatSigned(-mb.extraFall * 100f), ColorForLevelChange(-mb.extraFall * 100f));

            // 迷你条（带阈值刻度 + 变化箭头），让单需求页也能一眼看趋势
            Rect barRect = new Rect(0f, curY, rect.width - 2f, 8f);
            Widgets.FillableBar(barRect, Mathf.Clamp01(need.CurLevelPercentage), Widgets.BarFullTexHor);
            List<float> th = GetThreshPercents(need);
            if (th != null)
                for (int i = 0; i < th.Count; i++)
                    DrawMiniBarThreshold(barRect, th[i], need.CurLevelPercentage);
            if (need.GUIChangeArrow != 0) Widgets.FillableBarChangeArrows(barRect, need.GUIChangeArrow);
            curY += barRect.height + 4f;

            // 需求说明（多行）
            string desc = need.def.description;
            if (!desc.NullOrEmpty())
            {
                Section(rect, ref curY, "说明");
                float h = Text.CalcHeight(desc, rect.width);
                Widgets.Label(new Rect(0f, curY, rect.width, h), desc);
                curY += h;
            }
        }
    }
}
