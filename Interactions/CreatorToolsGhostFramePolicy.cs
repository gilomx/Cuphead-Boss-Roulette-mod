namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsGhostFramePolicy
    {
        internal static bool CanAdoptSourceFrame(
            bool activeInHierarchy,
            bool rendererEnabled,
            bool hasSprite,
            float sourceAlpha)
        {
            return activeInHierarchy && rendererEnabled && hasSprite &&
                sourceAlpha > 0.001f;
        }

        internal static bool ShouldDisplayCachedFrame(
            bool hasCachedSprite,
            bool sourceVisible,
            float effectOpacity)
        {
            // Source visibility is deliberately not required. Cuphead briefly
            // hides its player renderer during Chalice jumps, plane bomb
            // transformations and bomb explosions.
            return hasCachedSprite && effectOpacity > 0f;
        }

        internal static bool MayUseAlternateRenderer(bool groundPresentation)
        {
            // Ground jump animations can expose isolated Chalice body parts.
            // Plane transformations, instead, use alternate renderers that
            // represent the complete bomb/transition frame.
            return !groundPresentation;
        }

        internal static float ResolveGhostAlpha(
            float sourceAlpha, float effectOpacity)
        {
            // The player may fade to zero as part of a transition. The helper
            // keeps its own opacity while retaining the source RGB tint.
            if (effectOpacity < 0f)
                return 0f;
            if (effectOpacity > 1f)
                return 1f;
            return effectOpacity;
        }
    }
}
