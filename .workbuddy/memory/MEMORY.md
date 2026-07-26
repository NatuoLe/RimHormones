# Rim-Hormones 项目长期记忆

## 部署约定（重要）
- 项目源码：`D:\RimMods\Rim-Hormones\RimHormones\`（含 Source/ Defs/ Patches/ Assembly-CSharp.csproj）。真正编译工程在 `_indexProj/`（dotnet build -c Release，产物 `_indexProj/bin/Release/net48/RimHormones.dll`）。
- 游戏实际读取：`D:\Steam\steamapps\common\RimWorld\Mods\Rim-Hormones\` —— **独立副本，非软链接**。
- 改源码后必须手动把改动文件 cp 到 Steam 目录（XML 改只 cp XML；C# 改需重新编译 DLL 再 cp）。改完**完全重启游戏**才生效。
- `Assemblies/RimHormones.dll` 历史遗留：项目 `Assemblies/` 里有 6/22 旧文件，游戏不读，可清理。

## CE 兼容 = 独立补丁包（双包架构，2026-07-26 定）
- **主 mod（thgold.hormones）已移除内置 CE 软兼容层**（删了 `Source/Compat/CombatExtendedCompat.cs`）。本体只跑原版战斗逻辑。
- **CE 适配拆到独立包**：源码 `D:\RimMods\RimHormonesCE\`，packageId `thgold.hormones.ce`。硬引用 `RimHormones.dll` + `CombatExtended.dll` + Harmony，用强类型 `[HarmonyPatch]` 特性 patch CE 动词，复用本体 public 方法。
  - 编译：`cd D:\RimMods\RimHormonesCE\Source && dotnet build -c Release`，引用 dll 在 `../Refs/`（勿发布），产物 `bin/RimHormonesCE.dll` → cp 到 `Assemblies/`。
  - 发布/部署只需 `About/` + `Assemblies/`，Steam 目标 `D:\Steam\...\Mods\RimHormonesCE\`。
  - CE 关键签名：`Verb_MeleeAttackCE.TryCastShot()`/`Verb_LaunchProjectileCE.TryCastShot()` 都是 public virtual 无参可特性 patch；`Verb_MeleeAttackCE.GetHitChance(LocalTargetInfo)` 是 **private**，需字符串名+参数类型定位。
  - **改本体后若动了 AdrenalineProducer/AdrenalineLogic/HormonesLogic 的 public 签名，需把新 RimHormones.dll cp 到 RimHormonesCE/Refs/ 并重编译补丁包。**
- 参考：Milira CE Patch（workshop 3410567648）是硬引用+LoadFolders 分版本+Transpiler 的范本。

## RimWorld 版本（已核实：1.6.4871 rev590，非 1.3！）
- 实际安装游戏：`D:\Steam\steamapps\common\RimWorld\`，Version.txt = **1.6.4871 rev590**。
- 项目旧记忆写「1.3」已过时；凡涉 XML 字段名/API 均以 1.6 为准。
- **关键重命名（1.3→1.6）**：`HediffStage.capacityMods` 已改名为 **`capMods`**（内层 `<capacity>`/`<offset>` 不变）。旧名会导致 def 加载报错「doesn't correspond to any field in type HediffStage」。
- 其余已核实仍有效：`statFactors`/`statOffsets`/`painFactor`/`minSeverity`/`label` 等 HediffStage 字段。
- 精神状态路由：`category=Misc`/`Undefined` → `MentalStateNonCritical` 子树；`Aggro`/`Malicious` → `MentalStateCritical`。`Wander_OwnRoom` 在 `MentalStateNonCritical`(`SubTrees_Misc.xml`)。
- think tree 节点标签全是 `<li>`，类型靠 `Class="..."` 区分（如 `ThinkNode_ConditionalMentalStates`）。
- `MentalStateCategory` 枚举仅：Undefined / Aggro / Malicious / Misc（无 Bad）。
- `MentalStateDef` 合法字段对齐原版 `Wander_OwnRoom`；本版本**无** `unspawnedMtbDays`/`blocksSocialInteractions`/`stopsJobs`。
- Need 系统：`NeedInterval` 每 150 tick 驱动一次（皮质醇自动增长/衰减根）。
- ThoughtWorker 用 `protected override ThoughtState CurrentStateInternal(Pawn)`（非 ShouldHaveThought）。

## 数值/设计快照
- 皮质醇 ×100 语义，MaxLevel=10000；衰减档 13%/8%/3%，增长含体魄修正。
- 神经衰弱 Hediff：`RestRateMultiplier 0.5`（砍 50% 休息效率），含体魄心情加成。
- 优质睡眠 Hediff：`WorkSpeedGlobal 1.1`（全局效率+10%）+3心情，1天消失。
- 失眠发作：`NeurastheniaInsomnia`(category=Misc, stateClass=MentalState_WanderOwnRoom)，神经衰弱期间每6000tick 5%触发，2小时(5000tick)强制，recoveryMtbDays=-1。
