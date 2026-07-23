# Rim-Hormones 项目长期记忆

## 部署约定（重要）
- 项目源码：`D:\RimMods\Rim-Hormones\RimHormones\`（含 Source/ Defs/ Patches/ Assembly-CSharp.csproj）。
- 游戏实际读取：`D:\Steam\steamapps\common\RimWorld\Mods\Rim-Hormones\` —— **独立副本，非软链接**。
- 改源码后必须手动把改动文件 cp 到 Steam 目录（XML 改只 cp XML；C# 改需重新编译 DLL 再 cp）。改完**完全重启游戏**才生效。
- `Assemblies/RimHormones.dll` 历史遗留：项目 `Assemblies/` 里有 6/22 旧文件，游戏不读，可清理。

## RimWorld 关键技术点（1.3 / 本版本核实）
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
