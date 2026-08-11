using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class HpOneRejectedPlayerFlashEffect : MonoBehaviour
    {
        private const float Lifetime = 0.38f;

        private AbstractPlayerController target;
        private float elapsed;
        private bool initialized;

        internal void Initialize(AbstractPlayerController player)
        {
            target = player;
            elapsed = 0f;
            initialized = target != null;
        }

        private void LateUpdate()
        {
            if (!initialized)
                return;

            elapsed += Time.deltaTime;
            ForceCurrentTintToGrayscale();
            if (elapsed >= Lifetime)
                Destroy(this);
        }

        private void ForceCurrentTintToGrayscale()
        {
            SpriteRenderer renderer = null;
            var levelPlayer = target as LevelPlayerController;
            if (levelPlayer != null &&
                levelPlayer.animationController != null)
                renderer = levelPlayer.animationController.GetSpriteRenderer();
            else
            {
                var planePlayer = target as PlanePlayerController;
                if (planePlayer != null &&
                    planePlayer.animationController != null)
                    renderer = planePlayer.animationController.GetSpriteRenderer();
            }

            if (renderer == null)
                return;

            var color = renderer.color;
            var luminance = Mathf.Clamp01(
                color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
            renderer.color = new Color(
                luminance, luminance, luminance, color.a);
        }
    }
}