using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // The native Chalice heart keeps its own entrance animation. This brief
    // character-matched echo gives Cuphead and Mugman an equivalent arrival
    // without starting a real super, spending cards or locking their input.
    internal sealed class CreatorToolsExtraLifeArrivalEffect : MonoBehaviour
    {
        private const float Lifetime = 0.72f;
        private AbstractPlayerController player;
        private SpriteRenderer source;
        private SpriteRenderer echo;
        private float elapsed;

        internal void Initialize(AbstractPlayerController owner)
        {
            player = owner;
            source = FindPlayerRenderer(owner);
            if (source == null)
            {
                Destroy(this);
                return;
            }

            var root = new GameObject("CreatorTools_ExtraLifeArrival");
            root.layer = source.gameObject.layer;
            echo = root.AddComponent<SpriteRenderer>();
            echo.sharedMaterial = source.sharedMaterial;
            echo.sortingLayerID = source.sortingLayerID;
            echo.sortingOrder = source.sortingOrder + 1;
        }

        private void LateUpdate()
        {
            if (player == null || source == null || echo == null)
            {
                Destroy(this);
                return;
            }

            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / Lifetime);
            echo.sprite = source.sprite;
            echo.flipX = source.flipX;
            echo.flipY = source.flipY;
            echo.transform.position = source.transform.position;
            echo.transform.rotation = source.transform.rotation;
            echo.transform.localScale = source.transform.lossyScale *
                Mathf.Lerp(0.94f, 1.32f, progress);
            var opacity = Mathf.Sin(progress * Mathf.PI) * 0.7f;
            echo.color = new Color(
                1f,
                Mathf.Lerp(0.72f, 0.94f, progress),
                Mathf.Lerp(0.78f, 1f, progress),
                opacity);
            if (progress >= 1f)
                Destroy(this);
        }

        private void OnDestroy()
        {
            if (echo != null)
                Destroy(echo.gameObject);
        }

        private static SpriteRenderer FindPlayerRenderer(
            AbstractPlayerController owner)
        {
            var ground = owner as LevelPlayerController;
            if (ground != null && ground.animationController != null)
                return ground.animationController.GetSpriteRenderer();
            var plane = owner as PlanePlayerController;
            if (plane != null && plane.animationController != null)
                return plane.animationController.GetSpriteRenderer();
            return null;
        }
    }
}
