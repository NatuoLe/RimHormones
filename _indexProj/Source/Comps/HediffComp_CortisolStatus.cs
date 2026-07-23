using RimWorld;
using Verse;
using System.Text;

namespace Hormones
{
    /// <summary>
    /// 皮质醇状态显示组件，用于在健康面板显示当前皮质醇浓度
    /// </summary>
    public class HediffComp_CortisolStatus : HediffComp
    {
        public override string CompLabelInBracketsExtra => GetCortisolLabel();

        public override string CompDescriptionExtra => GetCortisolDescription();

        private string GetCortisolLabel()
        {
            var need = Pawn.needs?.TryGetNeed<Need_Cortisol>();
            if (need == null)
                return "未检测";

            var levelEnum = need.GetCortisolLevel();
            string levelText = levelEnum switch
            {
                CortisolLevel.Normal => "正常",
                CortisolLevel.Stressed => "承压",
                CortisolLevel.HighStress => "高压",
                _ => "未知"
            };
            return levelText;
        }

        private string GetCortisolDescription()
        {
            var need = Pawn.needs?.TryGetNeed<Need_Cortisol>();
            if (need == null)
                return "未检测到皮质醇 Need";

            float percentage = need.CurLevelPercentage;
            float level = need.CurLevel;
            var levelEnum = need.GetCortisolLevel();

            StringBuilder sb = new StringBuilder();

            // 浓度和档位
            string levelText = levelEnum switch
            {
                CortisolLevel.Normal => "正常波动",
                CortisolLevel.Stressed => "持续承压",
                CortisolLevel.HighStress => "高压过载",
                _ => "未知"
            };
            sb.AppendLine($"浓度: {level:F0}/10000 ({percentage:P0})");
            sb.AppendLine($"档位: {levelText}");

            // 神经衰弱风险
            if (levelEnum == CortisolLevel.HighStress)
            {
                float neuroProb = need.GetNeurastheniaProbability(percentage);
                sb.AppendLine($"\n高压状态：神经衰弱风险 {neuroProb:P1}/h");
            }
            else if (levelEnum == CortisolLevel.Stressed)
            {
                sb.AppendLine($"\n承压状态：神经衰弱风险较低");
            }
            else
            {
                sb.AppendLine($"\n正常状态：无神经衰弱风险");
            }

            // 是否有神经衰弱
            var neurastheniaDef = DefDatabase<HediffDef>.GetNamed("CortisolNeurasthenia", false);
            if (neurastheniaDef != null && Pawn.health.hediffSet.HasHediff(neurastheniaDef))
            {
                sb.AppendLine($"已患神经衰弱");
            }

            // 当前应激源
            var stressors = need.GetCurrentStressors();
            if (!string.IsNullOrEmpty(stressors))
            {
                sb.AppendLine($"\n当前应激源: {stressors}");
            }

            // 恢复/衰减状态
            sb.Append($"\n变化趋势: {need.GetChangeTrend()}");

            return sb.ToString();
        }
    }
}
