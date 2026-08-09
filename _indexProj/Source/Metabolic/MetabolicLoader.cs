using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

namespace Hormones
{
    /// <summary>
    /// “代谢扩展”可选模块的启动器。
    ///
    /// 部署形态：MetabolicEssential.dll 放在 mod 根的 MetabolicEssential\ 子目录（不在 Assemblies\ 下），
    /// 因此 RimWorld 不会自动加载它。只有玩家在主 mod 设置里勾选开关、且游戏重启后，
    /// 本类才在启动期用 Assembly.LoadFrom 显式加载该程序集，并反射调用其 IMetabolicModule.Init。
    ///
    /// 开关关闭 → 程序集完全不被载入（零副作用；主 mod 里的代谢需求保持 inert 满值）。
    /// 加载只在游戏启动期发生一次（见 MetabolicBootstrap），运行期改设置需重启客户端。
    /// </summary>
    public static class MetabolicLoader
    {
        public const string ModuleDir = "MetabolicEssential";
        public const string ModuleDll = "MetabolicEssential.dll";

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

        public static void TryLoad(ModContentPack pack)
        {
            IsLoaded = false;
            MetabolicState.IsLoadedMME = false;
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
                MetabolicState.Active = true;        // 驱动主 mod 内的代谢需求开始消耗
                MetabolicState.IsLoadedMME = true;   // 驱动四个代谢需求在需求栏显示
                Log.Message("[RimHormones] Metabolic Essential 模块已初始化。");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimHormones] 初始化 Metabolic Essential 模块失败：{ex}");
                IsLoaded = false;
            }
        }
    }
}
