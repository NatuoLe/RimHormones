# MEE 适配 DBH（Dubs Bad Hygiene）水系统 — 方案

> 目标：让 Rim-Hormones 的代谢扩展模块（MEE）在玩家同时装载 **DBH（Dubs Bad Hygiene，1.6 版）** 时，
> 复用 DBH 的水系统，而不是各自跑一套互不相干的"水需求"，避免双重喝水、AI 抢同一瓶水、地表喝水打架。
> 本文档为**设计/实施方案**，落地前请先确认文末「待确认决策」。

---

## 1. 背景与冲突

MEE 自己有一套水需求（`MEEWater` + 收集器 + 自动补水 + 地表喝水）。
DBH 也有一套完整的水系统（`DBHThirst` 需求 + 水管网络 + 井/泵/塔 + 瓶子/水池/地表喝水 AI）。

**两者同时装载时的问题：**

| 冲突点 | 现象 |
|---|---|
| 双水需求并存 | 一个殖民者同时有 `MEEWater` 和 `DBHThirst` 两条水条，都得喂 |
| MEE 自动补水 vs DBH `JobGiver_DrinkWater` | 两套 AI 抢同一瓶水 → 预定冲突（正是之前修过的 `Could not reserve ... stackCount 0` 那类坑） |
| 地表喝水重复 | MEE `MEE_DrinkFromGround` 与 DBH `DBHDrinkFromGround` 都能喝池塘，逻辑冗余 |
| 收集器产出孤立 | MEE 收集器只产 `MEE_RawWater` 物品，不进 DBH 水网，两套经济互不打通 |

**核心原则：DBH 在场时，DBH 是水的"真相源"（single source of truth），MEE 适配它，而非另立中央。**

---

## 2. DBH 水系统拆解（实测 1.6 版）

### 2.1 需求与 AI
- `NeedDef`：**`DBHThirst`**，`needClass=DubsBadHygiene.Need_Thirst`，`baseLevel=0.8`。
  - 关键字段（反编译可见）：`Thirst_Active`、`ThirstRateD`、`ThirstRateMultiplier` —— 即随时间掉落。
- 口渴 AI：`ThinkNode_ConditionalThirst` → `JobGiver_DrinkWater` / `JobGiver_DrinkWaterUrgent`，由 `Harmony_TrySatisfyPawnNeeds` 接入原版进食思考链。

### 2.2 三种喝水途径
1. **喝物品**：摄入带 `WaterExt` 扩展的 Thing（`SeekForThirst=true`），`Ingested` 时按 `<water>` 值补 `DBHThirst`。
   - 例：`DBH_WaterBottle`（分类 `Foods`、`foodType=Fluid`、`preferability=NeverForNutrition`、`<modExtensions><WaterExt><water>1</water><SeekForThirst>true</SeekForThirst></WaterExt></modExtensions>`）。
2. **喝地表**：`DBHDrinkFromGround`（job driver）直接喝池塘/河/海地形。
3. **喝设施**：`DBHDrinkFromBasin`（job driver）喝 `Building_WaterFeature`（接水网的水池/龙头）。

### 2.3 水网（资源级，地图级管网）
- 水源：`CompProperties_WaterInlet`（水井 `WaterWellInlet`/`DeepWaterWellInlet`，按半径产水）。
- 泵送：`CompProperties_WaterPumpingStation`（风泵/电泵/泵站，把井水抽到塔）。
- 管道：`CompProperties_Pipe`（`mode=Sewage` 即水/污水共用管网，纯 XML 连接，无额外组件字段）。
- 储水：`CompProperties_WaterStorage`（`WaterButt` 100 / `WaterTowerS` 8000 / `WaterTowerL` 50000，字段 `WaterStorageCap`）。
- 标记：`BuildWaterExt` modExtension 标记建筑属于水网。

> **互操作关键点**：DBH 识别"可饮水物品"的唯一依据是 `WaterExt` 这个 `DefModExtension`（XML 加即可，无需 C# 引用 DBH 程序集）。

---

## 3. MEE 当前水系统（现状）

| 组件 | 说明 |
|---|---|
| `Need_MEE_Water`（`MEEWater`） | `MaxLevel=GetMEEWaterCapacityMult(pawn)`（随体魄 0.90~1.45），`FallPerDay=0.55`；含 `Drink()` 地表微量补水 |
| `Comp_MEE_Satisfier` | 摄入物品时 `mee.Satisfy(satisfyFraction)` 补对应需求；水物品 `needDef=MEEWater` |
| 水物品 | `MEE_WaterBottle`、`MEE_RawWater`、`MEE_LightSaltWater`、`MEE_MilkTea`、`MEE_VegFruitJuice`、`MEE_FunctionalDrink`（均带 `Comp_MEE_Satisfier` + `MEEWater`） |
| `JobGiver_SatisfyMetabolic` | 水需求低于 0.5 时自动派 Ingest 任务，优先 `MEE_WaterBottle`→`MEE_RawWater`；无物品时回落 `MEE_DrinkFromGround` 喝地表 |
| `Comp_MEE_WaterCollector` | 雨/空气收集，即产即出 `MEE_RawWater`（可改 `spawnDefName`） |
| 事件 | `NeedChangeEvents.FireWaterChanged` / `FireDrinkMEEWater`（供模块飘字、水中毒判定） |

---

## 4. 设计原则

1. **DBH 在场 = DBH 权威**：水的消耗、喝水 AI、地表/设施喝水一律交给 DBH。
2. **MEE 水需求作为"适配器"而非"第二套"**：保留 MEE 水条的存在感与体魄缩放，但不自己掉、不与 DBH 抢水。
3. **可选依赖、零硬引用**：C# 运行时用反射/def 探测 DBH，编译期不引用 `BadHygiene`，DBH 缺失时完全走原 MEE 逻辑。
4. **XML 能解决的不写 C#**：给 MEE 水物品加 `WaterExt` 即可被 DBH 认可，纯 XML。
5. **可回滚**：所有 DBH 分支由单一 `DBHDetector.DBHPresent` 开关，关闭即还原纯 MEE。

---

## 5. 三套方案对比

### 方案 A（推荐）：适配器模式（Mirror / Adapter）
MEE_Water 成为 `DBHThirst` 的**只读适配器**：DBH 管真实水量，MEE 水条跟随，体魄缩放改为作用于"阈值/后果"而非存储本身。
- ✅ 单一真相源，零双重喝水，零 AI 抢水
- ✅ 保留 MEE 水条 UI、体魄抗风险语义、MEE 专属后果（水中毒容限随体魄↑）
- ⚠ MEE_Water 的"独立容量"语义要让渡给 DBH 存储（体魄改为影响后果强度）

### 方案 B（MVP，推荐先做）：仅兼容共存
MEE_Water 仍独立，但：
- 给 MEE 水物品加 `WaterExt`（DBH 认可它们）；
- DBH 在场时**关闭** MEE 自动补水与地表喝水（`JobGiver_SatisfyMetabolic` 水分支返回 null、`MEE_DrinkFromGround` 不注册）；
- 喝水由 DBH AI 统一驱动，一瓶水同时带 `WaterExt`+`Comp_MEE_Satisfier`，一次喝两系统都涨。
- ⚠ 两条水需求各自掉 → 实际"需水量翻倍"（喝一次同时补两个，但掉落也叠加）；作为第一阶段可接受，后面再升方案 A。

### 方案 C（激进）：完全合并
DBH 在场时直接移除 `MEEWater` 需求，所有 MEE 水相关效果（水中毒、飘字）改用 Harmony 挂到 `Need_Thirst`。
- ✅ 最干净
- ❌ 改动最大、丢失 MEE 水需求身份与体魄容量特色，回滚难

**结论：先 B（快速可玩），再演进到 A（干净）。下文按"B→A"两阶段给实现细节。**

---

## 6. 推荐实现细节（阶段 B 先落地）

### 6.1 DBH 运行时探测（新增 `DBHDetector.cs`）
```csharp
[StaticConstructorOnStartup]
public static class DBHDetector
{
    public static readonly bool DBHPresent;
    static DBHDetector()
    {
        // DBH 加载后才会注册该 NeedDef；不引用 DBH 程序集，纯 def 探测
        DBHPresent = DefDatabase<NeedDef>.GetNamed("DBHThirst", false) != null;
    }
}
```
> 备选更稳：同时探测 `AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "BadHygiene")`。

### 6.2 MEE 水物品加 `WaterExt`（纯 XML，逐个 ThingDef 加 modExtensions）
以 `Thing_MEE_WaterBottle.xml` 为例，在 `<comps>` 同级加：
```xml
<modExtensions>
  <li Class="DubsBadHygiene.WaterExt">
    <water>0.5</water>          <!-- 与 satisfyFraction 对齐：一瓶补 50% -->
    <UseForWashing>false</UseForWashing>
    <SeekForThirst>true</SeekForThirst>
  </li>
</modExtensions>
```
对 `MEE_WaterBottle`、`MEE_RawWater`、各 MEE 饮料同样处理（`water` 值按各自补给比例设）。
→ DBH 会把它们视为合法饮水源，其 `JobGiver_DrinkWater` 自然抓取。

### 6.3 关闭 MEE 的水自动逻辑（DBH 在场时）
- `JobGiver_SatisfyMetabolic.TryGiveJob`：进入循环前 `if (DBHDetector.DBHPresent) return null;`（整节点失效，DBH 接管喝水）。
- `MEE_DrinkFromGround` job / `JobDriver_MEE_DrinkFromGround`：DBH 在场时不注册（在 `HarmonyPatches` 或静态注册处用 `if (DBHDetector.DBHPresent) return;` 包住）。
- `Need_MEE_Water.Drink()` 地表补水分支：DBH 在场时 `NeedInterval` 早退，交给 DBH 地表喝水。

### 6.4 收集器（可选，阶段 B+）
DBH 在场时，`Comp_MEE_WaterCollector` 的产出可改为**同时/改向**喂 DBH 水网：
- **B.1（保守）**：仍产 `MEE_RawWater`，但因其已带 `WaterExt`，可被 DBH 系统使用；
- **B.2（打通水网）**：反射调用附近 `CompWaterStorage` 的加水接口，把收集到的水直接灌进 DBH 水桶/水塔（需反射读 `CompProperties_WaterStorage` 实例与加水方法，建议封装在 `DBHWaterBridge.cs`）。

---

## 7. 演进到方案 A（适配器，阶段二）

### 7.1 `Need_MEE_Water` 改为只读适配器
```csharp
public override float MaxLevel => DBHDetector.DBHPresent
    ? 1f                                   // 存储以 DBH 为准
    : PhysiqueLgc.GetMEEWaterCapacityMult(pawn);

public override void NeedInterval()
{
    if (DBHDetector.DBHPresent) return;    // 不自掉，DBH 管真实水量
    base.NeedInterval();
}

// DBH 在场时 CurLevel 跟随 DBHThirst
public float AdaptedCurLevel
{
    get
    {
        if (!DBHDetector.DBHPresent) return CurLevel;
        Need dbh = pawn.needs.TryGetNeed(DefDatabase<NeedDef>.GetNamed("DBHThirst"));
        return dbh != null ? dbh.CurLevelPercentage : 1f;
    }
}
```
- 体魄缩放改为作用于 **MEE 专属后果阈值**：例如水中毒容限、脱水减益触发点，按 `GetMEEWaterCapacityMult` 放大（高体魄更抗风险），而存储跟随 DBH。
- UI（健康卡水条、飘字）读 `AdaptedCurLevel`，玩家无感切换。

### 7.2 事件联动
- `FireWaterChanged` / `FireDrinkMEEWater` 在 DBH 模式下改为由 DBH 口渴变化驱动（反射订阅 `Need_Thirst` 的 level 变更，或 Harmony postfix `Need_Thirst.set_CurLevel`）。

### 7.3 模块侧（MetabolicEssential）
MEEMgr 中水相关 `Satisfy`/`OnDietEaten` 水分支在 DBH 模式改走"读 DBHThirst"路径，避免重复补水。

---

## 8. 实施阶段与验收

| 阶段 | 内容 | 验收 |
|---|---|---|
| **B（MVP）** | `DBHDetector` + 水物品加 `WaterExt` + 关闭 MEE 自动补水/地表喝水 | 双 mod 同载：小人只被 DBH 叫去喝水；MEE 水条仍显示（跟随原生或独立）；无 `Could not reserve` 报错 |
| **B+** | 收集器可灌 DBH 水网（`DBHWaterBridge`） | 放 MEE 收集器→DBH 水桶水位上升 |
| **A** | `Need_MEE_Water` 适配器 + 体魄后果缩放 + 事件联动 | DBH 在场时 MEE 水条=DBH 口渴的体魄增强视图；体魄高者水中毒容限更高 |

---

## 9. 风险与回滚

- **`WaterExt` 字段名/结构随 DBH 版本变**：DBH 升级需复核 `water`/`SeekForThirst` 字段（本方案基于 1.6 实测）。
- **反射调用 DBH 内部加水方法脆弱**：B+ 阶段改用"产出带 `WaterExt` 的物品让 DBH 自己收"更稳，反射灌网作为进阶。
- **DBH 关闭后残留**：所有分支以 `DBHDetector.DBHPresent` 守卫，关闭 DBH 即 100% 还原纯 MEE 行为。
- **调试开关**：在设置里加 `Settings.UseDBHWaterAdapter`（默认跟随探测），便于用户手动关。

---

## 10. 待确认决策（请回复）

1. **走哪条路线**：先 B（仅兼容）还是直接上 A（适配器）？（推荐 B 起步）
2. **收集器是否接 DBH 水网**：只产出兼容物品（B.1），还是主动灌水塔（B.2/A）？
3. **体魄缩放保留方式**：A 模式下接受"存储跟 DBH、体魄只影响后果"，还是坚持"MEE 自有容量"（那只能选 B 不升 A）？
4. **MEE 水条在 DBH 模式是否仍显示**：跟随 DBH 显示（适配器），还是直接隐藏 MEE 水条、只用 DBH 的？

> 确认后我再拆成代码任务（task list）逐步落地，并按 MEE设计.xlsx 的「问题/配方」同步更新。
