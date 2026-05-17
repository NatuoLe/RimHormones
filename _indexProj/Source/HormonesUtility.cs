using Verse;

namespace Hormones
{

public static class HormonesUtility
{
    public static HormonesComponent GetHormones(this Pawn pawn)
    {
        return pawn?.GetComp<HormonesComponent>();
    }

    public static float GetHormonesValue(this Pawn pawn)
    {
        return pawn.GetHormones()?.CurLevelPercentage ?? 0f;
    }

    public static HormonesStatus GetHormonesStatus(this Pawn pawn)
    {
        return pawn.GetHormones()?.Status ?? HormonesStatus.Normal;
    }

    public static void AddHormonesReduction(this Pawn pawn, float amount)
    {
        pawn.GetHormones()?.AddHormonesReduction(amount);
    }

    public static bool IsStressed(this Pawn pawn)
    {
        return pawn.GetHormones()?.IsStressed ?? false;
    }

    public static bool IsPanicked(this Pawn pawn)
    {
        return pawn.GetHormones()?.IsPanicked ?? false;
    }

    public static bool IsCalm(this Pawn pawn)
    {
        return pawn.GetHormones()?.IsCalm ?? true;
    }
}

}