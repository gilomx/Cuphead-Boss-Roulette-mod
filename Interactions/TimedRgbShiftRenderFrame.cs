using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // Overrides last only for the native image-effect call. Native pollen
    // coroutines keep advancing their own values between frames, including
    // when this interaction starts halfway through a pollen effect.
    internal static class TimedRgbShiftRenderFrame
    {
        internal struct ColorState
        {
            internal bool Applied;
            internal Vector2 Red, Green, Blue;
        }
        internal struct BlurState
        {
            internal bool Applied;
            internal float Size;
        }

        internal static ColorState Apply(ChromaticAberrationFilmGrain effect, float blend, float seconds)
        {
            if (effect == null || blend <= 0f) return default(ColorState);
            var state = new ColorState { Applied = true, Red = effect.r, Green = effect.g, Blue = effect.b };
            var sample = CreatorToolsRgbShiftVisuals.At(seconds, seconds);
            effect.r = Vector2.Lerp(state.Red, new Vector2(sample.RedX, sample.RedY), blend);
            effect.g = Vector2.Lerp(state.Green, new Vector2(sample.GreenX, sample.GreenY), blend);
            effect.b = Vector2.Lerp(state.Blue, new Vector2(sample.BlueX, sample.BlueY), blend);
            return state;
        }

        internal static void Restore(ChromaticAberrationFilmGrain effect, ColorState state)
        {
            if (effect == null || !state.Applied) return;
            effect.r = state.Red;
            effect.g = state.Green;
            effect.b = state.Blue;
        }

        internal static BlurState Apply(BlurGamma effect, float blend, float seconds)
        {
            if (effect == null || blend <= 0f) return default(BlurState);
            var state = new BlurState { Applied = true, Size = effect.blurSize };
            effect.blurSize = state.Size + CreatorToolsRgbShiftVisuals.At(seconds, seconds).Blur * Mathf.Clamp01(blend);
            return state;
        }

        internal static void Restore(BlurGamma effect, BlurState state)
        {
            if (effect != null && state.Applied) effect.blurSize = state.Size;
        }
    }
}
