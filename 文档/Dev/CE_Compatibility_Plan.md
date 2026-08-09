# Combat Extended 兼容方案：体魄 + 肾上腺素

> 目标游戏：RimWorld 1.6.4871 ｜ CE 版本：workshop 2890901044（`CombatExtended.dll` 1300KB，纯 DLL 无源码）
> 结论基于对 `CombatExtended.dll` 的反射解析（类继承 + 方法声明表）。

---

## 一、CE 到底重写了什么（反射实证）

| CE 类 | 继承自 | 含义 |
|---|---|---|
| `Verb_MeleeAttackCE` | **`RimWorld.Verb_MeleeAttack`（原版）** | 近战仍走原版基类，但重写了 `TryCastShot`、新增 `GetHitChance`/`GetDodgeChance` |
| `Verb_LaunchProjectileCE` | **`Verse.Verb`（不是原版 Verb_LaunchProjectile！）** | 远程完全脱离原版投射物体系 |
| `Verb_ShootCE` | `Verb_LaunchProjectileCE` | 所有枪械射击 |
| `ProjectileCE` | `Verse.ThingWithComps` | 自定义弹道，伤害经 `Impact()` → `ArmorUtilityCE` |
| `ArmorUtilityCE` | — | CE 自己的护甲/伤害管线（`GetAfterArmorDamage`） |

CE **没有** 替换 `WorkSpeedGlobal` / `MoveSpeed` / `MeleeDamageFactor` / `MeleeDodgeChance` / `MeleeHitChance` 这些 StatDef——它另开了 `CE_StatDefOf`（Bulk/Recoil/AimingAccuracy 等），原版 Stat 依然存在。

---

## 二、我方 9 个 Harmony patch 的存活判定

| # | 我方 Patch（挂载目标） | CE 下状态 | 原因 |
|---|---|---|---|
| 1 | `StatWorker.GetValue`（WorkSpeed/MoveSpeed/MeleeDamage/Dodge/MeleeHit） | ✅ **完全存活** | CE 不动 StatWorker.GetValue，也不删这些 StatDef。走 postfix 照样生效 |
| 2 | `Need_Food.MaxLevel` getter | ✅ 存活 | CE 不改饥饿系统 |
| 3 | `Need_Food.FoodFallPerTickAssumingCategory`（×2：体魄+肾上腺代谢） | ✅ 存活 | 同上 |
| 4 | `Pawn.Tick`（肾上腺被动增益/衰减） | ✅ 存活 | CE 不阻断 Pawn.Tick |
| 5 | `Thing.TakeDamage` postfix（挨打→加肾上腺 + 皮质醇削减） | ⚠️ **部分存活** | 原版 `Thing.TakeDamage` 仍是伤害最终落点，CE 也走它。但 CE 弹道伤害是 `ProjectileCE.Impact` 里手动构造 dinfo 再走护甲管线，**可能** 在个别路径不经过 `Thing.TakeDamage`。近战伤害仍进 TakeDamage |
| 6 | `Verb_MeleeAttack.GetNonMissChance` postfix（体魄命中加成 + 应激命中惩罚） | ❌ **失效（近战）** | CE 的 `Verb_MeleeAttackCE` **重写了命中判定**，改用自己的 `GetHitChance`/`GetDodgeChance`，**不再调用** 原版 `GetNonMissChance`。这个 postfix 在 CE 下形同虚设 |
| 7 | `Verb_MeleeAttack.TryCastShot` postfix（近战→加肾上腺 + 高浓度透支） | ✅ **存活** | `Verb_MeleeAttackCE : Verb_MeleeAttack` 且它 `override TryCastShot()` —— Harmony 打在基类方法上，**对子类 override 无效**！❌ 实际失效 |
| 8 | `Verb_LaunchProjectile.TryCastShot` postfix（远程→加肾上腺） | ❌ **完全失效** | CE 远程走 `Verb_LaunchProjectileCE : Verse.Verb`，**根本不继承** `Verb_LaunchProjectile`。这个 patch 永远不触发 |

> ⚠️ 修正 #7：Harmony patch 打在 `Verb_MeleeAttack.TryCastShot`。C# 虚方法派发下，`Verb_MeleeAttackCE` 有自己的 `override TryCastShot()`，运行时调用的是子类版本，**基类方法体不会执行** → 我方 postfix 不触发。**判定：失效。**

### 存活率总结
- **体魄的工作/代谢/移动/近战伤害/闪避加成**：✅ 全部存活（走 StatWorker）
- **肾上腺素的 Stat 效果（移动/近战伤害/闪避/命中/代谢）**：✅ 存活（走 StatWorker + Need_Food）
- **肾上腺素的"产生"（挨打/近战/远程触发增益）**：❌ 近战失效、远程失效、挨打部分失效 —— **这是最大问题：CE 下肾上腺素几乎涨不起来**
- **体魄的近战命中加成 + 应激命中惩罚**：❌ 近战失效（CE 自己算命中）

---

## 三、适配方案（按优先级）

### 方案 A —— 最小侵入，只补"肾上腺素产生"入口（推荐先做）

肾上腺素 Stat 效果本身在 CE 下都活着，问题只在"涨不起来"。把三个失效的产生入口重新挂到 CE 的正确方法上：

| 失效入口 | CE 下改挂到 | 做法 |
|---|---|---|
| 近战攻击触发（原 `Verb_MeleeAttack.TryCastShot`） | `CombatExtended.Verb_MeleeAttackCE.TryCastShot` | 用 `AccessTools.Method("CombatExtended.Verb_MeleeAttackCE:TryCastShot")` 动态 patch |
| 远程攻击触发（原 `Verb_LaunchProjectile.TryCastShot`） | `CombatExtended.Verb_LaunchProjectileCE.TryCastShot` | 同上（远程若最终改 0，可不接） |
| 挨打触发（`Thing.TakeDamage`） | 保留原 patch + 补 `ArmorUtilityCE.GetAfterArmorDamage` 兜底 | 双保险，避免 CE 弹道漏掉 |

**实现要点（条件 patch，CE 不存在时自动跳过）**：
```csharp
static void PatchCECompat(Harmony h) {
    var ceMelee = AccessTools.TypeByName("CombatExtended.Verb_MeleeAttackCE");
    if (ceMelee != null) {
        var m = AccessTools.Method(ceMelee, "TryCastShot");
        h.Patch(m, postfix: new HarmonyMethod(typeof(CE_MeleeTryCastShot_Patch), nameof(CE_MeleeTryCastShot_Patch.Postfix)));
    }
    var ceRanged = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
    if (ceRanged != null) {
        var m = AccessTools.Method(ceRanged, "TryCastShot");
        h.Patch(m, postfix: new HarmonyMethod(typeof(CE_RangedTryCastShot_Patch), nameof(CE_RangedTryCastShot_Patch.Postfix)));
    }
}
```
- 复用现有 `AdrenalineProducer.OnAttack(pawn, isMelee)` / `OnHit(pawn)`，逻辑零改动，只换触发点。
- 挂 postfix 后，`__instance.CasterPawn` 拿攻击者，跟现有 vanilla patch 完全一致。

### 方案 B —— 让体魄命中加成在 CE 下也生效（可选，工作量中）

CE 近战命中不走 `GetNonMissChance`，改用 `Verb_MeleeAttackCE.GetHitChance(LocalTargetInfo)` / `GetDodgeChance(Pawn)`。若要让"体魄命中加成 + 应激命中惩罚"在 CE 下复活：
- postfix patch `Verb_MeleeAttackCE.GetHitChance`，对 `ref float __result` 施加同样的 `ApplyPhysiqueCombatBonus` + `ApplyHormonesCombatPenalty`。
- 注意：CE 的命中是"部位命中率"，量纲和原版 hitChance 一致（0~1），直接乘无副作用。

### 方案 C —— 不做兼容（不推荐）
现状：装了 CE 后，肾上腺素基本不涨（只剩战区被动增益那点），体魄近战命中加成失效。玩法体验大幅缩水。

---

## 四、落地清单（若你确认走 A+B）

1. 新建 `Source/Compat/CombatExtendedCompat.cs`：
   - `[StaticConstructorOnStartup]` 或在现有 `HarmonyPatches` 静态构造里检测 `ModsConfig.IsActive("CETeam.CombatExtended")` 后调用 `PatchCECompat`。
   - 4 个 postfix 包装类：CE 近战 TryCastShot、CE 远程 TryCastShot、CE `GetHitChance`（体魄）、可选 `ArmorUtilityCE.GetAfterArmorDamage`（挨打兜底）。
2. `Assembly-CSharp.csproj` 增加该 `.cs` 引用（**不要**引用 CombatExtended.dll，全部走 `AccessTools` 反射，保证不装 CE 也能编译运行）。
3. 现有 8 个 vanilla patch **全部保留**（不装 CE 时它们才是主力；装了 CE，失效的那几个自然不触发，不冲突）。
4. 编译 → 关游戏 → 部署 DLL → 开 CE 存档验证：pawn 近战/挨打后肾上腺素正常上涨、健康面板归因正确。

---

## 五、关键风险 & 注意

- **加载顺序**：Rim-Hormones 必须在 CE **之后** 加载，否则 `AccessTools.TypeByName("CombatExtended...")` 拿不到类型。About.xml 里加 `<loadAfter><li>CETeam.CombatExtended</li></loadAfter>`。
- **不硬引用 CE.dll**：全程 `AccessTools` 反射 + 条件 patch，卸载 CE 时 mod 不崩、编译不依赖 CE。
- **弹道伤害路径**：CE `ProjectileCE.Impact` 是否 100% 经过 `Thing.TakeDamage` 需进游戏实测；不确定就同时接 `ArmorUtilityCE.GetAfterArmorDamage` 做挨打兜底。
- **透支伤害**（高浓度近战）现挂在失效的 vanilla TryCastShot 里，迁到 CE TryCastShot postfix 后一并恢复。
