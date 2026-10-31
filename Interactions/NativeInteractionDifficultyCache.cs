using System;

namespace Gilomx.CupheadBossRoulette
{
    // Native GetMode factories construct a complete boss configuration. Keep
    // one read-only configuration per difficulty for catalog actors whose
    // native implementation never changes that configuration.
    internal sealed class NativeInteractionDifficultyCache<T> where T : class
    {
        private readonly Func<Level.Mode, T> create;
        private readonly T[] values = new T[3];

        internal NativeInteractionDifficultyCache(Func<Level.Mode, T> create)
        {
            this.create = create;
        }

        internal T Get(Level.Mode mode)
        {
            var index = mode == Level.Mode.Easy ? 0 : mode == Level.Mode.Hard ? 2 : 1;
            if (values[index] == null)
                values[index] = create(index == 0 ? Level.Mode.Easy :
                    index == 2 ? Level.Mode.Hard : Level.Mode.Normal);
            return values[index];
        }

        internal void Prepare()
        {
            Get(Level.Mode.Easy);
            Get(Level.Mode.Normal);
            Get(Level.Mode.Hard);
        }
    }
}
