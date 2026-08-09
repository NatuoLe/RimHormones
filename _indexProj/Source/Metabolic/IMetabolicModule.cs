using Verse;

namespace Hormones
{
    /// <summary>
    /// 代谢扩展模块的契约。主 mod 只认这个接口，不编译引用具体实现，
    /// 从而保持 MetabolicEssential 模块“真正可选”，后期也能整体拆成独立 mod。
    /// </summary>
    public interface IMetabolicModule
    {
        /// <summary>
        /// 模块被主 mod 在启动期加载时调用。在此注册 Harmony 补丁、初始化逻辑。
        /// </summary>
        void Init(ModContentPack pack);
    }
}
