# Rim-Hormones 项目长期记忆

## 发布信息（Steam）
- 主 mod packageId `Lenatuo.hormones`（作者 Lenatuo，name「边缘体魄＆激素」），工坊 ID 3771856492。CE 补丁包 packageId `thgold.hormones.ce`（依赖本体）。
- 游戏版本 **1.6.4871 rev590**（非 1.3）。关键：`HediffStage.capacityMods`（旧名 capMods 已废弃）；MentalStateCategory 仅 Undefined/Aggro/Malicious/Misc；NeedInterval 每 150 tick；ThoughtWorker 用 `CurrentStateInternal(Pawn)`。

## 部署约定（重要）
- 源码 `D:\RimMods\Rim-Hormones\RimHormones\`；编译工程 `_indexProj/`（dotnet build -c Release → `_indexProj/bin/Release/net48/RimHormones.dll`）。游戏读 `D:\Steam\...\Mods\Rim-Hormones\`（独立副本，非软链接）；改完完全重启游戏。
- `Assembly-CSharp.csproj` 显式编译列表（`EnableDefaultCompileItems=false`）：新建 .cs 必须加 `<Compile Include>`，否则静默不进 DLL（验证 `grep -c "类型名" RimHormones.dll`）。
- 主 DLL 在 `Assemblies/`：`RimHormones.dll` + `0Harmony.dll`。可选模块 `MetabolicEssential.dll` 在 mod 根 **`MetabolicEssential/` 子目录（不进 Assemblies/，否则被自动加载绕过开关）**。
- 模块工程 `_metabolicEssentialsExtendedProj/MetabolicEssential.csproj`（net48，OutputPath=..\MetabolicEssential\，Private=false 引本体 dll）。
- 一键部署 `copyToRimWorld.bat`（mod 根）：build 主→暂存 DLL→build 模块→**防呆**删 Assemblies 残留 MetabolicEssential.dll→xcopy About/Defs/Patches/Languages/Config→拷 DLL 到 Steam Assemblies + 模块到 Steam MetabolicEssential\。顺序不可调（模块编译期引主 DLL）。

## 可选模块 MetabolicEssential（2026-08-09 定案）
- 四个代谢需求 `Need_MEE_Water/Sugar/Electrolytes/Protein`（水/糖/电解质/蛋白）源码在主 mod `_indexProj/Source/Needs/`（命名空间 Hormones），**默认加载、常驻实例化**；代谢机制受模块 DLL 开关控制。
- 标志：`MetaBolicLoadCtrl.Active`（Init 后置 true，驱动 `Need_MEE_Base.NeedInterval` 前置 `if(!Active) return;`）+ `MetaBolicLoadCtrl.IsLoadedMME`（同置位，驱动 `Need_MEE_Base.ShowOnNeedList => IsLoadedMME` → 未启用时四需求隐藏但仍实例化、不消耗、零副作用）。
- 接口解耦：主 mod 只认 `Hormones.IMetabolicModule` 接口，绝不编译引用具体实现；二级 DLL 反向引 RimHormones.dll。
- 加载：勾选「启用 Metabolic Essential」+重启 → `MetaBolicLoadCtrl`([StaticConstructorOnStartup] 静态构造器) 调 `MetaBolicLoadCtrl.TryLoad` → 文件存在且开关开 → `Assembly.LoadFrom(<modRoot>/MetabolicEssential/MetabolicEssential.dll)` → 反射 `IMetabolicModule.Init` → 置 Active/IsLoadedMME。关闭=程序集完全不载入。模块类禁 `[StaticConstructorOnStartup]`/副作用字段初始化器。
- 食谱显隐：主 mod `RecipeDef_AvailableNow_MEE_Patch`(Harmony postfix) 拦 `RecipeDef.AvailableNow`，对带 `MEERecipeMarker` DefModExtension 且 `!IsLoadedMME` 的食谱翻 false（隐藏且不可做）。MEE 食谱 XML 需加 `<modExtensions><li Class="Hormones.MEERecipeMarker"/></modExtensions>`。
- 物品显隐：ThingDef **无** ShowOnNeedList 等价物，仅能靠"无配方则不被制造"间接隐藏；若要彻底隐藏需拆成独立子 mod。当前 MEE 物品（MEE_Salt/MEE_ProteinExtract/MEE_WaterBottle/MEE_GlucoseMash）为常驻 def，未做隐藏。
- 后期拆独立 mod：移工程+About.xml，启用 MetabolicEssentialModule.cs 注释的 Bootstrap。
- 已落地 MEE 食谱链：土豆/玉米在酿造台(Brewery)→葡萄糖原浆(MEE_GlucoseMash)。食谱 `MEE_MakeGlucoseMashFromPotato/FromCorn`：`recipeUsers=Brewery` + `<modExtensions><li Class="Hormones.MEERecipeMarker"/></modExtensions>`（仅模块启用可见）。MEE_GlucoseMash 为 ResourceBase 常驻 def，是「糖」需求的唯一来源（MEE_RawSugar 已移除，葡萄糖原浆即糖）。
- 占位贴图自包含：1.6 原版贴图打包进资源、无松散 PNG 可 copy，故用纯色块 PNG 生成到 `Textures/Things/Item/MEE/`（MEE_Salt/ProteinExtract/WaterBottle/GlucoseMash），4 个 ThingDef 的 texPath 已全部改指本地，不再引用原版路径。生成器 `文档/gen_placeholders.py`（纯 stdlib）。
- 糖↔皮质醇双向联动（2026-08-10）：模块 `_metabolicEssentialsExtendedProj/Source/MetabolicLogic_Sugar.cs` 监听主 mod 的 `NeedChangeEvents`（**事件中心不在 Metabolic 文件夹、已移至主工程根 `Source/NeedChangeEvents.cs`**，属主体通用机制而非代谢特性；静态事件 `OnStrainChanged/OnCortisolChanged/OnSugarChanged`，**public 订阅 / internal 触发**；触发点在 `Need_MuscleStrain`/`Need_Cortisol`/`Need_MEE_Sugar` 的 CurLevel 变化处）。模块**仅通过主 mod 公共接口**回写效果——`Need_Cortisol.SetSugarCortisolModulation(perDayPercent)`（%/日，正=抑制增长/负=催高，独立通道不与饮品拓展的 extraCortisolDecay 冲突）与 `Need_MEE_Base.SetExtraFallPerDay(f)`（叠加糖消耗速率）——**绝不读/写主 mod 私有字段**，满足「避免 Metabolic 字段入侵主工程」。逻辑：皮质醇>20% 时，糖>33%→抑制皮质醇增长10%/日，糖<33%→催高13%/日；皮质醇>20%→糖消耗降为30%/日（基础40%）。`Init` 中 `MetabolicLogic_Sugar.Register()` 订阅。两工程均 0 错编译。
- 文件重组（2026-08-10）：原 `_indexProj/Source/Metabolic/` 下 5 个冗余文件（`IMetabolicModule.cs`/`MetabolicLoader.cs`/`MetabolicBootstrap.cs`/`MetabolicState.cs`/`MEERecipeMarker.cs`）已合并为单个 `Source/Metabolic/MetaBolicLoadCtrl.cs`（[StaticConstructorOnStartup] 单一静态类，合并状态标志+TryLoad 加载+静态构造器启动触发；`IMetabolicModule` 接口与 `MEERecipeMarker` 因 C# 继承约束保留为同文件内的独立类型，模块工程无感）；`NeedChangeEvents.cs` 移出 Metabolic 文件夹至主工程根 `Source/NeedChangeEvents.cs`。csproj 编译清单已同步更新。`RecipeDef_AvailableNow_MEE_Patch.cs` 仍单列保留。

## 姊妹 mod：Function Drinks Expanded
- 源码 `D:\RimMods\Rim-Hormones\Function-Drinks-Expanded\`（独立 git，已发工坊）；packageId `Lenatuo.functiondrinksexpanded`，程序集名仍 `DrinkingwaterIsGood`。依赖 DBH Lite + VCE + Rim-Hormones。
- 本体/外置解耦铁律：饮品对本体机制影响不得硬编码在本体；改机制时在本体 Need/Comp 加 private 字段+public Set/Reset/Get 方法，由外置 Mod 调。四方法：劳损速率/体魄经验/当成锻炼/皮质醇衰减。
- 配方铁律：`ingredientValueGetterClass=Nutrition` 配方禁 0 营养配料（÷0→int.MinValue 报错）。DBH_WaterBottle/VCE_Salt/VCE_RawSugar 营养均 0，固定件配方不写 getter。

## CE 兼容 = 独立补丁包
- 源码 `D:\RimMods\RimHormonesCE\`，packageId `thgold.hormones.ce`，硬引 RimHormones.dll+CombatExtended.dll+Harmony，强类型 `[HarmonyPatch]`。编译 `cd ...\Source && dotnet build -c Release` → `Assemblies/`。
- 改本体 public 签名后须 cp 新 RimHormones.dll 到 CE Refs/ 重编；新触发路径须同步 CE postfix；给 public 方法加参=删旧签名（CE 报 MissingMethodException）→ 用显式重载垫片（注释"勿删"）。

## 工具/速查
- 查 RimWorld 源码/Def 优先 `mcp__rimsage__*`（search_source/read_csharp_symbol/search_defs/get_def_details/list_directory/read_file）。后备 `/tmp/probeproj`（MetadataLoadContext+ICSharpCode.Decompiler）。
- 数值快照：皮质醇×100/MaxLevel=10000；神经衰弱 RestRateMultiplier 0.5；优质睡眠 WorkSpeedGlobal 1.1+3心情；失眠 disappearsAfterTicks=12500(5h)；技能学习倍率 无0.35/好奇1.0/狂热1.5。
