namespace Gilomx.CupheadBossRoulette
{
    internal static class ExperimentalFeatures
    {
        // Master switch: false removes RGB from the roulette and disables its
        // runtime effect without deleting the completed implementation.
        internal static readonly bool EnableRgbShiftChallenge = false;

        // Development switch: while true, every challenge-enabled spin uses
        // RGB and keeps the compatible boss selection random.
        internal static readonly bool ForceRgbShiftChallengeForTesting = false;

        internal static bool IsChallengeEnabled(ModifierId id)
        {
            return id != ModifierId.RgbShift || EnableRgbShiftChallenge;
        }
    }
}
