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

            curY += 10f;
            Widgets.DrawLineHorizontal(0f, curY, inner.width, Color.gray);
            curY += 10f;

            // ---- 静态行：只展示，不可点 ----
            Row(inner, ref curY, "WorkEfficiency".Translate(),
                PhysiqueLgc.GetWorkEfficiency(pawn).ToStringPercent(), Color.white);
            Row(inner, ref curY, "MetabolicRate".Translate(),
                PhysiqueLgc.GetMetabolicRate(pawn).ToStringPercent(), Color.white);
            Row(inner, ref curY, "HungerRate".Translate(),
                PhysiqueLgc.GetHungerRate(pawn).ToStringPercent(), Color.white);

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
                case DetailTab.MEEWater:
                case DetailTab.MEESugar:
                case DetailTab.MEEElectrolytes:
                case DetailTab.MEEProtein:
                    Need n = FindMeeNeed(pawn, tab.ToString());
                    return n != null ? n.def.LabelCap : "代谢需求";
            }
            return "";
        }

        /// <summary>小节标题，用于右侧内容分组。</summary>
        private static void Section(Rect rect, ref float curY, string label)
        {
            curY += 6f;
            Rect r = new Rect(0f, curY, rect.width, 24f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.75f, 0.85f, 1f);
            Widgets.Label(r, label);
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

            Section(rect, ref curY, "属性影响");
            Row(rect, ref curY, "工作效率", PhysiqueLgc.GetWorkEfficiency(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "代谢率",   PhysiqueLgc.GetMetabolicRate(pawn).ToStringPercent(), Color.white);
            Row(rect, ref curY, "饥饿速率", PhysiqueLgc.GetHungerRate(pawn).ToStringPercent(), Color.white);
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
            Row(rect, ref curY, "上限", strain.MaxLevel.ToString("F0"), Color.white);
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

            // 压力源是多行文本，用 Label 直接铺开而不是塞进单行 Row
            string stressors = c.GetCurrentStressors();
            if (!stressors.NullOrEmpty())
            {
                Section(rect, ref curY, "当前压力源");
                float h = Text.CalcHeight(stressors, rect.width);
                Widgets.Label(new Rect(0f, curY, rect.width, h), stressors);
                curY += h;
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
            Row(rect, ref curY, "储备",   need.CurLevelPercentage.ToStringPercent(), Color.white);
            Row(rect, ref curY, "上限",   need.MaxLevel.ToString("F0"),            Color.white);
            Row(rect, ref curY, "当前值", need.CurLevel.ToString("F2"),           Color.white);
            Row(rect, ref curY, "满足度", ((Need_MEE_Base)need).Severity.ToStringPercent(), Color.white);

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
