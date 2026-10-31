using System;

namespace Gilomx.CupheadBossRoulette
{
    internal struct NativeChallengePromptLayoutInput
    {
        internal string Text;
        internal object Target;
        internal object Canvas;
        internal object Font;
        internal int FontSize;
        internal int FontStyle;
        internal bool BestFit;
        internal int BestFitMinimum;
        internal int BestFitMaximum;
        internal float CanvasScale;
        internal int ScreenWidth;
        internal int ScreenHeight;
    }

    // Stores only the last successfully applied layout. Queries allocate no
    // strings or collections; Unity supplies the current visual inputs.
    internal sealed class NativeChallengePromptLayoutCache
    {
        private bool applied;
        private NativeChallengePromptLayoutInput previous;
        private float appliedX;
        private float appliedWidth;

        internal static bool CanPrepareTemplate(bool manualActive, bool mapAvailable)
        {
            // Prepare on the map before its native Help template disappears.
            // A remembered but inactive/incompatible manual challenge does
            // not justify repeatedly looking for that template in a battle.
            return manualActive || mapAvailable;
        }

        internal bool NeedsApply(NativeChallengePromptLayoutInput input,
            string currentText, float currentX, float currentWidth)
        {
            return !applied || input.Text != currentText ||
                input.Text != previous.Text ||
                !ReferenceEquals(input.Target, previous.Target) ||
                !ReferenceEquals(input.Canvas, previous.Canvas) ||
                !ReferenceEquals(input.Font, previous.Font) ||
                input.FontSize != previous.FontSize ||
                input.FontStyle != previous.FontStyle ||
                input.BestFit != previous.BestFit ||
                input.BestFitMinimum != previous.BestFitMinimum ||
                input.BestFitMaximum != previous.BestFitMaximum ||
                input.CanvasScale != previous.CanvasScale ||
                input.ScreenWidth != previous.ScreenWidth ||
                input.ScreenHeight != previous.ScreenHeight ||
                currentX != appliedX || currentWidth != appliedWidth;
        }

        internal void MarkApplied(NativeChallengePromptLayoutInput input,
            float x, float width)
        {
            previous = input;
            appliedX = x;
            appliedWidth = width;
            applied = true;
        }

        internal void Reset()
        {
            applied = false;
            previous = new NativeChallengePromptLayoutInput();
        }
    }
}
