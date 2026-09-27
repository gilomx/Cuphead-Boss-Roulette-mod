using System;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsGhostMotionPolicy
    {
        internal const float MaximumOpacity = 0.44f;

        internal static float ResolveEntranceOffsetFactor(
            float elapsed, float duration)
        {
            return SmoothStep(Normalize(elapsed, duration));
        }

        internal static float ResolveExitOffsetFactor(
            float remaining, float duration)
        {
            return SmoothStep(Normalize(remaining, duration));
        }

        internal static float ResolveOpacity(
            float elapsed,
            float lifetime,
            float entranceDuration,
            float exitDuration)
        {
            var entrance = Normalize(elapsed, entranceDuration);
            var exit = Normalize(lifetime - elapsed, exitDuration);
            return Math.Min(entrance, exit) * MaximumOpacity;
        }

        private static float Normalize(float value, float duration)
        {
            if (duration <= 0f)
                return value > 0f ? 1f : 0f;
            return Math.Max(0f, Math.Min(1f, value / duration));
        }

        private static float SmoothStep(float progress)
        {
            return progress * progress * (3f - 2f * progress);
        }
    }
}
