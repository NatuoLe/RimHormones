using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace Hormones.UI
{
    /// <summary>
    /// 单条飘字数据。**从对象池借还**，不要在外部直接 new。
    /// </summary>
    public class FlyTextEntry
    {
        public Vector3 worldPos;   // 世界坐标（地图格中心 + 抬升）
        public string text;        // 显示文本
        public Color color;        // 颜色（可替换）
        public float ageSec;       // 已存活秒数
        public float lifeSec;      // 总寿命（秒）
        public float velZ;         // 每秒上浮的世界 Z 速度
        public bool active;        // 是否在使用中

        public void Reset()
        {
            worldPos = Vector3.zero;
            text = null;
            color = Color.white;
            ageSec = 0f;
            lifeSec = 0f;
            velZ = 0f;
            active = false;
        }
    }

    /// <summary>
    /// 全局飘字管理器（GameComponent）。
    /// 特点：
    ///   1) 飘字 UI 对象（FlyTextEntry）从对象池获取/归还，避免 GC；
    ///   2) 字符串拼接用池化的 StringBuilder（AcquireSB/ReleaseSB）；
    ///   3) 颜色可通过参数替换；
    ///   4) GameComponentUpdate 推进上浮 + 淡出，GameComponentOnGUI 自绘。
    /// 用法：FlyTextMgr.Push(pawn, "文本", color);
    /// 或先 var sb = FlyTextMgr.AcquireSB(); sb.Append(...); FlyTextMgr.Push(pawn, sb, color);（自动归还 SB）。
    /// </summary>
    public class FlyTextMgr : GameComponent
    {
        // ---- 单例（当前局存在的实例）----
        public static FlyTextMgr Instance { get; private set; }

        // ---- 飘字对象池 ----
        private readonly List<FlyTextEntry> active = new List<FlyTextEntry>();
        private readonly Stack<FlyTextEntry> pool = new Stack<FlyTextEntry>();

        // ---- StringBuilder 池 ----
        private static readonly Stack<StringBuilder> sbPool = new Stack<StringBuilder>();

        // ---- 可调参数 ----
        private const float DefaultLifeSec = 3.0f;     // 存活时间
        private const float FadeStartFrac = 0.45f;     // 从此比例开始淡出
        private const float RiseWorldZPerSec = 0.9f;   // 每秒上浮世界 Z
        private const float SpawnJitterX = 0.32f;      // 出生水平抖动（世界 X）
        private const float WorldZOffset = 0.85f;      // 初始头顶抬升
        private const int InitialPoolSize = 24;
        private const int MaxActive = 128;             // 防爆：同屏最多飘字数

        public FlyTextMgr(Game game)
        {
            Prewarm();
        }

        public override void FinalizeInit()
        {
            Instance = this;
            Prewarm();
        }

        public override void LoadedGame()
        {
            Instance = this;
        }

        public override void StartedNewGame()
        {
            Instance = this;
            // 开局一次性把背景故事体魄偏移烤进开局殖民民的 Physique 技能（仅基础<阈值者）
            PhysiqueLgc.BakeBackstoryBonusForStartingPawns();
        }

        private void Prewarm()
        {
            while (pool.Count < InitialPoolSize)
            {
                pool.Push(new FlyTextEntry());
            }
        }

        // ============================================================
        //  StringBuilder 池
        // ============================================================
        public static StringBuilder AcquireSB()
        {
            lock (sbPool)
            {
                if (sbPool.Count > 0)
                {
                    StringBuilder sb = sbPool.Pop();
                    sb.Length = 0;
                    return sb;
                }
            }
            return new StringBuilder(64);
        }

        public static void ReleaseSB(StringBuilder sb)
        {
            if (sb == null) return;
            sb.Length = 0;
            lock (sbPool)
            {
                if (sbPool.Count < 32)
                    sbPool.Push(sb);
            }
        }

        // ============================================================
        //  推送飘字（对外主入口）
        // ============================================================

        /// <summary>在 pawn 头顶推送一条飘字，颜色可指定。</summary>
        public static void Push(Pawn pawn, string text, Color color)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned) return;
            if (Current.Game == null) return;
            FlyTextMgr mgr = Instance ?? Current.Game.GetComponent<FlyTextMgr>();
            if (mgr == null) return;
            // 仅在飘字所在地图为当前显示地图时才有意义（其它地图看不到），但仍缓存以防切图。
            mgr.PushInternal(pawn.DrawPos, text, color);
        }

        /// <summary>用池化 StringBuilder 推送；推送后自动归还 SB。</summary>
        public static void Push(Pawn pawn, StringBuilder sb, Color color)
        {
            if (sb == null) return;
            Push(pawn, sb.ToString(), color);
            ReleaseSB(sb);
        }

        /// <summary>在任意世界坐标推送飘字。</summary>
        public static void PushAt(Vector3 worldPos, string text, Color color)
        {
            if (Current.Game == null) return;
            FlyTextMgr mgr = Instance ?? Current.Game.GetComponent<FlyTextMgr>();
            if (mgr == null) return;
            mgr.PushInternal(worldPos, text, color);
        }

        private void PushInternal(Vector3 drawPos, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (active.Count >= MaxActive)
            {
                // 超上限：回收最老的一条
                RecycleAt(0);
            }

            FlyTextEntry e = pool.Count > 0 ? pool.Pop() : new FlyTextEntry();
            e.Reset();
            e.worldPos = drawPos;
            e.worldPos.x += Rand.Range(-SpawnJitterX, SpawnJitterX);
            e.worldPos.z += WorldZOffset;
            e.text = text;
            e.color = color;
            e.lifeSec = DefaultLifeSec;
            e.velZ = RiseWorldZPerSec;
            e.active = true;
            active.Add(e);
        }

        // ============================================================
        //  推进 & 回收
        // ============================================================
        public override void GameComponentUpdate()
        {
            if (active.Count == 0) return;
            // 用真实帧时间推进（不受游戏暂停影响，飘字照常上浮淡出）
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 1f / 60f;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                FlyTextEntry e = active[i];
                e.ageSec += dt;
                e.worldPos.z += e.velZ * dt;
                if (e.ageSec >= e.lifeSec)
                {
                    RecycleAt(i);
                }
            }
        }

        private void RecycleAt(int index)
        {
            FlyTextEntry e = active[index];
            active.RemoveAt(index);
            e.Reset();
            if (pool.Count < 256)
                pool.Push(e);
        }

        // ============================================================
        //  绘制
        // ============================================================
        public override void GameComponentOnGUI()
        {
            if (active.Count == 0) return;
            if (Event.current.type != EventType.Repaint) return;
            if (RimWorld.Planet.WorldRendererUtility.WorldSelected) return; // 世界地图视角下不绘制

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;

            Camera cam = Find.Camera;
            for (int i = 0; i < active.Count; i++)
            {
                FlyTextEntry e = active[i];
                if (!e.active || string.IsNullOrEmpty(e.text)) continue;

                // 淡出 alpha
                float a = 1f;
                float fadeStart = e.lifeSec * FadeStartFrac;
                if (e.ageSec > fadeStart)
                {
                    float t = (e.ageSec - fadeStart) / (e.lifeSec - fadeStart);
                    a = Mathf.Clamp01(1f - t);
                }

                // 世界 → 屏幕坐标
                Vector3 sp = cam.WorldToScreenPoint(e.worldPos) / Prefs.UIScale;
                if (sp.z < 0f) continue; // 在相机背后
                float sx = sp.x;
                float sy = (float)Verse.UI.screenHeight - sp.y;

                float w = Text.CalcSize(e.text).x;
                Color c = e.color;
                c.a *= a;
                GUI.color = c;
                Widgets.Label(new Rect(sx - w / 2f, sy - 2f, w, 999f), e.text);
            }

            GUI.color = oldColor;
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
        }

        // 换局时清空（GameComponent 随 Game 释放，静态引用要断开）
        public override void ExposeData()
        {
            // 飘字是纯表现层、无需存档；仅在加载时确保清空活动列表。
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                active.Clear();
            }
        }
    }
}
