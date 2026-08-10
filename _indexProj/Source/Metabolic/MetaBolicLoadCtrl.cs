using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

namespace Hormones
{
    /// <summary>
    /// 代谢扩展（Metabolic Essential）模块加载控制器。
    /// 把原先分散的 MetabolicState / MetabolicLoader / MetabolicBootstrap 合并到本类：
    ///  - 运行状态标志 Active / IsLoadedMME（驱动代谢需求的消耗与显示）
    ///  - 启动期按需加载 MetabolicEssential\ 子目录下 DLL 的 TryLoad
    ///  - [StaticConstructorOnStartup] 静态构造器，在游戏启动期触发加载
    ///
    /// 设计目标：本模块是主 mod 的“真正可选”组件——勾选开关 + 重启后，
    /// 主 mod 用 Assembly.LoadFrom 显式加载 MetabolicEssential\ 子目录下的 DLL 并反射调用其 IMetabolicModule.Init；
    /// 开关关闭则程序集完全不载入（零副作用，主 mod 内的代谢需求保持 inert 满值）。
    ///
    /// 模块契约 IMetabolicModule 与主 mod 只认接口、不编译引用具体实现，保持“真正可选”，后期也能整体拆成独立 mod。
    /// 标记类 MEERecipeMarker 因需继承 DefModExtension，与接口/控制器同处本文件但为独立类型。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MetaBolicLoadCtrl
    {
        public const string ModuleDir = "MetabolicEssential";
        public const string ModuleDll = "MetabolicEssential.dll";

        /// <summary>代谢扩展模块是否已激活（驱动“代谢需求”的消耗与显示）。</summary>
        public static bool Active = false;

        /// <summary>
        /// 控制主 mod 内四个代谢需求（水分 / 糖 / 电解质 / 蛋白质）是否在需求栏“显示”。
        /// 仅当 Metabolic Essential 模块被成功加载后置为 true；否则这四个需求恒为隐藏（仍实例化、不消耗、零副作用）。
        /// 由 TryLoad 在 Init 成功后统一设置，与 Active 同步。
        /// </summary>
        public static bool IsLoadedMME = false;

        /// <summary>模块是否已成功加载并初始化（而非“程序集是否存在”）。</summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>模块 DLL 是否随 mod 一起部署到 MetabolicEssential\ 子目录（与开关无关）。</summary>
        public static bool IsModulePresent => File.Exists(ModulePath());

        public static string ModulePath()
        {
            RimHormonesMod mod = LoadedModManager.GetMod<RimHormonesMod>();
            if (mod == null) return null;
            return Path.Combine(mod.Content.RootDir, ModuleDir, ModuleDll);
        }

        /// <summary>
        /// “代谢扩展”可选模块的启动器。
        /// 部署形态：MetabolicEssential.dll 放在 mod 根的 MetabolicEssential\ 子目录（不在 Assemblies\ 下），
        /// 因此 RimWorld 不会自动加载它。只有玩家在主 mod 设置里勾选开关、且游戏重启后，
        /// 才在启动期用 Assembly.LoadFrom 显式加载该程序集，并反射调用其 IMetabolicModule.Init。
        /// 开关关闭 → 程序集完全不被载入（零副作用；主 mod 里的代谢需求保持 inert 满值）。
        /// 加载只在游戏启动期发生一次（见本类静态构造器），运行期改设置需重启客户端。
        /// </summary>
        public static void TryLoad(ModContentPack pack)
        {
            IsLoaded = false;
            IsLoadedMME = false;
            Active = false;
            if (pack == null)
                return;

            string dll = Path.Combine(pack.RootDir, ModuleDir, ModuleDll);
            if (!File.Exists(dll))
            {
                Log.Warning(
                    $"[RimHormones] 未找到 Metabolic Essential 模块（{dll}）。如需启用，请确认 MetabolicEssential.dll 已部署到本 mod 的 MetabolicEssential\\ 目录。");
                return;
            }

            if (!RimHormonesMod.Settings.EnableMetabolicEssential)
                return;

            try
            {
                Assembly asm = Assembly.LoadFrom(dll);
                Type type = asm.GetTypes()
                    .FirstOrDefault(t => t != null && !t.IsAbstract && !t.IsInterface && typeof(IMetabolicModule).IsAssignableFrom(t));
                if (type == null)
                {
                    Log.Error($"[RimHormones] 在 {ModuleDll} 中未找到实现 Hormones.IMetabolicModule 的类型。");
                    return;
                }

                IMetabolicModule inst = (IMetabolicModule)Activator.CreateInstance(type);
                inst.Init(pack);
                IsLoaded = true;
                Active = true;        // 驱动主 mod 内的代谢需求开始消耗
                IsLoadedMME = true;   // 驱动四个代谢需求在需求栏显示
                Log.Message("[RimHormones] Metabolic Essential 模块已初始化。");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimHormones] 初始化 Metabolic Essential 模块失败：{ex}");
                IsLoaded = false;
            }
        }

        // 游戏启动期（所有 mod 加载完成后）触发可选模块的加载。
        // 放到 [StaticConstructorOnStartup] 而非 Mod 构造函数，确保 Settings 已就绪、AppDomain 稳定。
        // 运行期改设置不会重跑此处，因此开关变更必须重启客户端才生效。
        static MetaBolicLoadCtrl()
        {
            RimHormonesMod mod = LoadedModManager.GetMod<RimHormonesMod>();
            if (mod != null)
                TryLoad(mod.Content);
        }
    }

    /// <summary>
    /// 代谢扩展模块的契约。主 mod 只认这个接口，不编译引用具体实现，
    /// 从而保持 MetabolicEssential 模块“真正可选”，后期也能整体拆成独立 mod。
    /// </summary>
    public interface IMetabolicModule
    {
        /// <summary>模块被主 mod 在启动期加载时调用。在此注册 Harmony 补丁、初始化逻辑。</summary>
        void Init(ModContentPack pack);
    }

    /// <summary>
    /// 标记一份食谱属于 Metabolic Essential 模块。
    /// 配合 RecipeDef_AvailableNow_MEE_Patch：模块未加载（MetaBolicLoadCtrl.IsLoadedMME==false）时，
    /// 带此标记的食谱在“添加账单”菜单、健康卡手术列表等处全部隐藏且不可制作。
    /// 用法：在食谱 Def 的 &lt;modExtensions&gt; 里加 &lt;li Class="Hormones.MEERecipeMarker" /&gt;
    /// </summary>
    public class MEERecipeMarker : DefModExtension
    {
    }
}
