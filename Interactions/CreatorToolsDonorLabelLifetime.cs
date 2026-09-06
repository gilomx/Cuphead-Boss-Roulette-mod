using System;

namespace Gilomx.CupheadBossRoulette
{
    // Visibility can recover during turns and actor handoffs. Only losing the
    // actual target ends the detached label, using real time even while paused.
    internal sealed class CreatorToolsDonorLabelLifetime
    {
        internal const float FadeOutSeconds = 0.6f;
        private float missingSeconds;
        private float fadeStartOpacity;
        private bool fadingOut;

        internal float Opacity { get; private set; }
        internal bool Finished { get; private set; }

        internal void Rebind()
        {
            missingSeconds = 0f;
            fadingOut = false;
            Finished = false;
        }

        internal void Advance(
            bool targetExists, bool targetVisible,
            float requestedOpacity, float unscaledDeltaTime)
        {
            var requested = Math.Max(0f, Math.Min(1f, requestedOpacity));
            if (targetExists)
            {
                Rebind();
                Opacity = targetVisible ? requested : 0f;
                return;
            }
            if (!fadingOut)
            {
                fadingOut = true;
                missingSeconds = 0f;
                fadeStartOpacity = Math.Min(Opacity, requested);
            }
            missingSeconds += Math.Max(0f, unscaledDeltaTime);
            Opacity = Math.Min(requested, fadeStartOpacity *
                Math.Max(0f, 1f - missingSeconds / FadeOutSeconds));
            Finished = Opacity <= 0f;
        }
    }
}
