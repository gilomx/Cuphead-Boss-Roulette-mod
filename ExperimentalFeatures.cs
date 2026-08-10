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

        // Completed but dormant flat 180-degree screen rotation challenge.
        // Keep both switches false until its future public activation.
        internal static readonly bool EnableUpsideDownChallenge = false;
        internal static readonly bool ForceUpsideDownChallengeForTesting =
            false;

        internal static bool IsChallengeEnabled(ModifierId id)
        {
            if (id == ModifierId.RgbShift)
                return EnableRgbShiftChallenge;
            if (id == ModifierId.UpsideDown)
                return EnableUpsideDownChallenge;
            return true;
        }
    }
}
