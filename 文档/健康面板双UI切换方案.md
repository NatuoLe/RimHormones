# 健康面板双 UI 切换（原本 UI / 体魄 UI）落地方案

> 目标：在健康面板顶部加一组两态 toggle。按钮 1 = 原版健康 UI（完全不变），
> 按钮 2 = 自建体魄 UI，**复用同一个面板容器**（630×430），不新开窗口。
>
> 结论：**可行，且不需要改 ITab 注册、不需要改面板尺寸。**
> 全部改动集中在 1 个新 .cs 文件 + 1 行 csproj 注册。

---

## 一、原版结构（rimsage 核实，1.6.4871）

| 层 | 类型 / 方法 | 作用 |
|---|---|---|
| 容器 | `ITab_Pawn_Health`（`ITab` → `InspectTabBase`） | `size = 630×430`，`labelKey="TabHealth"`。`DoTabGUI()` 用 `ImmediateWindow` 画背景 + 右上关闭按钮，然后调 `FillTab()` |
| 入口 | `ITab_Pawn_Health.FillTab()` | 只有一句：`HealthCardUtility.DrawPawnHealthCard(new Rect(Vector2.zero, size), pawn, allowOps, showBloodLoss, SelThing)` |
| **分区** | `HealthCardUtility.DrawPawnHealthCard(outRect, ...)` | `outRect.y += 20`、`height -= 20`；左 `width*0.375` → `DrawHealthSummary`；右剩余 → `DrawHediffListing` |
| 左区 | `DrawHealthSummary` | `Widgets.DrawMenuSection` + `TabDrawer.DrawTabs`（概况/手术）+ `DrawOverviewTab` / `DrawMedOperationsTab` |
| 右区 | `DrawHediffListing` | 滚动列表 + 底部出血率行 |

关键点：
1. **页签是画在 rect 上边缘之外的**（`TabDrawer.DrawTabs` 内部 `rect.y -= 32f`）。所以 `DrawPawnHealthCard` 一进来就 `y += 20` 给页签留位置 —— 这块 20px 顶部空间原版并没有画东西，可利用。
2. `DrawPawnHealthCard` 是 **public static**，Harmony 打它最稳。
3. 该方法有 **4 个调用点**，必须区分对待：

| 调用点 | 是否应该受 toggle 影响 |
|---|---|
| `ITab_Pawn_Health.FillTab` | ✅ 是（我们要改的就是这个） |
| `Dialog_InfoCard`（信息卡） | ❌ 不应该 |
| `WITab_Caravan_Health`（商队健康） | ⚠️ 可选，建议先不动 |
| `StartingPawnUtility` / `Dialog_GrowthMomentChoices` | 只调 `DrawHediffListing`，不受影响 |

因此**不要直接无条件 patch `DrawPawnHealthCard`**，否则信息卡里也会冒出体魄 UI。

---

## 二、方案对比

| 方案 | 做法 | 评价 |
|---|---|---|
| **A. Patch `ITab_Pawn_Health.FillTab`（推荐）** | Prefix 拦 `FillTab`，自己画 toggle；选原版态就 `return true` 放行，选体魄态就画自己的然后 `return false` | ✅ 天然只影响健康页签，不污染信息卡/商队<br>✅ 原版态零风险<br>✅ 改动最小 |
| B. Patch `DrawPawnHealthCard` + 调用方判断 | 需要靠 `Find.MainTabsRoot.OpenTab` 之类间接判断来源 | ❌ 判断脆弱，容易误伤 |
| C. 新增独立 ITab | `ThingDef.inspectorTabs` 打 XML 补丁加一个页签 | ❌ 不满足"用当前 UI 容器"，且要 patch 人类/尸体两套 def |
| D. 往 `DrawHealthSummary` 的 TabRecord 列表里塞第三个页签 | Postfix 改不了已画完的东西，得 Prefix 重写整个方法 | ❌ 与其他改健康面板的 mod 冲突面大 |

**采用方案 A。**

---

## 三、实现

### 3.1 新文件 `_indexProj/Source/UI/HealthTabPhysiqueUI.cs`

```csharp
using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Hormones.UI
{
    /// <summary>
    /// 健康面板顶部两态切换：原本 UI / 体魄 UI。
    /// 复用 ITab_Pawn_Health 的同一个 630×430 容器，不新开窗口。
    ///
    /// 只 patch ITab_Pawn_Health.FillTab —— 这样信息卡(Dialog_InfoCard)、
    /// 商队健康(WITab_Caravan_Health) 等其他调用 DrawPawnHealthCard 的地方
    /// 完全不受影响。
    /// </summary>
    public static class HealthTabUIState
    {
        public enum Mode { Vanilla, Physique }

        /// <summary>当前模式。静态 = 全局共享，切小人不重置（与原版 onOperationTab 行为一致）。</summary>
        public static Mode Current = Mode.Vanilla;

        /// <summary>toggle 条高度。原版在 DrawPawnHealthCard 里预留了 20px 顶部空间，这里再自己撑开。</summary>
        public const float ToggleRowHeight = 26f;
        private const float ToggleWidth = 96f;
        private const float ToggleGap = 4f;

        /// <summary>
        /// 画 toggle 条，返回它占掉的矩形。
        /// 放在容器顶部靠左，避开右上角关闭按钮（CloseButtonFor 占 x 最右 22px、y 顶部 22px）。
        /// </summary>
        public static Rect DrawToggleRow(Rect containerRect)
        {
            Rect row = new Rect(containerRect.x, containerRect.y, containerRect.width, ToggleRowHeight);

            Rect btn1 = new Rect(row.x, row.y + 2f, ToggleWidth, ToggleRowHeight - 4f);
            Rect btn2 = new Rect(btn1.xMax + ToggleGap, btn1.y, ToggleWidth, btn1.height);

            DrawToggleButton(btn1, "HormonesHealthTab_Vanilla".Translate(), Mode.Vanilla);
            DrawToggleButton(btn2, "HormonesHealthTab_Physique".Translate(), Mode.Physique);

            return row;
        }

        private static void DrawToggleButton(Rect rect, string label, Mode mode)
        {
            bool selected = Current == mode;

            // DrawOptionBackground 会在 selected 时画高亮底，是原版选项条同款观感
            Widgets.DrawOptionBackground(rect, selected);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            GUI.color = selected ? Color.white : new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(rect))
            {
                if (Current != mode)
                {
                    Current = mode;
                    SoundDefOf.RowTabSelect.PlayOneShotOnCamera();
                }
            }
            if (Mouse.IsOver(rect) && !selected) Widgets.DrawHighlight(rect);
        }
    }

    [HarmonyPatch(typeof(ITab_Pawn_Health), "FillTab")]
    public static class ITab_Pawn_Health_FillTab_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ITab_Pawn_Health __instance)
        {
            Pawn pawn = ResolvePawn(__instance);
            // 拿不到 pawn 或非激素对象 → 完全交还原版，不画 toggle
            if (pawn == null || !PhysiqueLgc.IsHormoneSubject(pawn)) return true;

            // size 是 InspectTabBase 的 protected 字段，用 Traverse 读，避免反射写死
            Vector2 size = Traverse.Create(__instance).Field("size").GetValue<Vector2>();
            Rect container = new Rect(Vector2.zero, size);

            Rect toggleRow = HealthTabUIState.DrawToggleRow(container);

            Rect content = container;
            content.yMin = toggleRow.yMax;

            if (HealthTabUIState.Current == HealthTabUIState.Mode.Vanilla)
            {
                // 原版态：自己调一次原版绘制（区域下移了 toggle 高度），然后跳过原方法
                HealthCardUtility.DrawPawnHealthCard(
                    content,
                    pawn,
                    ShouldAllowOperations(__instance),
                    HealthCardUtility.ShowBloodLoss(Find.Selector.SingleSelectedThing),
                    Find.Selector.SingleSelectedThing);
            }
            else
            {
                PhysiqueHealthCard.Draw(content, pawn);
            }
            return false;
        }

        private static Pawn ResolvePawn(ITab_Pawn_Health tab)
        {
            // 复刻原版 PawnForHealth（private 属性）
            Thing t = Find.Selector.SingleSelectedThing;
            if (t is Pawn p) return p;
            if (t is Corpse c) return c.InnerPawn;
            return null;
        }

        private static bool ShouldAllowOperations(ITab_Pawn_Health tab)
        {
            // 原版是 private bool ShouldAllowOperations()，直接反射调，保证规则一致
            return Traverse.Create(tab).Method("ShouldAllowOperations").GetValue<bool>();
        }
    }
}
```

> **注意**：`Prefix` 里手动调 `HealthCardUtility.DrawPawnHealthCard` 的写法有个副作用 ——
> 如果别的 mod 也 Postfix 了 `FillTab`，我们 `return false` 后它的 Postfix 依然会跑（Harmony 语义），
> 通常无害。若担心兼容，可改成不下移区域、把 toggle 画进原版预留的顶部 20px 内，
> 原版态直接 `return true` 完全不接手（见 3.3「更保守的变体」）。

### 3.2 体魄 UI 内容 `PhysiqueHealthCard.Draw`

沿用原版左右分栏的观感，数据直接接现成的 `PhysiqueLgc` / `Need_MuscleStrain`：

```csharp
using RimWorld;
using UnityEngine;
using Verse;

namespace Hormones.UI
{
    /// <summary>体魄 UI 的内容绘制。布局刻意模仿原版健康页，降低违和感。</summary>
    public static class PhysiqueHealthCard
    {
        private static Vector2 scrollPos = Vector2.zero;
        private static float viewHeight = 0f;

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

            Row(inner, ref curY, "Physique".Translate(),
                PhysiqueLgc.GetPhysiqueLevel(pawn).ToString(), Color.white);
            Row(inner, ref curY, "WorkEfficiency".Translate(),
                PhysiqueLgc.GetWorkEfficiency(pawn).ToStringPercent(), Color.white);
            Row(inner, ref curY, "MetabolicRate".Translate(),
                PhysiqueLgc.GetMetabolicRate(pawn).ToStringPercent(), Color.white);
            Row(inner, ref curY, "HungerRate".Translate(),
                PhysiqueLgc.GetHungerRate(pawn).ToStringPercent(), Color.white);

            curY += 10f;
            Widgets.DrawLineHorizontal(0f, curY, inner.width, Color.gray);
            curY += 10f;

            Need_MuscleStrain strain = pawn.needs?.TryGetNeed<Need_MuscleStrain>();
            if (strain != null)
                Row(inner, ref curY, strain.def.LabelCap, strain.CurLevelPercentage.ToStringPercent(), Color.white);

            Widgets.EndGroup();
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void Row(Rect rect, ref float curY, string left, string right, Color rightColor)
        {
            Rect r = new Rect(0f, curY, rect.width, 22f);
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(r.x, r.y, r.width * 0.6f, r.height), left);
            GUI.color = rightColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f, r.height), right);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            curY += r.height;
        }

        private static void DrawDetailList(Rect rect, Pawn pawn)
        {
            Widgets.BeginGroup(rect);
            Rect view = new Rect(0f, 0f, rect.width - 16f, viewHeight);
            Widgets.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), ref scrollPos, view);

            float curY = 0f;
            // TODO: 在此逐行画劳损 Hediff / 肾上腺素 / MEE 四需求等
            // 每行样式可复用上面的 Row()，或直接 Widgets.Label

            if (Event.current.type == EventType.Repaint) viewHeight = curY;
            Widgets.EndScrollView();
            Widgets.EndGroup();
        }
    }
}
```

### 3.3 更保守的变体（若担心与其他 mod 冲突）

原版 `DrawPawnHealthCard` 开头就 `outRect.y += 20f; outRect.height -= 20f;`，
这 20px 顶部条**原版是空的**。所以可以：

- 原版态：`return true`（**完全不接手**，原方法照常跑），只在 `ExtraOnGUI` 或
  `FillTab` 的 **Postfix** 里往那 20px 里画 toggle。
- 体魄态：Prefix 画 toggle + 自己的内容，`return false`。

代价是 toggle 只有 20px 高、按钮偏矮。若接受 26px 的更好手感，就用 3.1 的写法。

### 3.4 csproj 注册（必做，否则静默不编译）

`_indexProj/Assembly-CSharp.csproj` 的 `<ItemGroup>` 里加：

```xml
<Compile Include="Source\UI\HealthTabPhysiqueUI.cs" />
<Compile Include="Source\UI\PhysiqueHealthCard.cs" />
```

### 3.5 翻译键

`Languages/ChineseSimplified/Keyed/*.xml`：
```xml
<HormonesHealthTab_Vanilla>原本UI</HormonesHealthTab_Vanilla>
<HormonesHealthTab_Physique>体魄UI</HormonesHealthTab_Physique>
```
`Languages/English/Keyed/*.xml`：
```xml
<HormonesHealthTab_Vanilla>Health</HormonesHealthTab_Vanilla>
<HormonesHealthTab_Physique>Physique</HormonesHealthTab_Physique>
```

---

## 四、注意事项 / 坑

1. **`size` 是 `protected` 字段**（`InspectTabBase.size`），不能直接访问，用
   `Traverse.Create(tab).Field("size")`。别硬编码 630×430 —— 有 mod 会改它。
2. **`PawnForHealth` 与 `ShouldAllowOperations` 都是 private**。前者逻辑简单可复刻，
   后者规则复杂（涉及囚犯/敌对/机械体判定），**建议反射调原版**，避免行为漂移。
3. **不要 patch `DrawPawnHealthCard` 本身**，会波及信息卡与商队健康面板。
4. **Rect 坐标是容器局部坐标**（`ImmediateWindow` 内部已 BeginGroup），起点是 `(0,0)`。
5. **右上角关闭按钮**占 `x ∈ [width-22, width-4]`、`y ∈ [4, 22]`，toggle 靠左放不会撞。
6. `Widgets.BeginGroup` / `EndGroup`、`BeginScrollView` / `EndScrollView` 必须成对，
   异常抛出会导致 GUI 状态错乱、整个界面变形。绘制逻辑里别抛异常。
7. 尸体（`Corpse`）也会打开健康页，`PhysiqueLgc.IsHormoneSubject` 已能过滤，
   但仍要注意 `pawn.needs` 在死亡后可能为 null —— 上面代码已用 `?.`。
8. 状态存 `static`：切换小人不重置。若希望每个小人独立记忆，改存
   `HormonesComponent`（已有持久化机制）。

---

## 五、验证清单

- [ ] 选中殖民者 → 健康页顶部出现两个按钮，默认「原本UI」高亮
- [ ] 点「体魄UI」→ 内容整体换成体魄面板，容器大小/位置不变
- [ ] 点「原本UI」→ 与未装 mod 时完全一致（页签、概况、手术、滚动列表都正常）
- [ ] 打开小人信息卡（i 图标）→ 健康区**没有** toggle（未被污染）
- [ ] 选中动物 / 尸体 → 不崩，行为合理
- [ ] 商队界面健康页 → 不崩
- [ ] 编译后 `grep -c "HealthTabUIState" RimHormones.dll` > 0（确认真的编进去了）
