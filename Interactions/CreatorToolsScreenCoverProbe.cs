using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsScreenCoverProbe
    {
        private static readonly List<SpriteRenderer> sprites =
            new List<SpriteRenderer>(16);
        private static int frame = -1;
        private static Scene scene;
        private static bool active;

        internal static bool IsActive()
        {
            var currentScene = SceneManager.GetActiveScene();
            if (frame == Time.frameCount && scene == currentScene)
                return active;
            frame = Time.frameCount;
            scene = currentScene;
            active = false;

            // Pirate ink covers the first splat before its alpha ramps up.
            var pirateInkOverlay = PirateLevelSquidInkOverlay.Current;
            if (pirateInkOverlay != null)
            {
                var pirateInkRenderer = pirateInkOverlay.GetComponent<
                    SpriteRenderer>();
                if (pirateInkRenderer != null && pirateInkRenderer.enabled &&
                    pirateInkRenderer.gameObject.activeInHierarchy)
                {
                    active = true;
                    return true;
                }
            }

            // Discover every frame so new effects and child renderers are
            // visible immediately. The non-generic overload avoids Unity's
            // extra typed-array copy; child discovery reuses one scratch list.
            var controllers = UnityEngine.Object.FindObjectsOfType(
                typeof(PlayerScreenEffectController));
            try
            {
                for (var i = 0; i < controllers.Length; i++)
                {
                    var controller = controllers[i] as
                        PlayerScreenEffectController;
                    if (controller == null ||
                        !controller.gameObject.activeInHierarchy)
                        continue;
                    sprites.Clear();
                    controller.GetComponentsInChildren(true, sprites);
                    for (var j = 0; j < sprites.Count; j++)
                    {
                        var sprite = sprites[j];
                        if (sprite == null || !sprite.enabled ||
                            !sprite.gameObject.activeInHierarchy ||
                            sprite.sprite == null || sprite.color.a <= 0.01f)
                            continue;
                        active = true;
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                // Do not retain a controller's children after an early return
                // or failed native query, including across scene teardown.
                sprites.Clear();
            }
        }
    }
}
