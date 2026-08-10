using Verse;

namespace Hormones
{
    /// <summary>
    /// 主 mod 内「需求数值变化」事件的统一发布中心。
    ///
    /// 设计目的：让可选模块（如 Metabolic Essential）能“监听”劳损 / 皮质醇 / 糖 等需求的变化，
    /// 而不必直接读取或写入主 mod 的私有字段——从而避免「字段入侵主工程」。
    ///
    /// 约定：
    ///  - 事件是 public，外部程序集可订阅（+=），但只能订阅、不能调用（event 语义）。
    ///  - 触发方法（Fire*）是 internal，仅主 mod 自身可调用；因此模块永远无法直接发动事件。
    ///  - 回调参数为 (Pawn, oldValue, newValue)，oldValue/newValue 为该需求 CurLevel 的【绝对数值】
    ///    （注意：皮质醇 MaxLevel=10000、糖 MaxLevel=1，跨需求比较时需各自先除以自身 MaxLevel 归一到 0~1）。
    ///
    /// 触发点：
    ///  - OnStrainChanged   ← Need_MuscleStrain 的 CurLevel 发生变化时
    ///  - OnCortisolChanged ← Need_Cortisol 的 CurLevel 发生变化时
    ///  - OnSugarChanged    ← Need_MEE_Sugar 的 CurLevel 发生变化时
    /// </summary>
    public static class NeedChangeEvents
    {
        /// <summary>需求数值变化回调签名：(发生变化的 pawn, 变化前 CurLevel, 变化后 CurLevel)。</summary>
        public delegate void NeedChangedHandler(Pawn pawn, float oldValue, float newValue);

        /// <summary>劳损（Need_MuscleStrain）变化时触发。</summary>
        public static event NeedChangedHandler OnStrainChanged;

        /// <summary>皮质醇（Need_Cortisol）变化时触发。</summary>
        public static event NeedChangedHandler OnCortisolChanged;

        /// <summary>糖（Need_MEE_Sugar）变化时触发。</summary>
        public static event NeedChangedHandler OnSugarChanged;

        internal static void FireStrainChanged(Pawn pawn, float oldV, float newV)
            => OnStrainChanged?.Invoke(pawn, oldV, newV);

        internal static void FireCortisolChanged(Pawn pawn, float oldV, float newV)
            => OnCortisolChanged?.Invoke(pawn, oldV, newV);

        internal static void FireSugarChanged(Pawn pawn, float oldV, float newV)
            => OnSugarChanged?.Invoke(pawn, oldV, newV);
    }
}
