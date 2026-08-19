# MEE 适配 DBH — 覆盖逻辑（逆置版）

> 本文档记录**当前实际实现**：MEE 独家管水、DBH 水系统退场。
> 取代早期「方案 A」（DBH 是水的真相源），早期文档已废弃，见文末。

---

## 一、一句话概括

MEE 模块加载时检测 DBH 是否共存；若共存，**压制 DBH 的水需求 `DBHThirst`**，把 DBH 的「饮水」类 JobDriver **重定向到 MEE 水系统**，并用 MEE 专属「存储水」WorkGiver 取代 DBH 的水瓶物流。全程零硬引用 DBH。

---

## 二、覆盖逻辑总表（DBH JobDriver → MEE）

| DBH JobDriver | 处理 | 重定向后的行为 |
|---|---|---|
| `JobDriver_DrinkFromGround` | 重定向 `Wild` | 喝自然水面**生水**：每 tick `Need_MEE_Water.Drink()`，结束 **20% 食物性中毒**（与 MEE 地形饮水一致） |
| `JobDriver_DrinkFromBasin` | 重定向 `Basin` | 补 `Need_MEE_Water`（水盆视为过滤水，**不中毒**） |
| `JobDriver_AdministerFluids` | 重定向 `Administer` | 给**病患**补 `Need_MEE_Water`（取 `job.targetA` 的 Pawn，取不到回退执行者自身） |
| `JobDriver_StockpileWaterBottles` | 退出（空 toil） | 不再整理 DBH 水瓶；**由 MEE 存储水 WorkGiver 接管**（见 §四） |
| `JobDriver_PackWaterBottle` | 退出（空 toil） | 不再装填 DBH 水瓶 |
| `JobDriver_LoadFridge` | 退出（空 toil） | 不再往冰箱放水瓶 |

---

## 三、压制水需求（单一水条）

- Patch `Verse.Pawn_NeedsTracker.ShouldHaveNeed(NeedDef)` 的 Postfix：
  当 `DBH 已加载 && MetaBolicLoadCtrl.Active && def.defName == "DBHThirst"` 时，`__result = false`。
- 结果：`DBHThirst` 不再实例化，小人需求栏只剩 **MEE 单水条**；DBH 的渴 AI 因需求缺失自动停摆。

---

## 四、存储水（WorkGiver_MEE_StoreWater）

DBH 的 `StockpileWaterBottles` 只认 DBH 水瓶、MEE 接管后不会被派发，改它的 toil 无用。故 MEE 自己提供一个等价物：

- **新类** `Hormones.WorkGiver_MEE_StoreWater`（`_indexProj/Source/Comps/WorkGiver_MEE_StoreWater.cs`，继承 `WorkGiver_Scanner`）：
  - 扫描 `ThingRequestGroup.HaulableEver`，仅处理 `MEE_RawWater` / `MEE_WaterBottle`；
  - 用 `StoreUtility.TryFindBestBetterStoreCellFor(..., StoragePriority.Unstored, ...)` 找储藏格；
  - 派发 `JobDefOf.HaulToCell` 搬运进储藏区；
  - `ShouldSkip` 返回 `!MetaBolicLoadCtrl.Active`（模块关时完全不工作）。
- **新 XML** `Defs/WorkGiverDefs/WorkGiver_MEE_StoreWater.xml`：`workType=Haul`、`priorityInType=200`。
- 这样「存储水」由 MEE 自己的 WorkGiver + MEE 水物品完成，不依赖 DBH 派发。

> 注：MEE 水物品本身已带 `thingCategories=Foods`，原生 Haul 也会存它们；此 WorkGiver 是**专属机制**，语义上对应 `StockpileWaterBottles`。

---

## 五、保留（不 patch）

| DBH 内容 | 原因 |
|---|---|
| `DrainWater` / `RefillWater`（管网供水） | 马桶/淋浴等卫生设施靠它供水，关掉会破坏 plumbing |
| `GoSwimming`（游泳娱乐） | 与渴需求无关，属娱乐 |

---

## 六、机制要点

1. **探测**：`DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst") != null` 判断 DBH 共存（零硬引用）。
2. **零硬引用**：所有 DBH 类型/方法用 `AccessTools.TypeByName` / `AccessTools.Method` 手动定位，定位失败静默跳过，DBH 改名/缺方法不炸本 mod。
3. **运行期闸门**：所有 prefix/postfix 首行校验 `MetaBolicLoadCtrl.Active`，MEE 模块关时完全不干预 DBH。
4. **防 NRE**：对每个 DBH JobDriver 的 `TryMakePreToilReservations` 注入前缀强制返回 true，完全绕过 DBH 预留代码。
5. **统一入口**：`WaterAdapter.GetWaterNeed(pawn)` 返回 `Need_MEE_Water`（逆置版 MEE 是唯一真相源）。

---

## 七、文件清单

| 文件 | 说明 |
|---|---|
| `_metabolicEssentialsExtendedProj/Source/WaterAdapter.cs` | 适配器核心：探测 + 压制 DBHThirst + 6 个饮水 Job 重定向/退出 + GetWaterNeed |
| `_indexProj/Source/Comps/WorkGiver_MEE_StoreWater.cs` | 存储水 WorkGiver（新增） |
| `Defs/WorkGiverDefs/WorkGiver_MEE_StoreWater.xml` | 存储水 WorkGiverDef（新增，workType=Haul） |
| `About/About.xml` | `loadAfter` 加 `Dubwise.DubsBadHygiene`（保 DBH 先加载） |

---

## 八、测试要点

- MEE + DBH 共存：只剩 MEE 单水条；喝 `MEE_RawWater`/地形水；DBH 水瓶不再被生产/整理；马桶淋浴照常供水。
- `DrinkFromGround` 真的补 MEE 水且靠近野水有概率中毒；`DrinkFromBasin` 补 MEE 水不中毒；医护 `AdministerFluids` 给病患补 MEE 水。
- 水箱产出 `MEE_RawWater` 后，小人会把它搬进储藏区（存储水 WorkGiver + 原生 Haul）。
- 吃素菜飘「糖 +X%」；切换体魄后水/糖/电/蛋白容量上限变化。
- 盯日志：若 DBH 在非 JobDriver 处（如某 `CompTick` 读 `DBHThirst`）报 NRE，按需再补 patch。

---

## 废弃文档

- `MEE适配DBH水系统方案.md`（早期方案对比）
- `MEE适配DBH_方案A_WaterAdapter.md`（方案 A：DBH 是真相源——**未采用**，实际落地为本文档逆置版）
