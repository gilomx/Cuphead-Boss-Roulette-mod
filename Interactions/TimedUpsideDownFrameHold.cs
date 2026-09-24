namespace Gilomx.CupheadBossRoulette
{
    // _OnLose releases the gameplay lease immediately afterward. Capture the
    // last rendered angle long enough to transfer it into the defeat return;
    // no timer or gameplay effect survives the failed attempt.
    internal sealed class TimedUpsideDownFrameHold
    {
        internal float Blend { get; private set; }
        internal bool Active { get { return Blend > 0.001f; } }

        internal bool Capture(CreatorToolsTimedChallenge timer)
        {
            if (Active)
                return true;
            if (timer == null || !timer.Busy ||
                timer.Item != CreatorToolsTimedChallenge.UpsideDown)
                return false;

            var blend = timer.UpsideDownBlend;
            if (blend <= 0.001f)
                return false;
            Blend = blend;
            return true;
        }

        internal void Clear()
        {
            Blend = 0f;
        }

        internal float Consume()
        {
            var blend = Blend;
            Blend = 0f;
            return blend;
        }
    }
}
