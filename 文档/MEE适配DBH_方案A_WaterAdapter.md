# MEE 适配 DBH 水系统 — 方案 A 详细实现（WaterAdapter）

> 本文件是 `MEE适配DBH水系统方案.md` 中「方案 A（适配器）」的**细化落地设计**。
> 目标：用统一的 `WaterAdapter` 把"当前生效的水需求"收敛到一处，DBH 在场时 DBH 是水的唯一真相源，MEE 的体魄抗风险特色以"属性/后果层"形式保留。
>
> 用户点名的三件事，本文逐一给出确定答案：
> 1. **`WaterAdapter.GetWaterNeed`** —— DBH 在场返回 DBH 的水需求，否则返回 MEE 的。
> 2. **初始化时初始化适配器** —— `StaticConstructorOnStartup` + `RimHormonesMod` 入口双保险，探测到 DBH 即切到 DBH 模式。
> 3. **相关属性修改（含"从 ground 补水的 thing 到底用哪个"）** —— 见 §5.2 与 §5.5。

---

## 1. 设计决策（一句话版）

| 问题 | 方案 A 的确定答案 |
|---|---|
| `GetWaterNeed(pawn)` 返回什么？ | DBH 模式 → `pawn.needs.TryGetNeed(DBHThirst)`（DBH 的 `Need_Thirst`）；MEE 模式 → `TryGetNeed<Need_MEE_Water>()` |
| DBH 模式下 `Need_MEE_Water` 怎么办？ | **不实例化**（Harmony 摘除），消除双水条；MEE 水条 UI 改用 `WaterAdapter` 显示 DBH 水位 |
| 从 ground 补水的 thing 用哪个？ | MEE 模式用 `MEE_RawWater` 物品或地形水格（`Need_MEE_Water.Drink()`）；**DBH 模式 MEE 完全不主动喝地表水**，地表/设施喝水全交 DBH（`DBHDrinkFromGround`/`DBHDrinkFromBasin`） |
| 体魄缩放怎么保留？ | MEE 模式直接作用于 `MaxLevel`（0.90~1.45）；DBH 模式转为 `WaterAdapter` 暴露的**阈值/后果**倍率（渴得更晚、水中毒更抗），不碰 DBH 存储 |

---

## 2. WaterAdapter 核心类

新增文件：`_indexProj/Source/Comps/WaterAdapter.cs`

```csharp
using RimWorld;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 水需求统一适配器（方案 A）。运行时决定"当前生效的水系统"：
    ///   - DBH 在场 → DBH 是水的唯一真相源（Need_Thirst / DefName "DBHThirst"）
    ///   - 否则     → MEE 自带水需求（Need_MEE_Water / DefName "MEEWater"）
    /// 全代码库通过本类访问水需求，禁止再直接 new / TryGetNeed 具体类型。
    /// </summary>
    public static class WaterAdapter
    {
        public enum System { MEE, DBH }

        /// <summary>当前水系统。由 Init() 在启动期根据 DBHDetector 设定，之后只读。</summary>
        public static System Active { get; private set; } = System.MEE;
        public static bool UseDBH => Active == System.DBH;

        // ---- DBH 引用缓存（惰性、零硬引用） ----
        private static NeedDef _dbhThirstDef;
        private static NeedDef DBHThirstDef
            => _dbhThirstDef ?? (_dbhThirstDef = DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst"));

        /// <summary>初始化适配器。必须在 DBHDetector 之后调用（见 §3 时序）。</summary>
        public static void Init()
        {
            Active = (DBHDetector.DBHPresent && DBHThirstDef != null) ? System.DBH : System.MEE;
        }

        /// <summary>拿到当前生效的水需求实例。DBH 在场返回 Need_Thirst，否则 MEE_Water。可能为 null（小人无该需求时）。</summary>
        public static Need GetWaterNeed(Pawn pawn)
        {
            if (pawn?.needs == null) return null;
            return UseDBH
                ? pawn.needs.TryGetNeed(DBHThirstDef)
                : pawn.needs.TryGetNeed<Need_MEE_Water>();
        }

        /// <summary>当前水位（0~1），统一入口供 UI / AI 判断。</summary>
        public static float GetWaterLevel(Pawn pawn) => GetWaterNeed(pawn)?.CurLevelPercentage ?? 1f;

        /// <summary>是否低于"口渴阈值"。阈值本身随体魄变化（见 §5.5）。</summary>
        public static bool IsThirsty(Pawn pawn, float baseThreshold = 0.5f)
            => GetWaterLevel(pawn) < GetThirstUrgencyThreshold(pawn, baseThreshold);

        // ===== 体魄缩放的"属性/后果"层（仅 DBH 模式生效；MEE 模式由 Need_MEE_Water.MaxLevel 直接承载） =====

        /// <summary>口渴紧迫阈值：高体魄→更高阈值（更晚觉得渴）。baseThreshold 默认 0.5。</summary>
        public static float GetThirstUrgencyThreshold(Pawn pawn, float baseThreshold = 0.5f)
            => baseThreshold * PhysiqueLgc.GetMEEWaterCapacityMult(pawn);

        /// <summary>水中毒/脱水后果倍率：高体魄→更高容限（后果更轻）。用于 MEE 减益/飘字逻辑。</summary>
        public static float GetWaterHazardTolerance(Pawn pawn)
            => PhysiqueLgc.GetMEEWaterCapacityMult(pawn);
    }
}
```

> **为什么返回 `Need` 基类而非具体类型？** 消费方只关心 `CurLevel` / `CurLevelPercentage` / `Satisfy`，这些都在 `Need` 基类上；`Need_Thirst` 与 `Need_MEE_Water` 都是 `Need` 子类，适配对调用方透明。
>
> **DBH 程序集零引用**：只靠 `DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst")` 拿 def，不 `using DubsBadHygiene`。DBH 缺失时 `DBHThirstDef == null` → 全程走 MEE 分支。

---

## 3. 初始化时序（确保"初始化时初始化适配器"）

DBH 的 `NeedDef` 在 StaticConstructorOnStartup 阶段已注册，但要保证 MEE 的初始化在 DBH **之后**读它。两道保险：

1. **`DBHDetector`（已有，复用）** —— 探测 `DBHThirst` 是否存在：
   ```csharp
   [StaticConstructorOnStartup]
   public static class DBHDetector
   {
       public static readonly bool DBHPresent =
           DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst") != null;
   }
   ```
2. **`WaterAdapter.Init()` 在 MEE mod 入口显式调用**（不依赖 ctor 顺序）：
   在 `RimHormonesMod` 的静态构造或 `PostLoad` 末尾、`DBHDetector` 之后调用：
   ```csharp
   static RimHormonesMod()   // 或 [StaticConstructorOnStartup] 的合适入口
   {
       DBHDetector.StaticInit();   // 确保已探测
       WaterAdapter.Init();        // 设定 Active
       // ... 其余 MEE 注册
   }
   ```
3. **About/LoadAfter 加 DBH**（保底 mod 顺序）：
   `About/About.xml` 的 `<loadAfters>` 加入 `836308268`（DBH 的 packageId），确保 DBH 先初始化、其 NeedDef 已注册。

> 仅依赖 `StaticConstructorOnStartup` 的 ctor 执行顺序不可靠（mod 间无保证），所以**第 2 步显式调用是必须的**。

---

## 4. 双水需求治理：DBH 模式下 `Need_MEE_Water` 的处理

**决策：DBH 模式不实例化 `Need_MEE_Water`（单点 Harmony 摘除）。**

理由：
- 若实例化但 NeedInterval 早退 + CurLevel 跟随 DBH，会造成"两条水条并存"观感，且 `NeedDef` 仍进需求列表（UI/统计污染）。
- 用户明确要求 `GetWaterNeed` 在 DBH 模式返回 **DBH 的**，所以 MEE 水条 UI 应直接渲染 DBH 水位，无需 MEE_Water 实例。

实现（单点 patch，最小侵入）：
```csharp
// HarmonyPatch：DBH 模式摘除 MEEWater 需求
[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
public static class Patch_PawnNeedsTracker_ShouldHaveNeed
{
    [HarmonyPostfix]
    public static void Postfix(NeedDef def, ref bool __result)
    {
        if (WaterAdapter.UseDBH && def.defName == "MEEWater")
            __result = false;   // 不创建该需求实例
    }
}
```
> `Pawn_NeedsTracker.ShouldHaveNeed(NeedDef)` 是 RimWorld 判定"该 Pawn 是否持有此需求"的入口，返回 false 即跳过实例化。一处 patch 覆盖所有 Pawn。
>
> **回滚**：DBH 关闭 → `UseDBH=false` → patch 不生效 → MEEWater 正常实例化，100% 还原纯 MEE。

---

## 5. 关键交叉点详解

### 5.1 `GetWaterNeed` 的消费方改造清单

全代码库所有 `TryGetNeed<Need_MEE_Water>()` / `DefDatabase...MEEWater` 改为 `WaterAdapter.GetWaterNeed(pawn)`（或 `.GetWaterLevel`）。逐文件：

**主工程（`_indexProj/Source`）**：
| 文件 | 原写法 | 改为 |
|---|---|---|
| `JobDriver_MEE_DrinkFromGround.cs` (L33,38) | `pawn.needs.TryGetNeed<Need_MEE_Water>()` | `WaterAdapter.GetWaterNeed(pawn) as Need_MEE_Water`（MEE 模式才进此 driver，见 §5.3） |
| `JobGiver_SatisfyMetabolic.cs` (L34,72,77) | 字符串 `"MEEWater"` 匹配 + `GetNamedSilentFail("MEE_DrinkFromGround")` | 水分支整体包 `if (WaterAdapter.UseDBH) return null;`（见 §5.3） |
| `Comp_MEE_Satisfier.cs` (L65) | `Props.needDef.defName == "MEEWater"` | 保留（物品 XML 的 needDef 字段仍在；DBH 模式该物品加 `WaterExt` 见 §6） |
| `PhysiqueHealthCard.cs` (L27,127,359,380) | 直接渲染 `MEEWater` 水条 | 改用 `WaterAdapter.GetWaterNeed(pawn)` 取当前水需求渲染（DBH 模式显示 DBH 水位） |
| `NeedChangeEvents.cs` | `OnWaterChanged`/`FireDrinkMEEWater` 定义 | 保留（事件仍由 MEE 物品/DBH 触发，见 §5.6） |
| `RimHormonesMod.cs` (L59,109,141) | `ShowMEEWaterMotes` 设置 | 保留（DBH 模式可改为控制"DBH 水位飘字"开关，语义不变） |

**模块（`_metabolicEssentialsExtendedProj/Source`）**：
| 文件 | 行 | 改为 |
|---|---|---|
| `MetabolicLogic_Sugar.cs` | L128 | `Need_MEE_Water water = pawn.needs.TryGetNeed<Need_MEE_Water>();` → `var water = WaterAdapter.GetWaterNeed(pawn);`（并判 null） |
| `MetabolicLogic_Hediffs.cs` | L46 | 同上，改 `WaterAdapter.GetWaterNeed(pawn)` |
| `MEEMgr.cs` | L76,96,121 | 飘字取数改用 `WaterAdapter.GetWaterLevel(pawn)`，needKey 仍用 `"MEEWater"` 作飘字标签键（见 §5.6） |

### 5.2 从 ground 补水的 thing 到底用哪个（**重点回答**）

**MEE 模式（默认）**
- 自动补水 `JobGiver_SatisfyMetabolic` 优先找 `MEE_WaterBottle` → `MEE_RawWater` 物品做 Ingest（这些 item 带 `Comp_MEE_Satisfier` + `needDef=MEEWater`）。
- 无物品时回落 `MEE_DrinkFromGround` job：走到某水格（TargetIndex.A），`Need_MEE_Water.Drink()` 每 tick +0.006，**不消耗任何 thing**（直接改需求值，池塘/野水视为无限地形资源）。
- 此时"ground 补水的东西"= `MEE_RawWater` 物品 **或** 任意水格地形。

**DBH 模式**
- MEE **完全不主动喝地表水**：
  - `JobGiver_SatisfyMetabolic` 水分支 `if (WaterAdapter.UseDBH) return null;`（§5.3）
  - `MEE_DrinkFromGround` job 不再生成（该 job 仅在 MEE 模式由 JobGiver 派发）。
- 地表喝水**全交 DBH**：
  - `DBHDrinkFromGround`（job driver）喝**地形地表水**（DBH 通过 `IsSurfaceWater`/`TerrainWater`/`GetGroundWaterCapacity` 识别池塘/河/海地形，非物品）；
  - `DBHDrinkFromBasin` 喝**水网设施**（`Building_WaterFeature`，接 DBH 水管网的水池/龙头）。
- 此时"ground 补水的 thing"= **DBH 的地形地表水 + 水网设施**，**与 MEE 无关**。

> ✅ **直接回答**：DBH 在场时，地面补水由 DBH 接管（地形水/水网设施），MEE 的地表喝水逻辑整体停用——不存在"两个 mod 抢同一片池塘"的冲突。MEE 的 `MEE_RawWater` 物品在 DBH 模式不应再作为地面水源（见 §5.4 处理其产出）。

### 5.3 `JobGiver_SatisfyMetabolic` 水分支改造

```csharp
public static Job TryGiveJob(Pawn pawn)
{
    // ... 模块未启用等早退 ...

    if (WaterAdapter.UseDBH)
        return null;   // DBH 是水的唯一真相源，MEE 不派任何补水/地表喝水任务

    // ===== 以下原 MEE 逻辑（仅 MEE 模式执行）=====
    foreach (var pair in new[] {
        ("MEEWater", new[] { "MEE_WaterBottle", "MEE_RawWater" }),
        // ... 其他三需求
    }) {
        // ... 原逻辑
        if (pair.need == "MEEWater")
        {
            // 无物品时回落 MEE_DrinkFromGround（仅 MEE 模式才能到这里）
            JobDef drinkJob = DefDatabase<JobDef>.GetNamedSilentFail("MEE_DrinkFromGround");
            if (drinkJob != null) return JobMaker.MakeJob(drinkJob, ...);
        }
    }
    return null;
}
```

### 5.4 收集器产出在 DBH 模式的归属

`Comp_MEE_WaterCollector` 当前即产即出 `MEE_RawWater`。DBH 模式若不处理，该物品会变成无人喝的垃圾。

二选一（建议 B.1，最稳）：

- **B.1（推荐）**：DBH 模式仍产 `MEE_RawWater`，但给它加 `WaterExt`（`water` 值 + `SeekForThirst=true`，详见 §6），使 **DBH 把它当合法饮水源**承接。MEE 收集器→产出被 DBH 系统认领，小人的 DBH 喝水 AI 自然取用。**无需反射、零脆弱点**。
- **B.2（进阶，可选）**：反射调用附近 `CompWaterStorage.PushWater`（或 `Building_WaterFeature`），把收集到的水直接灌进 DBH 水桶/水塔，收集器不再产物品。需反射 DBH 内部方法，标为后续增强。

### 5.5 体魄缩放属性修改（两模式对照）

`PhysiqueLgc.GetMEEWaterCapacityMult(pawn)` 常量（已存在）：Frail 0.90 / Average 1.00 / Fit 1.15 / Strong 1.30 / Peak 1.45。

| 模式 | 存储（水位下降速度/上限） | 体魄缩放落点 |
|---|---|---|
| **MEE** | `Need_MEE_Water.MaxLevel = GetMEEWaterCapacityMult`（0.90~1.45），`FallPerDay=0.55` | **直接作用于存储**：高体魄容量大、断水撑更久 ✅ |
| **DBH** | DBH 管真实存储（`Need_Thirst` 自掉、容量由 DBH 定） | **作用于阈值/后果层**（不碰 DBH 存储）：<br>• `GetThirstUrgencyThreshold`：高体魄渴得更晚<br>• `GetWaterHazardTolerance`：高体魄水中毒/脱水后果更轻<br>即"高体魄抗风险"语义保留，但体现在 MEE 的减益/飘字判定上 |

> 若日后想要"高体魄真在 DBH 里也存更久"（最强融合），可加 **B.2 增强**：Harmony patch `Need_Thirst` 的 `MaxLevel`/`FallPerDay` getter，乘入 `GetMEEWaterCapacityMult`。这是可选增强，不在本期必做（反射 DBH 内部有版本脆弱性，见 §9）。

### 5.6 事件联动（飘字 / 水中毒 / 水变更）

- **飘字**：`MEEMgr` 的水飘字改用 `WaterAdapter.GetWaterLevel(pawn)` 取水位（DBH 模式显示 DBH 水位涨跌），needKey 仍 `"MEEWater"`（仅作飘字标签，无语义依赖）。
- **水中毒**：`MetabolicLogic_Hediffs.OnDrinkMEEWater` 触发逻辑保留，但 DBH 模式下改为订阅 DBH 喝水事件（反射 `Need_Thirst` 的 level 变更，或 Harmony postfix `Need_Thirst` 的 set_CurLevel），用 `WaterAdapter.GetWaterHazardTolerance(pawn)` 调整中毒概率/强度。
- **水变更事件**：`NeedChangeEvents.FireWaterChanged` 在 DBH 模式下由 DBH 水位变化驱动（同上订阅），保证 MEE 的后续逻辑（如脱水减益）仍能响应。

---

## 6. XML 改动清单

**A. MEE 水物品加 `WaterExt`（DBH 模式被 DBH 认领）** —— 以下 ThingDef 各加 `<modExtensions>`（纯 XML，不动 C#）：
`Thing_MEE_WaterBottle` / `Thing_MEE_RawWater` / `Thing_MEE_LightSaltWater` / `Thing_MEE_MilkTea` / `Thing_MEE_VegFruitJuice` / `Thing_MEE_FunctionalDrink`

示例（`Thing_MEE_WaterBottle.xml`）：
```xml
<modExtensions>
  <li Class="DubsBadHygiene.WaterExt">
    <water>0.5</water>            <!-- 与 satisfyFraction=0.5 对齐：一瓶补 50% -->
    <UseForWashing>false</UseForWashing>
    <SeekForThirst>true</SeekForThirst>
  </li>
</modExtensions>
```
（`water` 值按各自 `satisfyFraction` 设；`MEE_RawWater` 生鲜水建议 `0.5` 或更低，`MEE_FunctionalDrink` 按配方补水量设。）

**B. `About/About.xml`**：`<loadAfters>` 加 `836308268`（DBH packageId），保底 mod 加载顺序。

**C.（可选）`Thing_MEE_RawWater.xml`** 在 DBH 模式：若走 B.1，仅加 `WaterExt` 即可；若走 B.2 进阶，则改 `Comp_MEE_WaterCollector.spawnDefName` 或加 `<modExtensions><MEEBuildingMarker/></...>` 控制显隐（随你后续选择，本期先按 B.1 只加 WaterExt）。

---

## 7. C# 文件改动清单（汇总）

| 文件 | 改动 |
|---|---|
| `Source/Comps/WaterAdapter.cs` | **新增**：适配器核心类（§2） |
| `Source/Comps/DBHDetector.cs` | **已有**：复用，确保 `DBHPresent` 探测正确 |
| `Source/Mods/RimHormonesMod.cs` | 入口显式调用 `WaterAdapter.Init()`（§3.2） |
| `Source/Comps/JobGiver_SatisfyMetabolic.cs` | 水分支加 `if (WaterAdapter.UseDBH) return null;`（§5.3） |
| `Source/Comps/JobDriver_MEE_DrinkFromGround.cs` | `TryGetNeed<Need_MEE_Water>()` → `WaterAdapter.GetWaterNeed(pawn) as Need_MEE_Water`（§5.1） |
| `Source/UI/PhysiqueHealthCard.cs` | 水条渲染改用 `WaterAdapter.GetWaterNeed(pawn)`（§5.1） |
| `Source/Comps/Comp_MEE_Satisfier.cs` | 基本不变（`needDef` 字段照旧；DBH 模式由 WaterExt 驱动 DBH 喝水） |
| `Source/Needs/Need_MEE_Water.cs` | 基本不变（MEE 模式照旧；DBH 模式已被 §4 patch 摘除实例） |
| **Harmony patch 文件（新增）** | `Patch_PawnNeedsTracker_ShouldHaveNeed`：DBH 模式摘除 MEEWater（§4） |
| `MetabolicLogic_Sugar.cs` (L128) | `TryGetNeed<Need_MEE_Water>()` → `WaterAdapter.GetWaterNeed(pawn)` |
| `MetabolicLogic_Hediffs.cs` (L46) | 同上；DBH 模式订阅 DBH 水位事件 + `GetWaterHazardTolerance` |
| `Logic/MEEMgr.cs` (L76,96,121) | 飘字取数改用 `WaterAdapter.GetWaterLevel(pawn)` |

> 所有新增 `.cs` 须在 `Assembly-CSharp.csproj` 与 `MetabolicEssential.csproj` 的 `<Compile Include>` 中登记（`EnableDefaultCompileItems=false`）。

---

## 8. 初始化 / 运行流程（文字图）

```
[启动] 各 mod Def 加载
  └─ DBHDef 注册 "DBHThirst" (若 DBH 在场)
[StaticConstructorOnStartup]
  ├─ DBHDetector.ctor → DBHPresent = (DBHThirst != null)
  └─ RimHormonesMod 入口 → WaterAdapter.Init()
        └─ Active = UseDBH ? DBH : MEE
[Pawn 生成]
  └─ Pawn_NeedsTracker.AddOrRemoveNeeds
        └─ ShouldHaveNeed(MEEWater)
              └─ DBH 模式 → false（不创建 MEEWater 实例）
[每 tick]
  ├─ MEE 模式：Need_MEE_Water.NeedInterval 自掉；JobGiver 派补水/地表喝水
  └─ DBH 模式：MEE 水逻辑全停用；DBH 自己掉水、自己派 DBHDrinkFromGround/Basin
[任意代码取水位]
  └─ WaterAdapter.GetWaterNeed(pawn) → DBH 或 MEE（统一入口）
```

---

## 9. 风险与回滚

| 风险 | 缓解 |
|---|---|
| `ShouldHaveNeed` patch 签名随 RimWorld 版本变 | RimWorld 1.6 该方法稳定；若失效，`grep` 报错即知，回退该 patch 即可 |
| `WaterExt` 字段名/结构随 DBH 版本变 | 本方案基于 DBH 1.6 实测（`water`/`SeekForThirst`/`UseForWashing`）；DBH 升级需复核 |
| 反射订阅 DBH 水位事件脆弱（§5.6 进阶） | 本期飘字/水中毒可先只在 MEE 模式全功能、DBH 模式降级（仅显示 DBH 水位，不触发 MEE 中毒判定），反射订阅标为后续增强 |
| DBH 关闭后残留 | 所有分支以 `WaterAdapter.UseDBH` 守卫，关闭 DBH → 自动 100% 还原纯 MEE |
| mod 加载顺序 | `About/loadAfters` 加 DBH packageId 保底 |

---

## 10. 验收标准

| 场景 | 期望 |
|---|---|
| 仅 MEE | 行为完全等同现状（水条、体魄缩放 0.90~1.45、自动补水、地表喝水均正常） |
| MEE + DBH 同载 | ① 小人**只有 DBH 一条水条**（无 MEEWater 实例）；② 地表/设施喝水由 DBH 驱动，无 `Could not reserve` 冲突；③ MEE 水物品（带 WaterExt）被 DBH 认领为合法饮水源；④ `WaterAdapter.GetWaterNeed` 在调试下确认返回 DBHThirst |
| DBH 模式体魄表现 | 高体魄小人通过 `GetThirstUrgencyThreshold` 更晚觉渴、`GetWaterHazardTolerance` 更抗水中毒（MEE 特色保留） |
| 关闭 DBH | 全功能还原纯 MEE，无任何残留 |

---

## 11. 本期确定 vs 待定

**本期确定做（方案 A 核心）**：§2 WaterAdapter + §3 初始化 + §4 摘除 MEEWater + §5.1/5.2/5.3/5.5 + §6 WaterExt + §7 改造清单。

**待定（需你拍板后做）**：
1. 收集器走 B.1（加 WaterExt）还是 B.2（灌 DBH 水网）？—— 建议 B.1。
2. 飘字/水中毒在 DBH 模式是否做完整联动（需反射订阅 DBH 事件），还是先降级为"仅显示 DBH 水位"？—— 建议先降级、稳定后再增强。
3. 是否加 §5.5 B.2 增强（Harmony 注入体魄倍率到 DBH 存储）？—— 默认不做（版本脆弱）。
