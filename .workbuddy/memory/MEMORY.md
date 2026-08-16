# Rim-Hormones 项目长期记忆

## 基本信息
- 主 mod packageId `Lenatuo.hormones`（「边缘体魄＆激素」，作者 Lenatuo），工坊 3771856492。CE 补丁 `thgold.hormones.ce`。姊妹 mod Function Drinks Expanded `Lenatuo.functiondrinksexpanded`。
- 游戏版本 1.6.4871 rev590。API：HediffStage.capacityMods；NeedInterval 每150 tick；ThoughtWorker 用 CurrentStateInternal(Pawn)。
- 数值：皮质醇×100/MaxLevel=10000；神经衰弱 RestRateMultiplier 0.5；优质睡眠 WorkSpeedGlobal 1.1+3心情；失眠 disappearsAfterTicks=12500。

## 部署约定
- **JobGiver 生成 Ingest 任务铁律**：地图上找补剂/食物必须检查可预定性 `t.Map.reservationManager.CanReserveStack(pawn, t, 10) > 0`（maxPawns=10 与 JobDriver_Ingest 一致），否则堆叠被预定完时 JobDriver_Ingest 用 `FoodUtility.GetMaxAmountToPickup` 返回 0 作 stackCount → 刷 "Could not reserve ... stackCount 0" 报错（原版 JobGiver_GetFood 会先检查所以不踩坑）。JobDefOf 无 Sleep 字段，睡眠 job def 是 `JobDefOf.LayDown`。
- 源码 `D:\RimMods\Rim-Hormones\RimHormones\`；主工程 `_indexProj/`→`Assemblies/RimHormones.dll`；模块 `_metabolicEssentialsExtendedProj/MetabolicEssential.csproj`→`MetabolicEssential/MetabolicEssential.dll`（mod 根子目录，放 Assemblies/ 会被自动加载绕过开关）。
- csproj `EnableDefaultCompileItems=false`：新 .cs 必须 `<Compile Include>`，否则静默不编（验证 `grep -c "类名" RimHormones.dll`）。
- **本机可直接编译验证**：`dotnet 9.0.313` 下 `dotnet build Assembly-CSharp.csproj -c Release` 可成功；产物 `bin/Release/net48/RimHormones.dll`。模块同理。改完 C# 应本地构建验证，不必等用户贴报错。
- **增量编译缓存坑（假报错）**：`_indexProj/bin`、`obj` 陈旧时监视器报"已修复过的错"。遇此 `rm -rf _indexProj/bin _indexProj/obj` 强制全量重编。
- **部署锁坑**：RimWorld 运行时锁 `Assemblies/RimHormones.dll`，copy 会 `WinError 1224` 静默跳过→DLL 仍是旧版。**只有完全退出游戏才能覆盖**。deploy.py 的 `WARN ... NOT updated (WinError 1224)` 可能是假阳性（锁检测在真正复制前），复制后须用 md5 比对确认真实状态，且内存里跑的仍是旧 DLL，必须完全重启游戏才生效。
- 沙箱调起 python：`D:/RimMods/deploy.py`（带盘符正斜杠）；python 用 `/c/Users/zhou/.workbuddy/binaries/python/versions/3.13.12/python.exe`。纯 XML 改动直接 `cp -r 源/Defs/. 目标/Defs/`。
- **建筑(ThingComp)被动 tick 铁律**：Building 默认进 Rare tick 列表，只回调 `CompTickRare()`，`CompTick()` 永不被调用。持续累积逻辑须 override `CompTickRare()`（250 tick 缩放）；双保险：同时 override `CompTick`(elapsed=1) 与 `CompTickRare`(elapsed=250) 共用 `Accumulate(int)`，XML 写 `<tickerType>Normal</tickerType>`。
- 禁止建筑重叠：靠原版 `isEdifice`(默认 true) + `<canBuildNonEdificesUnder>false</canBuildNonEdificesUnder>`（见 Thing_MEE_MoistureCollector.xml 注释）。自定义 `PlaceWorker_MEE_NotOnBuildings` 已删——原版 `CanPlaceBlueprintOver` 已拦重叠，自写类冗余且陈旧 DLL 会幽灵报错。

## 可选模块 MetabolicEssential（解耦铁律）
- 四需求 Need_MEE_Water/Sugar/Electrolytes/Protein 源码在主 mod `_indexProj/Source/Needs/`，受模块开关控。
- MetaBolicLoadCtrl.cs（[StaticConstructorOnStartup]）：Active（Need_MEE_Base.NeedInterval 早退）+ IsLoadedMME（ShowOnNeedList）。TryLoad→Assembly.LoadFrom→反射 IMetabolicModule.Init。模块类禁 [StaticConstructorOnStartup]/副作用字段初始化器。
- NeedChangeEvents.cs：public 订阅/internal 触发 OnStrainChanged/OnCortisolChanged/OnSugarChanged/OnWaterChanged/OnElectrolytesChanged/OnProteinChanged/OnDrinkMEEWater/OnSugarEaten/OnPhysiqueLevelChanged。
- MEE 需求飘字=模块内 `MEEMgr.cs`（订阅四个 OnXxxChanged，`MoteMaker.ThrowText` 抛 ±pct%+NeedDef 标签，受 `ShowMEEMotes`/`ShowMEE*Motes`/`MetaBolicLoadCtrl.Active` 控制）。真实 NeedDef：`MEEWater/MEESugar/MEEElectrolytes/MEEProtein`。
- 解耦：外置 mod 影响本体须经本体 public Set/Reset/Get，绝不读写私有字段。

## XML 写法铁律（踩坑）
- **LoadDataFromXmlCustom 类型 List 项必须简写**（元素名=key、内容=value），**禁 `<li>`**：StatModifier(statBases/statOffsets/statFactors)、ThingDefCountClass(products/costList)、SkillRequirement、SkillGain/DamageFactor/MemeWeight/BackstoryTrait/XenotypeChance/TraitRequirement/DefHyperlink/MTBByBiome/Aptitude。写 `<li><stat>X</stat><value>N</value></li>`→StatModifier 拿"li"当 stat、FirstChild.Value=null→ArgumentNullException(s)（栈含 ParseHelper.ParseFloat+XX.LoadDataFromXmlCustom 即此坑）。
- 普通复杂类型**必须 `<li>`**：HediffStage.capMods、RecipeDef.ingredients、comps、recipeUsers。`<billInsertion>` **非** BuildingProperties 字段（报 XML 错）。
- **Comp 必须写属性类而非运行时类**：`CompPowerTrader` 是运行时类，正确写 `<li Class="CompProperties_Power"><compClass>CompPowerTrader</compClass><basePowerConsumption>200</basePowerConsumption></li>`（`powerConsumption` 错；`basePowerConsumption` 是私有字段但 XML 可正常读，照搬原版 Cooler）。
- 原版 defName 须 rimsage search_defs 核实：玉米=RawCorn、土豆=RawPotatoes、鸡蛋=EggChickenUnfertilized、生肉=MeatRaw、石块=StoneBlocks、牛奶=Milk、切石机=TableStonecutter、炉灶=ElectricStove/FueledStove、酿造台=Brewery。合法技能仅 12（无 Stonecutting，切石用 Crafting）。
- ThingDef nutrition 是 stat：`<statBases><Nutrition>0</Nutrition></statBases>`；Nutrition==0 时 preferability=NeverForNutrition。液体 FoodTypeFlags 用 Fluid(=4) 非 Liquid；不能为 None。
- 物品进存储区必须带 `<thingCategories>`；可食用归 `Foods`，非食用代谢资源归 `MEE_Ingredients`（自建 ThingCategoryDef 挂 ResourcesRaw 下）。
- 制作台建筑：`thingClass=Building_WorkTable`，加 `<inspectorTabs><li>ITab_Bills</li></inspectorTabs>`，并显式绑定 `<WorkGiverDef>(<giverClass>WorkGiver_DoBill</giverClass>+<workType>+<fixedBillGiverDefs><li>建筑</li></fixedBillGiverDefs>)`，否则报 "Can't find a WorkGiver for a BillGiver"。
- 模块关则建筑/配方隐藏：`MEEBuildingMarker`(DefModExtension) 在 `!IsLoadedMME` 时把带标记建筑 `designationCategory` 置 null；`MEERecipeMarker` 配 RecipeDef.AvailableNow 的 Harmony Postfix 隐藏配方。
