namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsHelp
    {
        internal const string ExtraLife = "help_extra_life";
        internal const string Ghost = "help_ghost";

        internal static bool Supports(string item)
        {
            return item == ExtraLife || item == Ghost;
        }
    }
}
