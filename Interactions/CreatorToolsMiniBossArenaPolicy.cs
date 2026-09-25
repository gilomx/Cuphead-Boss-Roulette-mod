namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsMiniBossArenaPolicy
    {
        internal static bool SupportsScreenEdgeFloor(Levels level)
        {
            return level == Levels.Dragon;
        }

        internal static bool UsesViewportFloor(
            Levels level,
            bool hasAircraftPlayer,
            bool usesWaterFloor,
            bool usesDevilLowerArena)
        {
            return !usesWaterFloor &&
                (hasAircraftPlayer || level == Levels.Airplane ||
                 SupportsScreenEdgeFloor(level) || usesDevilLowerArena);
        }
    }
}
