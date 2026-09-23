using System;

namespace Gilomx.CupheadBossRoulette
{
    // Shared by the equipped challenge and temporary interaction. All values
    // retain the accepted RGB motion and 70% native Cagney blur pulse.
    internal static class CreatorToolsRgbShiftVisuals
    {
        internal const float BlurPulseDuration = 2.2f;
        internal static Sample At(float seconds, float blurSeconds)
        {
            var x = (float)Math.Sin(seconds * 10f * 0.73f + (float)Math.PI * 0.5f) * 32f * 0.7f;
            var y = (float)Math.Sin(seconds * 10f) * 32f;
            var pulse = blurSeconds % BlurPulseDuration;
            var blur = pulse < 0.6f ? 1f + pulse : Math.Max(0f, 1.6f - (pulse - 0.6f));
            return new Sample
            {
                RedX = x * 1.2f, RedY = y * 1.2f,
                GreenX = x * 0.6f, GreenY = y * 0.6f,
                BlueX = -x * 0.9f, BlueY = -y * 0.9f,
                Blur = blur * 0.7f
            };
        }

        internal struct Sample
        {
            internal float RedX, RedY, GreenX, GreenY, BlueX, BlueY, Blur;
        }
    }
}
