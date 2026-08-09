using Verse;

namespace Hormones
{
    /// <summary>
    /// 在游戏启动期（所有 mod 加载完成后）触发可选模块的加载。
    /// 放到 [StaticConstructorOnStartup] 而非 Mod 构造函数，确保 Settings 已就绪、AppDomain 稳定。
    /// 运行期改设置不会重跑此处，因此开关变更必须重启客户端才生效。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MetabolicBootstrap
    {
        static MetabolicBootstrap()
        {
            RimHormonesMod mod = LoadedModManager.GetMod<RimHormonesMod>();
            if (mod != null)
                MetabolicLoader.TryLoad(mod.Content);
        }
    }
}
