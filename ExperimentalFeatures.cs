namespace Gilomx.CupheadBossRoulette
{
    internal static class ExperimentalFeatures
    {
        // Completed public-candidate challenge. Enabled for the combined
        // five-challenge acceptance pass.
        internal static readonly bool EnableRgbShiftChallenge = true;

        // Development switch: while true, every challenge-enabled spin uses
        // RGB and keeps the compatible boss selection random.
        internal static readonly bool ForceRgbShiftChallengeForTesting = false;

        // Completed flat 180-degree screen rotation challenge. Enabled for the
        // combined five-challenge acceptance pass.
        internal static readonly bool EnableUpsideDownChallenge = true;
        internal static readonly bool ForceUpsideDownChallengeForTesting =
            false;

        // HP.1 and its final animated art are complete. Enabled for the
        // combined five-challenge acceptance pass.
        internal static readonly bool EnableHpOneChallenge = true;
        internal static readonly bool ForceHpOneChallengeForTesting = false;

        // Ink Rain passed its gameplay and presentation acceptance matrix. It
        // is enabled normally; keep the force switch false in public builds.
        internal static readonly bool EnableInkRainChallenge = true;
        internal static readonly bool ForceInkRainChallengeForTesting = false;

        // Validated implementation with final animated art. Enabled for the
        // combined five-challenge acceptance pass.
        internal static readonly bool EnableHalfDamageChallenge = true;
        internal static readonly bool ForceHalfDamageChallengeForTesting = false;

        // Creator Tools development master switch.
        internal static readonly bool EnableCreatorTools = true;

        internal static bool IsChallengeEnabled(ModifierId id)
        {
            if (id == ModifierId.RgbShift)
                return EnableRgbShiftChallenge;
            if (id == ModifierId.UpsideDown)
                return EnableUpsideDownChallenge;
            if (id == ModifierId.HpOne)
                return EnableHpOneChallenge;
            if (id == ModifierId.InkRain)
                return EnableInkRainChallenge;
            if (id == ModifierId.HalfDamage)
                return EnableHalfDamageChallenge;
            return true;
        }
    }
}
