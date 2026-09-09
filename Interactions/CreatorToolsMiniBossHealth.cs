using System;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsMiniBossHealth
    {
        // Only catalog copies call this; native Baroness fights keep their HP.
        internal static int ForCatalog(int nativeHealth)
        {
            return (int)Math.Max(1L, (nativeHealth * 13L + 19L) / 20L);
        }
    }
}
