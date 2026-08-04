# Rim-Hormones 项目长期记忆

## 发布信息（Steam 创意工坊）
- **主 mod packageId 实际为 `Lenatuo.hormones`**（作者 Lenatuo，name「边缘体魄＆激素」）；旧记忆里的 `thgold.hormones` 已过时，凡引用本体 packageId 均以 `Lenatuo.hormones` 为准。
- 主 mod 工坊 ID = **3771856492**（https://steamcommunity.com/sharedfiles/filedetails/?id=3771856492）。CE 补丁包 About.xml 里 `Lenatuo.hormones` 依赖已补 `steamWorkshopUrl` 指向此 ID。
- CE 补丁包 packageId `thgold.hormones.ce`（作者 ThGold），依赖本体 `Lenatuo.hormones`。

## 部署约定（重要）
- 项目源码：`D:\RimMods\Rim-Hormones\RimHormones\`（含 Source/ Defs/ Patches/ Assembly-CSharp.csproj）。真正编译工程在 `_indexProj/`（dotnet build -c Release，产物 `_indexProj/bin/Release/net48/RimHormones.dll`）。
- 游戏实际读取：`D:\Steam\steamapps\common\RimWorld\Mods\Rim-Hormones\` —— **独立副本，非软链接**。
- 改源码后必须手动把改动文件 cp 到 Steam 目录（XML 改只 cp XML；C# 改需重新编译 DLL 再 cp）。改完**完全重启游戏**才生效。
- **`_indexProj/Assembly-CSharp.csproj` 是显式编译列表**（`EnableDefaultCompileItems=false`）：**新建 .cs 必须手动加 `<Compile Include>`**，否则静默不进 DLL（编译照样 0 错！XML workerClass 引用该类型时游戏报 ArgumentNullException(type)）。新类验证：`grep -c "类型名" RimHormones.dll`。交叉核对脚本：`C:\Users\zhou\AppData\Local\Temp\check_csproj.py`（2026-08-01 曾因此漏编 PawnColumnWorker_StrainBlock 和皮质醇冒犯/侮辱权重两个功能）。
- `Assemblies/RimHormones.dll` 历史遗留：项目 `Assemblies/` 里有 6/22 旧文件，游戏不读，可清理。

## 姊妹 mod：Function Drinks Expanded（饮品）
- 源码 **`D:\RimMods\Rim-Hormones\Function-Drinks-Expanded\`**（2026-08-04 从 DrinkingwaterIsGood/ 搬来，独立 git 仓库，已发工坊）；packageId `Lenatuo.functiondrinksexpanded`，程序集名仍 `DrinkingwaterIsGood`。
- 部署：根目录 `copyToRimWorld.bat`（build + xcopy 到 `Steam\...\Mods\DrinkingwaterIsGood\`，Steam 侧目录名不变）；xcopy 不清理目标多余文件，删残留要手动。
- 依赖：DBH Lite（水瓶/口渴）+ VCE（盐/糖/巧克力糖浆）+ Rim-Hormones。
- **配方铁律（2026-08-04 int.MinValue 报错换来）**：`ingredientValueGetterClass=Nutrition` 的配方里**不允许出现 0 营养配料**（需求数=count÷营养，÷0=∞→(int)∞=int.MinValue→ThingCount 报错）。DBH_WaterBottle/VCE_Salt/VCE_RawSugar 营养均为 0；此类固定件配料配方不要写 getter（默认 Volume 对非 stuff 按 1/件计）。

## CE 兼容 = 独立补丁包（双包架构，2026-07-26 定）
- **主 mod（thgold.hormones）已移除内置 CE 软兼容层**（删了 `Source/Compat/CombatExtendedCompat.cs`）。本体只跑原版战斗逻辑。
- **CE 适配拆到独立包**：源码 `D:\RimMods\RimHormonesCE\`，packageId `thgold.hormones.ce`。硬引用 `RimHormones.dll` + `CombatExtended.dll` + Harmony，用强类型 `[HarmonyPatch]` 特性 patch CE 动词，复用本体 public 方法。
  - 编译：`cd D:\RimMods\RimHormonesCE\Source && dotnet build -c Release`，引用 dll 在 `../Refs/`（勿发布），产物 `bin/RimHormonesCE.dll` → cp 到 `Assemblies/`。
  - 发布/部署只需 `About/` + `Assemblies/`，Steam 目标 `D:\Steam\...\Mods\RimHormonesCE\`。
  - CE 关键签名：`Verb_MeleeAttackCE.TryCastShot()`/`Verb_LaunchProjectileCE.TryCastShot()` 都是 public virtual 无参可特性 patch；`Verb_MeleeAttackCE.GetHitChance(LocalTargetInfo)` 是 **private**，需字符串名+参数类型定位。
  - **改本体后若动了 AdrenalineProducer/AdrenalineLogic/HormonesLogic 的 public 签名，需把新 RimHormones.dll cp 到 RimHormonesCE/Refs/ 并重编译补丁包。**
  - **功能对齐铁律（2026-08-02 射击失效根因）**：本体新增/调整触发路径（新触发源、门槛、倍率、判定分支）后，**必须同步检查 CE 补丁包的 postfix 是否调用了对应逻辑**——CE 动词完全绕过本体 patch，CE 包漏调用 = 该路径在 CE 下整体失效（实例：CE 远程 postfix 只调 OnAttack 没调 TryApplyOverexertDamage，射击损伤在 CE 下从未触发）。另外本体 const（如概率倍率）会被**编译期内联**进 CE DLL，本体改值 CE 必须重编译。
  - **二进制兼容铁律（2026-08-01 玩家报错换来的教训）**：给已发布的 public 方法**加可选参数 = 删掉旧签名**（C# 可选参数是编译期语法糖，IL 层旧签名不复存在），玩家手里的旧 CE DLL 会抛 `MissingMethodException`（JIT 时报，物种守卫挡不住）。正确做法：**永不动旧签名，新增显式重载做垫片转发**（实例：`AdrenalineLogic.TryApplyOverexertDamage(Pawn)` 单参垫片，注释标"勿删"）。两个显式重载比"可选参数"更安全（无解析歧义）。
- 参考：Milira CE Patch（workshop 3410567648）是硬引用+LoadFolders 分版本+Transpiler 的范本。

## 查游戏源码/Def 的首选工具：rimsage MCP（2026-08-01 用户指定）
- 查 RimWorld 内部机制（源码逻辑、Def XML）**优先用 `mcp__rimsage__*`**：`search_source`(正则搜反编译源码)、`read_csharp_symbol`(读类型/方法体)、`search_defs`+`get_def_details`(查 Def，支持继承合并)、`list_directory`/`read_file`(路径如 `Source/Verse/HediffSet.cs`)。
- 本地后备：`/tmp/probeproj`（MetadataLoadContext 反射 + ICSharpCode.Decompiler 9.1.0.7988 反编译 Assembly-CSharp）。
- 已核实的核心机制速查：死亡判定 `Pawn_HealthTracker.ShouldBeDead`（免死标记→hediff lethalSeverity→lethalFlesh 能力≤minForCapable(默认0)→躯干效率≤0.0001→总伤≥150×HealthScale）；部件摧毁=HP归零自动挂 MissingBodyPart（内部器官 depth=Inside 不触发缺失出血/疼痛）；意识=脑效率−疼痛(≤0.4)，再被泵血/呼吸/过滤低值以 0.2/0.2/0.1 lerp 拖低。

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
- 技能热情学习倍率（1.6 DLL 实测 `SkillRecord.LearnFactorPassion*`）：无=**0.35**（非 wiki 的 0.333！wiki 已过时）、好奇=1.0、狂热=1.5。`SkillRecord.pawn` 是 private，取 pawn 用 public 属性 **`Pawn`**。
