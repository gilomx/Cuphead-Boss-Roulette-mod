using System;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsInteractionBodyScalePolicy
    {
        internal static float Calculate(
            float cameraScale,
            bool usesNativeWorldSize,
            bool usesAircraftSize)
        {
            // Wide ground arenas keep bodies at the player's native world
            // size, including while their camera zoom is still settling.
            if (usesNativeWorldSize)
                return 1f;
            return Math.Max(0.01f, cameraScale) *
                (usesAircraftSize ? 0.8f : 1f);
        }
    }
}
