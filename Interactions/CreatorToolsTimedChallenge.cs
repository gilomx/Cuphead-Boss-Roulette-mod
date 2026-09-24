using System;
using System.Globalization;

namespace Gilomx.CupheadBossRoulette
{
    internal enum TimedChallengePhase { Idle, Countdown, CountdownExit, Active, Exit }
    // Receives gameplay time only. A completed lease cannot cancel its successor.
    internal sealed class CreatorToolsTimedChallenge
    {
        internal const string HalfDamage = "challenge_half_damage";
        internal const string NoEx = "challenge_no_ex";
        internal const string NoDash = "challenge_no_dash";
        internal const string StiffMode = "challenge_stiff_mode";
        internal const string BlackAndWhite = "challenge_black_and_white";
        internal const string NoBombs = "challenge_no_bombs";
        internal const string NoPeashooter = "challenge_no_peashooter";
        internal const string RgbShift = "challenge_rgb_shift";
        internal const string UpsideDown = "challenge_upside_down";
        internal const string InkRain = "challenge_ink_rain";
        internal const string MiniPlaneOnly = "challenge_mini_plane_only";
        internal static readonly string[] Items = { HalfDamage, NoEx, NoDash, StiffMode, BlackAndWhite, NoBombs, NoPeashooter, RgbShift, UpsideDown, InkRain, MiniPlaneOnly };
        internal static bool Supports(string item) { return item == HalfDamage || item == NoEx || item == NoDash || item == StiffMode || item == InkRain || HasVisualTransition(item) || RequiresPlane(item); }
        internal static bool RequiresPlane(string item) { return UsesForcedPlaneWeapon(item) || item == MiniPlaneOnly; }
        internal static bool UsesForcedPlaneWeapon(string item) { return item == NoBombs || item == NoPeashooter; }
        internal static bool HasVisualTransition(string item) { return item == BlackAndWhite || item == RgbShift || item == UpsideDown; }
        internal static bool SupportsLevel(string item, bool plane) { return Supports(item) && (!RequiresPlane(item) || plane); }
        internal static string ChooseAutomatic(bool plane, Func<string, bool> eligible, Func<int, int> draw)
        {
            string selected = null;
            var count = 0;
            var miniPlaneIncluded = false;
            foreach (var item in Items)
            {
                if (!eligible(item)) continue;
                var miniPlane = plane && (item == NoDash || item == StiffMode);
                if (miniPlane && miniPlaneIncluded) continue;
                if (miniPlane) miniPlaneIncluded = true;
                if (draw(++count) == 0) selected = item;
            }
            return selected;
        }
        internal string Item { get; private set; }
        internal bool PlaneControls { get; private set; }
        internal const int DefaultDuration = 15;
        internal const int MaximumDuration = 120;
        internal const int DefaultCountdown = 3;
        internal const int MaximumCountdown = 30;
        internal const float ExitSeconds = 0.25f;
        internal const float BlackAndWhiteFadeInSeconds = 1.25f;
        internal const float BlackAndWhiteFadeOutSeconds = 0.9f;
        internal const float UpsideDownEntryDelaySeconds = 0.25f;
        internal const float UpsideDownEntrySeconds = 0.45f;
        internal const float UpsideDownExitSeconds = 0.9f;
        private float visualExitBlend;
        internal float EffectElapsed { get; private set; }
        internal TimedChallengePhase Phase { get; private set; }
        internal float PhaseElapsed { get; private set; }
        internal float CountdownRemaining { get; private set; }
        internal float Remaining { get; private set; }
        internal bool Active { get { return Phase == TimedChallengePhase.Active; } }
        internal bool Busy { get { return Phase != TimedChallengePhase.Idle; } }
        internal bool HudVisible { get { return Busy && (Phase != TimedChallengePhase.Exit || PhaseElapsed < ExitSeconds); } }
        internal bool RendersBlackAndWhite { get { return Item == BlackAndWhite && (Active || Phase == TimedChallengePhase.Exit); } }
        internal bool RendersRgbShift { get { return Item == RgbShift && (Active || Phase == TimedChallengePhase.Exit); } }
        internal bool RendersUpsideDown { get { return Item == UpsideDown && (Active || Phase == TimedChallengePhase.Exit); } }
        internal bool CountingDown { get { return Phase == TimedChallengePhase.Countdown || Phase == TimedChallengePhase.CountdownExit; } }
        internal int Revision { get; private set; }

        internal float BlackAndWhiteBlend
        {
            get { return RendersBlackAndWhite ? VisualBlend : 0f; }
        }

        internal float RgbShiftBlend
        {
            get { return RendersRgbShift ? VisualBlend : 0f; }
        }

        internal float UpsideDownBlend
        {
            get { return RendersUpsideDown ? VisualBlend : 0f; }
        }

        private float VisualBlend
        {
            get
            {
                if (!HasVisualTransition(Item) || (!Active && Phase != TimedChallengePhase.Exit)) return 0f;
                // Entry starts with the active countdown. Restoration starts
                // only at zero, retaining both full transition lengths. A short
                // challenge fades back from the strength actually reached.
                var exiting = Phase == TimedChallengePhase.Exit;
                var elapsed = Math.Max(0f, PhaseElapsed - (exiting ? 0f : VisualEntryDelay(Item)));
                var progress = Math.Min(1f, elapsed / (exiting ? VisualExitDuration(Item) : VisualEntryDuration(Item)));
                var eased = progress * progress * (3f - 2f * progress);
                return exiting ? visualExitBlend * (1f - eased) : eased;
            }
        }

        private static float VisualEntryDelay(string item)
        {
            return item == UpsideDown ? UpsideDownEntryDelaySeconds : 0f;
        }

        private static float VisualEntryDuration(string item)
        {
            return item == UpsideDown ? UpsideDownEntrySeconds : BlackAndWhiteFadeInSeconds;
        }

        private static float VisualExitDuration(string item)
        {
            return item == UpsideDown ? UpsideDownExitSeconds : BlackAndWhiteFadeOutSeconds;
        }

        internal float ComposeBlackAndWhiteBlend(float baseBlend)
        {
            // The temporary source cannot erase the equipped challenge's
            // blend, nor write to the player's persistent filter setting.
            return Math.Max(baseBlend, BlackAndWhiteBlend);
        }

        internal float ComposeUpsideDownBlend(float baseBlend)
        {
            // A temporary rotation must not weaken an equipped upside-down
            // challenge that owns the same final-frame render bridge.
            return Math.Max(baseBlend, UpsideDownBlend);
        }

        internal static bool TryDuration(string token, out int duration)
        {
            duration = DefaultDuration;
            return string.IsNullOrEmpty(token) ||
                (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out duration) && duration >= 1 && duration <= MaximumDuration);
        }

        internal static bool TryCountdown(string token, out int seconds)
        {
            seconds = DefaultCountdown;
            return string.IsNullOrEmpty(token) ||
                (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out seconds) && seconds >= 0 && seconds <= MaximumCountdown);
        }

        internal bool Start(int duration, int countdown = DefaultCountdown)
        {
            return Start(HalfDamage, duration, countdown);
        }

        internal bool IsActive(string item) { return Active && Item == item; }

        internal bool Start(string item, int duration, int countdown = DefaultCountdown, bool planeControls = false)
        {
            if (!SupportsLevel(item, planeControls) || Busy || duration < 1 || duration > MaximumDuration || countdown < 0 || countdown > MaximumCountdown)
                return false;
            Revision++;
            Item = item;
            PlaneControls = planeControls;
            visualExitBlend = 0f;
            EffectElapsed = 0f;
            Remaining = duration;
            CountdownRemaining = countdown;
            Enter(countdown > 0 ? TimedChallengePhase.Countdown : TimedChallengePhase.Active);
            return true;
        }

        internal void Advance(float seconds, bool playing)
        {
            if (!playing || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
                return;
            // Carry overshoot through every boundary: frame rate never changes
            // warning length, the full effect duration, or either exit animation.
            while (Busy && seconds > 0f)
            {
                // Ink owns its exit until the last drop, impact and stain have
                // finished. HUD exit still lasts only ExitSeconds.
                if (Phase == TimedChallengePhase.Exit && Item == InkRain)
                {
                    PhaseElapsed += seconds;
                    EffectElapsed += seconds;
                    break;
                }
                var left = Phase == TimedChallengePhase.Countdown ? CountdownRemaining
                    : Active ? Remaining
                    : (Phase == TimedChallengePhase.Exit && HasVisualTransition(Item) ? VisualExitDuration(Item) : ExitSeconds) - PhaseElapsed;
                var step = Math.Min(seconds, Math.Max(0f, left));
                PhaseElapsed += step;
                if (Active || Phase == TimedChallengePhase.Exit) EffectElapsed += step;
                seconds -= step;
                if (Phase == TimedChallengePhase.Countdown) CountdownRemaining = Math.Max(0f, CountdownRemaining - step);
                else if (Active) Remaining = Math.Max(0f, Remaining - step);
                if (step < left) break;
                if (Phase == TimedChallengePhase.Countdown) Enter(TimedChallengePhase.CountdownExit);
                else if (Phase == TimedChallengePhase.CountdownExit) Enter(TimedChallengePhase.Active);
                else if (Active)
                {
                    visualExitBlend = VisualBlend;
                    Enter(TimedChallengePhase.Exit);
                }
                else End();
            }
        }

        private void Enter(TimedChallengePhase phase) { Phase = phase; PhaseElapsed = 0f; }
        internal void CompleteInkRainDrain()
        {
            if (Item == InkRain && Phase == TimedChallengePhase.Exit && PhaseElapsed >= ExitSeconds) End();
        }
        // Display-only copy: ending the gameplay lease must not erase the
        // time shown on the defeat screen. The HUD never advances this copy.
        internal CreatorToolsTimedChallenge Snapshot() { return (CreatorToolsTimedChallenge)MemberwiseClone(); }
        internal void End() { Remaining = 0f; CountdownRemaining = 0f; Enter(TimedChallengePhase.Idle); }
        internal void End(int revision) { if (revision == Revision) End(); }
    }
}
