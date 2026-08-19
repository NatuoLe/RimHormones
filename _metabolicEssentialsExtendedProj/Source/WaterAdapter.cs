using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using Hormones;

namespace MetabolicEssential
{
    /// <summary>
    /// 水需求适配器（Water Adapter）。
    /// 由 Metabolic Essential 模块在 Init 时调用 WaterAdapter.Init() 触发——即“MEE 加载时”才会执行。
    /// 职责（逆置版：MEE 独家管水，DBH 退场）：
    ///  1. 检测 DBH（Dubs Bad Hygiene，packageId 836308268）是否共存；
    ///  2. 若共存，压制 DBH 自己的水需求（DBHThirst），让 Need_MEE_Water 成为唯一水需求权威；
    ///  3. 将 DBH 的“饮水”类 JobDriver **重定向到 MEE 水系统**（真正补 Need_MEE_Water，而非无操作跳过）：
    ///       - DrinkFromGround  → 喝自然水面“生水”（每 tick Need_MEE_Water.Drink()，结束 20% 食物性中毒，与 MEE 地形饮水一致）；
    ///       - DrinkFromBasin   → 补 Need_MEE_Water（ Basin 视为过滤水，不中毒）；
    ///       - AdministerFluids → 给病患补 Need_MEE_Water；
    ///     DBH 的“水瓶物流”类 Job（StockpileWaterBottles / PackWaterBottle / LoadFridge）管理的是 DBH 专属水瓶物品。
    ///     StockpileWaterBottles（管网灌装）被完全覆盖：MEE 自建 toils，从设施 CompPipe 拿到 PlumbingNet，
    ///     调用 DBH 的 PlumbingNet.PullWater 真实扣管网水，按 0.5L/瓶逐瓶产出 MEE_WaterBottle（仿 DBH 一瓶一瓶、带耗时）。
    ///     产瓶数尊重 job.count（DBH 右键 / FDE WorkGiver 设定的请求量），并受管网实际水量与单趟安全上限（50 瓶）约束。
    ///     不调用 DBH 原版 MakeNewToils，因此绝不会产生 DBH_WaterBottle；扣水/单位/配额全部由 MEE 掌控。
    ///     其余水瓶物流 Job（PackWaterBottle / LoadFridge）退出（Suppress），MEE 不使用 DBH 水瓶囤积。
    ///
    /// 设计要点：
    ///  - 零硬引用 DBH：仅通过 DefDatabase.GetNamedSilentFail("DBHThirst") 探测，DBH 不存在时完全不生效。
    ///  - 用 AccessTools.TypeByName/Method 手动定位 DBH 内部类型/方法并 Patch，不依赖 [HarmonyPatch] 标注，
    ///    避免被模块 Init 的 PatchAll 重复注册；定位失败则静默跳过（绝不因 DBH 改名/缺方法而炸本 mod）。
    ///  - 运行期以 MetaBolicLoadCtrl.Active 为闸：MEE 模块关时完全不干预 DBH，DBH 自行完整运行。
    /// </summary>
    public static class WaterAdapter
    {
        /// <summary>DBH 水需求的 NeedDef defName（仅用于探测，不引用其程序集）。</summary>
        private const string DBHThirstDefName = "DBHThirst";

        private static bool _initialized;
        private static bool? _dbhPresent;
        public static bool DBHPresent
        {
            get
            {
                if (!_dbhPresent.HasValue)
                    _dbhPresent = DefDatabase<NeedDef>.GetNamedSilentFail(DBHThirstDefName) != null;
                return _dbhPresent.Value;
            }
        }

    /// <summary>DBH 饮水 Job 的重定向模式。</summary>
    private enum RedirectMode { Wild, Basin, Administer, Suppress, Fill }

        /// <summary>
        /// DBH 的"饮水/水瓶"链 JobDriver → 重定向模式。
        /// DrinkFromGround=生水、DrinkFromBasin=补 MEE 水、AdministerFluids=给病患补 MEE 水；
        /// Stockpile/Pack/LoadFridge=退出（由 MEE 的 WorkGiver_MEE_StoreWater 处理 MEE 水物品的存储）。
        /// </summary>
        private static readonly Dictionary<string, RedirectMode> DBHWaterJobMap = new()
        {
            { "DubsBadHygiene.JobDriver_DrinkFromGround", RedirectMode.Wild },
            { "DubsBadHygiene.JobDriver_DrinkFromBasin", RedirectMode.Basin },
            { "DubsBadHygiene.JobDriver_AdministerFluids", RedirectMode.Administer },
            { "DubsBadHygiene.JobDriver_StockpileWaterBottles", RedirectMode.Fill },
            { "DubsBadHygiene.JobDriver_PackWaterBottle", RedirectMode.Suppress },
            { "DubsBadHygiene.JobDriver_LoadFridge", RedirectMode.Suppress },
        };

        /// <summary>MEE 加载时由 MetabolicEssentialModule.Init 调用：检测 DBH 并注册 DBH 补丁。</summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            if (!DBHPresent)
            {
                // 未检测到 DBH：MEE 水系统独立运行，无需任何 DBH 补丁。
                return;
            }

            var harmony = new Harmony("Lenatuo.metabolicEssential.water");

            // 1) 压制 DBHThirst 需求（private 方法，手动定位）。
            var shouldHaveNeed = AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed");
            if (shouldHaveNeed != null)
            {
                harmony.Patch(shouldHaveNeed, postfix: new HarmonyMethod(typeof(WaterAdapter), nameof(DBHThirstSuppressor)));
                Log.Message("[MetabolicEssential] 检测到 DBH，已压制其水需求(DBHThirst)，由 MEE 水系统接管饮水。");
            }
            else
            {
                Log.Error("[MetabolicEssential] 无法定位 Pawn_NeedsTracker.ShouldHaveNeed，DBH 水需求压制失败。");
            }

            // 2) 将 DBH 饮水 JobDriver 重定向到 MEE 水系统（或退出水瓶物流 Job）。
            int patched = 0;
            foreach (var kv in DBHWaterJobMap)
            {
                var t = AccessTools.TypeByName(kv.Key);
                if (t == null) continue;

                // 强制 TryMakePreToilReservations 成功并跳过原方法，确保完全不执行 DBH 预留代码（防 NRE）。
                var preReserve = AccessTools.Method(t, "TryMakePreToilReservations", new[] { typeof(bool) });
                if (preReserve != null)
                    harmony.Patch(preReserve, prefix: new HarmonyMethod(typeof(WaterAdapter), nameof(ForcePreToilReservations)));

                var makeToils = AccessTools.Method(t, "MakeNewToils");
                if (makeToils != null)
                {
                    string prefix = kv.Value switch
                    {
                        RedirectMode.Wild => nameof(RedirectWild),
                        RedirectMode.Basin => nameof(RedirectBasin),
                        RedirectMode.Administer => nameof(RedirectAdminister),
                        RedirectMode.Fill => nameof(RedirectFill),
                        _ => nameof(SuppressMakeNewToils),
                    };
                    harmony.Patch(makeToils, prefix: new HarmonyMethod(typeof(WaterAdapter), prefix));
                    patched++;
                }
            }
            if (patched > 0)
                Log.Message($"[MetabolicEssential] 已重定向 {patched} 个 DBH 饮水/水瓶 JobDriver 到 MEE 水系统（或退出水瓶物流）。");
        }

        /// <summary>压制 DBHThirst 的 Postfix。</summary>
        private static void DBHThirstSuppressor(NeedDef nd, ref bool __result)
        {
            if (!__result || nd == null || nd.defName != DBHThirstDefName) return;
            if (!MetaBolicLoadCtrl.Active) return;
            if (DefDatabase<NeedDef>.GetNamedSilentFail(DBHThirstDefName) == null) return;
            __result = false;
        }

        /// <summary>JobDriver.TryMakePreToilReservations 前缀：
        /// 对 Drink/Suppress 类（MEE 完全自建 toils，不依赖 DBH 预留）→ 强制成功并跳过原方法（防 NRE）；
        /// 对 Fill 类（管网灌装，覆盖式 toils 仍需正常预定设施）→ 放行原版 reserve 逻辑。</summary>
        private static bool ForcePreToilReservations(ref bool __result, object __instance)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            // Fill 模式：让 DBH 原版 reserve 正常跑（预定设施），不拦截。
            if (__instance != null && __instance.GetType().FullName == "DubsBadHygiene.JobDriver_StockpileWaterBottles")
                return true;
            __result = true;
            return false;
        }

        /// <summary>JobDriver.MakeNewToils 前缀（生水）：喝自然水面，真实补 Need_MEE_Water 并带 20% 食物性中毒。</summary>
        private static bool RedirectWild(ref IEnumerable<Toil> __result, object __instance)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            Pawn pawn = ((JobDriver)__instance).pawn;
            __result = BuildMEEWaterToils(pawn, RedirectMode.Wild);
            return false;
        }

        /// <summary>JobDriver.MakeNewToils 前缀（水盆）：补 Need_MEE_Water（过滤水，不中毒）。</summary>
        private static bool RedirectBasin(ref IEnumerable<Toil> __result, object __instance)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            Pawn pawn = ((JobDriver)__instance).pawn;
            __result = BuildMEEWaterToils(pawn, RedirectMode.Basin);
            return false;
        }

        /// <summary>JobDriver.MakeNewToils 前缀（喂水）：给病患补 Need_MEE_Water。</summary>
        private static bool RedirectAdminister(ref IEnumerable<Toil> __result, object __instance)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            Pawn medic = ((JobDriver)__instance).pawn;
            __result = BuildMEEWaterToils(medic, RedirectMode.Administer);
            return false;
        }

        /// <summary>JobDriver.MakeNewToils 前缀（水瓶物流）：MEE 退出，空 toil 立即结束。</summary>
        private static bool SuppressMakeNewToils(ref IEnumerable<Toil> __result)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            __result = Enumerable.Empty<Toil>();
            return false;
        }

        // —— 以下为“管网灌装”覆盖式实现的 DBH 反射缓存（零硬引用，DBH 缺席时全部为 null）——
        private static Type _dbhCompPipeType;       // DubsBadHygiene.CompPipe
        private static PropertyInfo _dbhCompPipeNet; // CompPipe.pipeNet -> PlumbingNet
        private static Type _dbhPlumbingNetType;    // DubsBadHygiene.PlumbingNet
        private static MethodInfo _dbhPullWater;     // PlumbingNet.PullWater(Single, out ContaminationLevel)
        private static PropertyInfo _dbhNetWater;    // PlumbingNet.WaterStorage (get)
        private static Type _dbhContamType;         // DubsBadHygiene.ContaminationLevel (enum)
        private static bool _dbhFillApiResolved;

        private static void ResolveDBHFillApi()
        {
            if (_dbhFillApiResolved) return;
            _dbhFillApiResolved = true;
            try
            {
                _dbhCompPipeType = AccessTools.TypeByName("DubsBadHygiene.CompPipe");
                _dbhPlumbingNetType = AccessTools.TypeByName("DubsBadHygiene.PlumbingNet");
                _dbhContamType = AccessTools.TypeByName("DubsBadHygiene.ContaminationLevel");
                if (_dbhCompPipeType != null)
                    _dbhCompPipeNet = _dbhCompPipeType.GetProperty("pipeNet", BindingFlags.Public | BindingFlags.Instance);
                if (_dbhPlumbingNetType != null)
                {
                    _dbhNetWater = _dbhPlumbingNetType.GetProperty("WaterStorage", BindingFlags.Public | BindingFlags.Instance);
                    _dbhPullWater = _dbhPlumbingNetType.GetMethod("PullWater",
                        BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float), _dbhContamType.MakeByRefType() }, null);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[MetabolicEssential] 解析 DBH 管网灌装 API 失败: {e.Message}");
            }
        }

        /// <summary>JobDriver.MakeNewToils 前缀（管网灌装）：完全覆盖 DBH 原版。
        /// 自己构造 toils：走到带 CompPipe 的灌装设施 → 从管网 PlumbingNet 真实扣水（PullWater）→ 逐瓶 Spawn MEE_WaterBottle。
        /// 不调用 DBH 原版 MakeNewToils，因此绝不会产生 DBH_WaterBottle；扣水/单位/配额全部由 MEE 按 0.5L/瓶、尊重 job.count 掌控。
        /// 零硬引用 DBH：仅通过 AccessTools 反射其 CompPipe/PlumbingNet，DBH 改名或 API 变动时静默跳过。</summary>
        private static bool RedirectFill(ref IEnumerable<Toil> __result, object __instance)
        {
            if (!MetaBolicLoadCtrl.Active) return true;
            ResolveDBHFillApi();
            if (_dbhCompPipeType == null || _dbhCompPipeNet == null || _dbhPlumbingNetType == null
                || _dbhPullWater == null || _dbhNetWater == null || _dbhContamType == null)
            {
                Log.Warning("[MetabolicEssential] 管网灌装所需 DBH API 不可用，退回 DBH 原版。");
                return true; // 缺 API 则让 DBH 自行运行，绝不空跑。
            }

            Pawn pawn = ((JobDriver)__instance).pawn;
            __result = BuildMEEWaterFillToils((JobDriver)__instance);
            return false;
        }

        /// <summary>
        /// 构造 MEE 水补给定 toil：在延迟期间内每 tick 调用 Need_MEE_Water.Drink() 直到达满。
        /// Wild=自然水面（生水，结束 20% 食物性中毒）；Basin=过滤水（不中毒）；Administer=作用于病患而非执行者。
        /// </summary>
        private static IEnumerable<Toil> BuildMEEWaterToils(Pawn actor, RedirectMode mode)
        {
            if (actor == null) yield break;

            // Administer 时目标为病患（JobGiver 的 targetA 通常是病患 Pawn），拿不到则回退为执行者自身。
            Pawn target = actor;
            if (mode == RedirectMode.Administer)
            {
                var p = actor.CurJob?.targetA.Thing as Pawn;
                if (p != null) target = p;
            }

            Toil drink = new Toil();
            drink.defaultDuration = (mode == RedirectMode.Administer) ? 200 : 1000;
            drink.defaultCompleteMode = ToilCompleteMode.Delay;
            drink.tickAction = delegate
            {
                target.needs?.TryGetNeed<Need_MEE_Water>()?.Drink();
            };
            drink.AddEndCondition(delegate
            {
                Need_MEE_Water w = target.needs?.TryGetNeed<Need_MEE_Water>();
                if (w == null) return JobCondition.Incompletable;
                return w.CurLevel >= 1f ? JobCondition.Succeeded : JobCondition.Ongoing;
            });
            if (mode == RedirectMode.Wild)
            {
                // 自然水面“生水”风险：20% 概率食物性中毒（与原版 FoodPoisoning、MEE 地形饮水一致）。
                drink.AddFinishAction(delegate
                {
                    if (Rand.Chance(0.2f)
                        && target.health?.hediffSet != null
                        && !target.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning))
                    {
                        target.health.AddHediff(HediffDefOf.FoodPoisoning);
                    }
                });
            }
            yield return drink;
        }

        /// <summary>
        /// 管网灌装 toils（完全覆盖 DBH 原版，仿 DBH 一瓶一瓶、带耗时）：走到带 CompPipe 的灌装设施，
        /// 从管网 PlumbingNet 真实逐瓶扣水（每瓶 0.5L，PullWater），每 ticksPerBottle tick 产 1 个 MEE_WaterBottle。
        /// 产瓶总数 = 尊重 job.count（DBH 右键 / FDE WorkGiver 设定的请求量），并受「管网实际可取水量」与「单趟安全上限」双重约束。
        /// 因此「取 10 瓶就出 10 瓶」，且每瓶都有可见耗时；管网中途被抽干则提前停止（已产出的保留）。
        /// 绝不会产生 DBH_WaterBottle。零硬引用 DBH：CompPipe / PlumbingNet / PullWater 全部通过反射缓存访问。
        /// </summary>
        private static IEnumerable<Toil> BuildMEEWaterFillToils(JobDriver driver)
        {
            if (driver == null) yield break;
            Pawn pawn = driver.pawn;
            if (pawn == null) yield break;
            Job job = driver.job;
            if (job == null) yield break;

            const float litersPerBottle = 0.5f;
            const int ticksPerBottle = 90;   // 每瓶耗时 ≈1.5s，逐瓶可见（仿 DBH）
            const int hardMaxBottles = 50;   // 单趟安全上限，防止极端 job.count 卡死

            // 1) 走到灌装设施（targetA 是带 CompPipe 的设施）。
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // 2) 预先计算本趟要灌多少瓶（尊重 job.count，clamp 管网水量与安全上限）。
            Thing fixture = job.targetA.Thing;
            if (fixture == null || fixture.Destroyed) yield break;

            object comp = null;
            if (fixture is ThingWithComps twc)
            {
                foreach (var c in twc.AllComps)
                {
                    if (c != null && c.GetType() == _dbhCompPipeType) { comp = c; break; }
                }
            }
            if (comp == null) yield break;
            object net = _dbhCompPipeNet.GetValue(comp);
            if (net == null) yield break;

            // 管网当前总水量（升）。
            object netWaterObj = _dbhNetWater.GetValue(net);
            float netWater = netWaterObj is float f ? f : 0f;

            int requested = job.count > 0 ? job.count : hardMaxBottles;
            int byWater = (int)Math.Floor(netWater / litersPerBottle);
            int bottlesToMake = Math.Min(Math.Min(requested, byWater), hardMaxBottles);
            if (bottlesToMake <= 0) yield break; // 管网没水或请求为 0：直接结束，不产瓶

            ThingDef meeBottle = DefDatabase<ThingDef>.GetNamedSilentFail("MEE_WaterBottle");
            if (meeBottle == null) yield break;

            Map map = pawn.Map;
            if (map == null) yield break;

            // 落点：设施交互格优先，无效则 8 邻格空地，再退化到设施中心。
            IntVec3 spawnCell = fixture.InteractionCell;
            if (!spawnCell.IsValid || !spawnCell.Standable(map) || spawnCell.GetThingList(map).Any(t => t is Building))
            {
                spawnCell = IntVec3.Invalid;
                int fx = fixture.Position.x, fz = fixture.Position.z;
                for (int dx = -1; dx <= 1 && !spawnCell.IsValid; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        IntVec3 adj = new IntVec3(fx + dx, 0, fz + dz);
                        if (adj.InBounds(map) && adj.Standable(map)
                            && !adj.GetThingList(map).Any(t => t is Building))
                        {
                            spawnCell = adj;
                            break;
                        }
                    }
                }
            }
            if (!spawnCell.IsValid) spawnCell = fixture.Position;

            // 逐瓶灌装：每 ticksPerBottle tick 拉 0.5L 管网水并产 1 瓶（优先并入已有同类堆叠）。
            int remaining = bottlesToMake;
            int timer = 0;

            Toil fill = new Toil();
            fill.defaultCompleteMode = ToilCompleteMode.Delay;
            fill.defaultDuration = Math.Max(1, bottlesToMake * ticksPerBottle);
            fill.tickAction = delegate
            {
                if (remaining <= 0) return;
                if (++timer < ticksPerBottle) return;
                timer = 0;

                // 真实从管网扣 0.5L（out 污染等级参数用枚举默认值占位）。
                object contam = _dbhContamType != null ? Enum.ToObject(_dbhContamType, 0) : null;
                object[] args = new object[] { litersPerBottle, contam };
                bool pulled;
                try
                {
                    pulled = (bool)_dbhPullWater.Invoke(net, args);
                }
                catch (Exception e)
                {
                    Log.Warning($"[MetabolicEssential] 管网扣水失败: {e.Message}");
                    pulled = false;
                }
                if (!pulled) { remaining = 0; return; } // 管网被抽干：提前停止，已产出的保留

                // 产 1 瓶：优先并入 spawnCell 已有 MEE_WaterBottle 堆叠，否则落地新瓶。
                IntVec3 cell = spawnCell;
                Thing onCell = map.thingGrid.ThingAt(cell, ThingCategory.Item);
                if (onCell != null && (onCell.def != meeBottle || onCell.stackCount >= meeBottle.stackLimit))
                {
                    // 落点被占用/已满：在设施 8 邻格找空地或可并入的同类堆叠。
                    cell = IntVec3.Invalid;
                    foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(fixture))
                    {
                        if (!c.InBounds(map) || !c.Standable(map)) continue;
                        Thing a = map.thingGrid.ThingAt(c, ThingCategory.Item);
                        if (a == null) { cell = c; break; }
                        if (a.def == meeBottle && a.stackCount < meeBottle.stackLimit) { cell = c; break; }
                    }
                    if (!cell.IsValid) cell = spawnCell;
                }

                Thing here = map.thingGrid.ThingAt(cell, ThingCategory.Item);
                if (here != null && here.def == meeBottle && here.stackCount < meeBottle.stackLimit)
                    here.stackCount++;
                else
                {
                    Thing prod = ThingMaker.MakeThing(meeBottle);
                    prod.stackCount = 1;
                    GenSpawn.Spawn(prod, cell, map);
                }
                remaining--;
            };
            fill.AddEndCondition(delegate
            {
                return remaining <= 0 ? JobCondition.Succeeded : JobCondition.Ongoing;
            });
            yield return fill;
        }
        /// 统一的水需求访问入口（预留）：MEE 为权威，直接返回 Need_MEE_Water。
        /// DBH 模式下 DBHThirst 已被压制，不存在，故返回 MEE 的水需求即唯一真相源。
        /// </summary>
        public static Need GetWaterNeed(Pawn pawn)
        {
            return pawn?.needs?.TryGetNeed<Need_MEE_Water>();
        }
    }
}
