using System;
using System.Text;

namespace Gilomx.CupheadBossRoulette
{
    // Main-thread projection scratch space. Only changed content becomes a
    // new immutable string; readers can safely retain earlier snapshots.
    internal sealed class CreatorToolsJsonSnapshot
    {
        private readonly StringBuilder builder;
        private char[] characters;
        private string previous;

        internal CreatorToolsJsonSnapshot(int capacity = 4096)
        {
            builder = new StringBuilder(capacity);
            characters = new char[capacity];
        }

        internal StringBuilder Begin()
        {
            builder.Length = 0;
            return builder;
        }

        internal string Complete()
        {
            var length = builder.Length;
            if (characters.Length < length)
                characters = new char[Math.Max(length, characters.Length * 2)];
            builder.CopyTo(0, characters, 0, length);
            if (previous != null && previous.Length == length)
            {
                var same = true;
                for (var i = 0; i < length; i++)
                    if (previous[i] != characters[i])
                    {
                        same = false;
                        break;
                    }
                if (same) return previous;
            }

            // Cuphead's Mono StringBuilder.ToString() may return its own
            // backing string, forcing the next edit to allocate a full copy.
            // CopyTo + a new string leaves that mutable buffer reusable.
            previous = new string(characters, 0, length);
            return previous;
        }
    }
}
