using System.Reflection;
using HarmonyLib;
using Verse;
using Hormones;

namespace MetabolicEssential
{
    /// <summary>
    /// 代谢扩展模块（实验性）。
    /// 实现主 mod 的 Hormones.IMetabolicModule。
    ///
    /// 部署：本 DLL 产出到 mod 根的 MetabolicEssential\ 子目录（不在 Assemblies\ 下），
    /// RimWorld 不会自动加载它。只有在主 mod 设置中勾选开关并重启后，
    /// 主 mod 的 Hormones.MetaBolicLoadCtrl 才会用 Assembly.LoadFrom 反射实例化本类并调用 Init。
    ///
    /// 因此本类**不要**写 [StaticConstructorOnStartup]，也不要在字段初始化器里产生副作用，
    /// 否则会绕过开关在关闭状态下执行。所有副作用一律放进 Init。
    ///
    /// 模块被加载并 Init 成功后，主 mod 的 MetaBolicLoadCtrl 会把 Hormones.MetaBolicLoadCtrl.Active 置为 true，
    /// 从而驱动主 mod 内“默认加载但 inert”的代谢需求（水分/糖/电解质/蛋白质）开始生效。
    ///
    /// 后期可整体分离为独立 mod：把本工程移到独立 mod 目录、加 About.xml，
    /// 并启用下方 Bootstrap 的 [StaticConstructorOnStartup]（此时由自身 mod 自主初始化，
    /// 不再依赖主 mod 的 MetaBolicLoadCtrl）。接口契约 Hormones.IMetabolicModule 保持不变即可平滑迁移。
    /// </summary>
    public class MetabolicEssentialModule : IMetabolicModule
    {
        public void Init(ModContentPack pack)
        {
            // 在此注册本模块的 Harmony 补丁与初始化逻辑。
            // 目前为骨架：PatchAll 会应用本程序集内所有 [HarmonyPatch] 标注（当前为空，无副作用）。
            // 注意：不要把“激活代谢需求”的逻辑写在这里——那由主 mod 的 MetaBolicLoadCtrl 统一置 MetaBolicLoadCtrl.Active。
            var harmony = new Harmony("Lenatuo.metabolicEssential");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // 注册糖↔皮质醇双向反馈逻辑（订阅主 mod 的需求变化事件）。
            MetabolicLogic_Sugar.Register();

            Log.Message("[MetabolicEssential] 代谢扩展模块已初始化（实验性骨架）。");
        }
    }

    // 预留：若本模块未来作为“独立 mod”加载，取消下方注释即可让 RimWorld 在启动时自动初始化。
    // [StaticConstructorOnStartup]
    // public static class Bootstrap
    // {
    //     static Bootstrap()
    //     {
    //         var mod = LoadedModManager.GetMod<MetabolicEssentialMod>();
    //         if (mod != null) new MetabolicEssentialModule().Init(mod.Content);
    //     }
    // }
    //
    // public class MetabolicEssentialMod : Mod
    // {
    //     public MetabolicEssentialMod(ModContentPack content) : base(content) { }
    // }
}
