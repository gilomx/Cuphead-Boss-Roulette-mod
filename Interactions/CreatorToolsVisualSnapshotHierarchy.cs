using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsVisualSnapshotHierarchy
    {
        internal static void RetainBranch<T>(HashSet<T> retained, T leaf, T root,
            Func<T, T> getParent) where T : class
        {
            var cursor = leaf;
            while (cursor != null && retained.Add(cursor))
            {
                if (EqualityComparer<T>.Default.Equals(cursor, root))
                    break;
                cursor = getParent(cursor);
            }
        }
    }
}
