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
- 标志：`MetabolicState.Active`（loader Init 后置 true，驱动 `Need_MEE_Base.NeedInterval` 前置 `if(!Active) return;`）+ `MetabolicState.IsLoadedMME`（同置位，驱动 `Need_MEE_Base.ShowOnNeedList => IsLoadedMME` → 未启用时四需求隐藏但仍实例化、不消耗、零副作用）。
- 接口解耦：主 mod 只认 `Hormones.IMetabolicModule` 接口，绝不编译引用具体实现；二级 DLL 反向引 RimHormones.dll。
- 加载：勾选「启用 Metabolic Essential」+重启 → `MetabolicBootstrap`([StaticConstructorOnStartup]) 调 `MetabolicLoader.TryLoad` → 文件存在且开关开 → `Assembly.LoadFrom(<modRoot>/MetabolicEssential/MetabolicEssential.dll)` → 反射 `IMetabolicModule.Init` → 置 Active/IsLoadedMME。关闭=程序集完全不载入。模块类禁 `[StaticConstructorOnStartup]`/副作用字段初始化器。
- 食谱显隐：主 mod `RecipeDef_AvailableNow_MEE_Patch`(Harmony postfix) 拦 `RecipeDef.AvailableNow`，对带 `MEERecipeMarker` DefModExtension 且 `!IsLoadedMME` 的食谱翻 false（隐藏且不可做）。MEE 食谱 XML 需加 `<modExtensions><li Class="Hormones.MEERecipeMarker"/></modExtensions>`。
- 物品显隐：ThingDef **无** ShowOnNeedList 等价物，仅能靠"无配方则不被制造"间接隐藏；若要彻底隐藏需拆成独立子 mod。当前 MEE 物品（MEE_RawSugar/MEE_Salt/MEE_ProteinExtract/MEE_WaterBottle）为常驻 def，未做隐藏。
- 后期拆独立 mod：移工程+About.xml，启用 MetabolicEssentialModule.cs 注释的 Bootstrap。

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
