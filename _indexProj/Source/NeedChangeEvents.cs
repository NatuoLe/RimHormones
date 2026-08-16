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
    ///  - OnDietEaten       ← 殖民者摄入食物（饮食）实际获得营养时；携带食物实例供模块按类型转化（营养 15%→水、素菜 8%→糖）
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

        /// <summary>水（Need_MEE_Water）变化时触发。</summary>
        public static event NeedChangedHandler OnWaterChanged;

        /// <summary>电解质（Need_MEE_Electrolytes）变化时触发。</summary>
        public static event NeedChangedHandler OnElectrolytesChanged;

        /// <summary>蛋白质（Need_MEE_Protein）变化时触发。</summary>
        public static event NeedChangedHandler OnProteinChanged;

        /// <summary>饮食（摄入食物）时获得的营养量回调签名：(pawn, 实际获得的营养值 0~1+, 被吃的食物实例)。</summary>
        public delegate void DietEatenHandler(Pawn pawn, float nutritionGained, Thing food);

        /// <summary>殖民者摄入食物（饮食）获得营养时触发。供代谢模块把营养按比例转化为水分（15%）、素食再转糖（8%）等。</summary>
        public static event DietEatenHandler OnDietEaten;

        /// <summary>饮用 MEE 水瓶时的回调签名：(pawn, 饮用前的 Water 满足度 0~1)。供模块检测溢出→水中毒。</summary>
        public delegate void MEEDrinkHandler(Pawn pawn, float levelBeforeDrink);

        /// <summary>饮用 MEE 水瓶（补充 Water）时触发。</summary>
        public static event MEEDrinkHandler OnDrinkMEEWater;

        /// <summary>摄入 MEE 葡萄糖原浆（补充 Sugar）时触发。供模块施加「吃了糖」心情 Buff。</summary>
        public static event MEEDrinkHandler OnSugarEaten;

        /// <summary>体魄等级变化回调签名：(pawn, 变化前等级, 变化后等级)。</summary>
        public delegate void PhysiqueLevelChangedHandler(Pawn pawn, int oldLevel, int newLevel);

        /// <summary>体魄（Physique 技能）等级提升时触发。供代谢模块扣减蛋白质缓冲。</summary>
        public static event PhysiqueLevelChangedHandler OnPhysiqueLevelChanged;

        internal static void FireStrainChanged(Pawn pawn, float oldV, float newV)
            => OnStrainChanged?.Invoke(pawn, oldV, newV);

        internal static void FireCortisolChanged(Pawn pawn, float oldV, float newV)
            => OnCortisolChanged?.Invoke(pawn, oldV, newV);

        internal static void FireSugarChanged(Pawn pawn, float oldV, float newV)
            => OnSugarChanged?.Invoke(pawn, oldV, newV);

        internal static void FireWaterChanged(Pawn pawn, float oldV, float newV)
            => OnWaterChanged?.Invoke(pawn, oldV, newV);

        internal static void FireElectrolytesChanged(Pawn pawn, float oldV, float newV)
            => OnElectrolytesChanged?.Invoke(pawn, oldV, newV);

        internal static void FireProteinChanged(Pawn pawn, float oldV, float newV)
            => OnProteinChanged?.Invoke(pawn, oldV, newV);

        internal static void FireDietEaten(Pawn pawn, float nutritionGained, Thing food)
            => OnDietEaten?.Invoke(pawn, nutritionGained, food);

        internal static void FireDrinkMEEWater(Pawn pawn, float levelBeforeDrink)
            => OnDrinkMEEWater?.Invoke(pawn, levelBeforeDrink);

        internal static void FireSugarEaten(Pawn pawn, float levelBeforeEat)
            => OnSugarEaten?.Invoke(pawn, levelBeforeEat);

        internal static void FirePhysiqueLevelChanged(Pawn pawn, int oldLevel, int newLevel)
            => OnPhysiqueLevelChanged?.Invoke(pawn, oldLevel, newLevel);
    }
}
