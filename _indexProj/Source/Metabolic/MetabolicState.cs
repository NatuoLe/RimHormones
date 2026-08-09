using Verse;

namespace Hormones
{
    /// <summary>
    /// 代谢扩展模块的运行状态标志。
    /// 由 MetabolicLoader 在成功加载并初始化 MetabolicEssential.dll 后置为 true。
    ///
    /// 主 mod 内由该模块“接管”的代谢需求（水分 / 糖 / 电解质 / 蛋白质）都先检查此标志，
    /// 从而被游戏内开关统一控制：开关关闭 → 这些需求保持初始满值、零消耗、零副作用（等效于“未启用”）。
    /// 因勾选需重启客户端才生效，本标志在单次会话内最多被设置一次。
    ///
    /// 注意：这些需求本身由主 mod 默认加载（需求栏常驻），只是其“代谢机制”受模块开关控制。
    /// </summary>
    public static class MetabolicState
    {
        /// <summary>代谢扩展模块是否已加载并激活（驱动“代谢需求”的消耗与显示）。</summary>
        public static bool Active = false;

        /// <summary>
        /// 控制主 mod 内四个代谢需求（水分 / 糖 / 电解质 / 蛋白质）是否在需求栏“显示”。
        /// 仅当 Metabolic Essential 模块被成功加载后置为 true；否则这四个需求恒为隐藏（仍实例化、不消耗、零副作用）。
        /// 由 MetabolicLoader.TryLoad 在 Init 成功后统一设置，与 Active 同步。
        /// </summary>
        public static bool IsLoadedMME = false;
    }
}
