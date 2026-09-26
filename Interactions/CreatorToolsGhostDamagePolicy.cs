namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsGhostDamagePolicy
    {
        internal static float CalculateMultiplier(
            bool halfDamage, bool ghostActive)
        {
            var multiplier = 1f;
            if (halfDamage)
                multiplier *= 0.5f;
            if (ghostActive)
                multiplier *= 2f;
            return multiplier;
        }
    }
}
