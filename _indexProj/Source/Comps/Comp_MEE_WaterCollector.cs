using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace Hormones
{
    /// <summary>
    /// 水分收集器组件（被动建筑，无账单、无需殖民者操作）。
    ///
    /// 通用采集模型，同一套逻辑同时支撑两种建筑：
    ///  - 下雨/下雪时按降水强度收集，叠加 rainMultiplier 倍率；
    ///  - 不下雨时若 airMultiplier &gt; 0，可从空气中恒定速率持续收集（代表“水分收集器”抽湿）；
    ///  - 若 requiresPower，必须通电（CompPowerTrader.PowerOn）才工作；
    ///  - 若 needsNoRoof，下雨收集要求建筑未被屋顶覆盖（雨淋不到屋顶之下）。
    ///
    /// 水箱每攒够 1 单位就立即产出一份 spawnDefName（默认生水 MEE_RawWater），
    /// 即产即出、不留整数缓存（小数保留继续累积，不丢水也不等攒满）。
    /// 落点优先并入已有同类堆叠，避免散落成多堆。
    ///
    /// ⚠ 关键坑：Building 默认进入 Rare tick 列表，原版只回调 CompTickRare()，
    ///   CompTick() 不会被调用。积累逻辑必须同时挂在 CompTick 与 CompTickRare 上。
    /// </summary>
    public class CompProperties_MEE_WaterCollector : CompProperties
    {
        /// <summary>整日满强度（RainRate/SnowRate=1）降水可收集的单位数（基准速率）。</summary>
        public float bottlesPerDay = 10f;

        /// <summary>内部储水缓冲上限（单位）。即产即出模式下仅作显示/容量参考，不再是产出门槛。</summary>
        public float maxStored = 12f;

        /// <summary>产出物 defName（默认生水 MEE_RawWater；改产出直接改 XML，无需重编）。</summary>
        public string spawnDefName = "MEE_RawWater";

        /// <summary>是否把下雪也算作降水（强度取雨/雪较大者）。简易雨水收集器设 false（只认雨）。</summary>
        public bool collectSnow = true;

        /// <summary>下雨/下雪时的速率倍率。水分收集器设 2（下雨 2 倍），简易雨水收集器设 1（同当前）。</summary>
        public float rainMultiplier = 1f;

        /// <summary>不下雨时从空气收集的恒定速率倍率（bottlesPerDay × 该值）。0 表示不下雨不收集。水分收集器设 0.5（½ 当前）。</summary>
        public float airMultiplier = 0f;

        /// <summary>是否需要通电才工作（水分收集器设 true）。</summary>
        public bool requiresPower = false;

        /// <summary>下雨收集是否要求建筑未被屋顶覆盖（简易雨水收集器设 true：有屋顶接不到雨）。</summary>
        public bool needsNoRoof = false;

        public CompProperties_MEE_WaterCollector()
        {
            compClass = typeof(Comp_MEE_WaterCollector);
        }
    }

    public class Comp_MEE_WaterCollector : ThingComp
    {
        private CompProperties_MEE_WaterCollector Props => (CompProperties_MEE_WaterCollector)props;
        private CompPowerTrader powerComp;
        private float stored = 0f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
        }

        // 覆盖 Normal tick 与 Rare tick，确保无论建筑进哪个 tick 列表积累逻辑都能跑。
        public override void CompTick()
        {
            Accumulate(1);
        }

        public override void CompTickRare()
        {
            Accumulate(250);
        }

        private void Accumulate(int ticksElapsed)
        {
            if (!parent.Spawned || parent.Map == null)
                return;

            Map map = parent.Map;
            float intensity = Mathf.Max(map.weatherManager.RainRate,
                Props.collectSnow ? map.weatherManager.SnowRate : 0f);

            float rate = 0f;
            if (intensity > 0.001f)
            {
                // 下雨/下雪：受 rainMultiplier 缩放。needsNoRoof 且被屋顶覆盖则接不到雨。
                if (Props.needsNoRoof && parent.Position.Roofed(map))
                    rate = 0f;
                else
                    rate = Props.bottlesPerDay * Props.rainMultiplier * intensity;
            }
            else
            {
                // 不下雨：从空气恒定收集（airMultiplier=0 时即不收集）。
                rate = Props.bottlesPerDay * Props.airMultiplier;
            }

            // 供电门控：需电但没通电则不收集。
            if (rate > 0f && Props.requiresPower && powerComp != null && !powerComp.PowerOn)
                rate = 0f;

            if (rate > 0f)
                stored += rate * ticksElapsed / 60000f;

            // 即产即出：每攒够 1 单位立即产出一份，整数部分全部结算，
            // 小数保留继续累积（不丢水、不攒满才出、不出现“满箱断供”空档）。
            int n = Mathf.FloorToInt(stored);
            if (n > 0)
            {
                stored -= n;
                SpawnBottles(n, map);
            }
        }

        /// <summary>成批产出：优先并入已有同类堆叠，必要时拆分到邻格。</summary>
        private void SpawnBottles(int n, Map map)
        {
            ThingDef def = ThingDef.Named(Props.spawnDefName);
            if (def == null || n <= 0)
                return;
            int remaining = n;
            while (remaining > 0)
            {
                int take = Mathf.Min(remaining, def.stackLimit);
                IntVec3 cell = FindDropCell(map, def);
                Thing existing = map.thingGrid.ThingAt(cell, ThingCategory.Item);
                if (existing != null && existing.def == def && existing.stackCount < def.stackLimit)
                {
                    int room = def.stackLimit - existing.stackCount;
                    int merge = Mathf.Min(take, room);
                    existing.stackCount += merge;
                    remaining -= merge;
                    take -= merge;
                }
                if (take > 0)
                {
                    Thing t = ThingMaker.MakeThing(def);
                    t.stackCount = take;
                    GenSpawn.Spawn(t, cell, map);
                    remaining -= take;
                }
            }
        }

        /// <summary>在建筑 8 邻格中找落点：优先已有同类 → 并入堆叠；其次空地；最后建筑中心。</summary>
        private IntVec3 FindDropCell(Map map, ThingDef def)
        {
            foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(parent))
            {
                if (!c.InBounds(map) || !c.Walkable(map))
                    continue;
                Thing existing = map.thingGrid.ThingAt(c, ThingCategory.Item);
                if (existing != null && existing.def == def && existing.stackCount < def.stackLimit)
                    return c;
            }
            foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(parent))
            {
                if (!c.InBounds(map) || !c.Walkable(map))
                    continue;
                if (map.thingGrid.ThingAt(c, ThingCategory.Item) == null)
                    return c;
            }
            return parent.Position;
        }

        /// <summary>选中建筑时在检视面板显示收集进度与当前速率（点击即见，实时刷新）。</summary>
        public override string CompInspectStringExtra()
        {
            string s = "MEE_WaterCollectorStored".Translate() + ": " + stored.ToString("F2") + " / " + Props.maxStored.ToString("F0");
            if (!parent.Spawned || parent.Map == null)
                return s;
            Map map = parent.Map;

            if (Props.requiresPower && powerComp != null && !powerComp.PowerOn)
            {
                s += "\n" + "MEE_WaterCollectorNoPower".Translate();
                return s;
            }

            float intensity = Mathf.Max(map.weatherManager.RainRate,
                Props.collectSnow ? map.weatherManager.SnowRate : 0f);

            if (intensity > 0.001f)
            {
                if (Props.needsNoRoof && parent.Position.Roofed(map))
                    s += "\n" + "MEE_WaterCollectorNoRoof".Translate();
                else
                {
                    float rate = Props.bottlesPerDay * Props.rainMultiplier * intensity;
                    s += "\n" + "MEE_WaterCollectorRate".Translate(rate.ToString("F1"));
                }
            }
            else
            {
                if (Props.airMultiplier > 0f)
                    s += "\n" + "MEE_WaterCollectorAir".Translate((Props.bottlesPerDay * Props.airMultiplier).ToString("F1"));
                else
                    s += "\n" + "MEE_WaterCollectorIdle".Translate();
            }
            return s;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            yield return new StatusGizmo(
                "MEE_WaterCollectorStored".Translate() + ": " + stored.ToString("F2") + " / " + Props.maxStored.ToString("F0"));
        }

        /// <summary>只读状态 gizmo：显示当前储水量（灰色、不可点击）。</summary>
        private class StatusGizmo : Command_Action
        {
            public StatusGizmo(string label)
            {
                defaultLabel = label;
                defaultDesc = "";
                disabled = true;   // protected，子类内可赋值
            }
        }

        /// <summary>储水量随存档持久化，重载后继续累积。</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref stored, "stored", 0f);
        }
    }
}
