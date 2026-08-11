using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class HpOneRejectedHeartEffect : MonoBehaviour
    {
        private const float Lifetime = 1.15f;
        private const float FadeStart = 0.82f;
        private const float JitterInterval = 0.055f;
        private const float BaseOpacity = 0.50f;

        private Renderer[] renderers;
        private Material[] materials;
        private Vector3 originalLocalPosition;
        private PlayerDamageReceiver receiver;
        private float elapsed;
        private float nextJitterAt;
        private int jitterStep;
        private bool initialized;
        private bool destroyObjectAtEnd;
        private bool finished;

        internal PlayerDamageReceiver Receiver
        {
            get { return receiver; }
        }

        internal void Initialize(Shader shader, LevelPlayerController player)
        {
            Initialize(shader, player, true);
        }

        internal void Initialize(Shader shader, LevelPlayerController player,
            bool destroyAtEnd)
        {
            if (initialized)
                return;

            initialized = true;
            destroyObjectAtEnd = destroyAtEnd;
            originalLocalPosition = transform.localPosition;
            receiver = player == null ? null : player.damageReceiver;
            renderers = GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length];

            for (var i = 0; i < renderers.Length; i++)
            {
                var source = renderers[i];
                if (source == null)
                    continue;

                var originalMaterial = source.sharedMaterial;
                var material = shader == null
                    ? originalMaterial == null
                        ? null
                        : new Material(originalMaterial)
                    : new Material(shader);
                if (material == null)
                    continue;
                if (originalMaterial != null &&
                    originalMaterial.mainTexture != null)
                    material.mainTexture = originalMaterial.mainTexture;
                materials[i] = material;
                source.sharedMaterial = material;
                var sprite = source as SpriteRenderer;
                if (sprite != null)
                    sprite.color = Color.white;
                if (material.HasProperty("_Opacity"))
                    material.SetFloat("_Opacity", BaseOpacity);
            }
        }

        private void Update()
        {
            if (!initialized || finished)
                return;

            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime)
            {
                if (destroyObjectAtEnd)
                    Destroy(gameObject);
                else
                {
                    SetOpacity(0f);
                    transform.localPosition = originalLocalPosition;
                    finished = true;
                }
                return;
            }

            if (elapsed >= nextJitterAt)
            {
                jitterStep++;
                nextJitterAt = elapsed + JitterInterval;
                var x = HashSigned(jitterStep * 17) * 0.055f;
                var y = HashSigned(jitterStep * 31) * 0.014f;
                transform.localPosition = originalLocalPosition +
                    new Vector3(x, y, 0f);
            }

            var flicker = 0.055f * Mathf.Sin(elapsed * 57f) +
                          0.025f * Mathf.Sin(elapsed * 103f);
            var fade = elapsed <= FadeStart
                ? 1f
                : 1f - Mathf.Clamp01(
                    (elapsed - FadeStart) / (Lifetime - FadeStart));
            var opacity = Mathf.Clamp01((BaseOpacity + flicker) * fade);
            SetOpacity(opacity);
        }

        private void SetOpacity(float opacity)
        {
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;
                var source = renderers[i];
                if (source != null)
                {
                    if (source.sharedMaterial != material)
                        source.sharedMaterial = material;
                    var sprite = source as SpriteRenderer;
                    if (sprite != null)
                        sprite.color = Color.white;
                }
                if (material.HasProperty("_Opacity"))
                    material.SetFloat("_Opacity", opacity);
                if (material.HasProperty("_ScanlinePhase"))
                    material.SetFloat("_ScanlinePhase", elapsed * 8f);
            }
        }

        private static float HashSigned(int value)
        {
            var sample = Mathf.Sin(value * 12.9898f) * 43758.5453f;
            return (sample - Mathf.Floor(sample)) * 2f - 1f;
        }

        private void OnDestroy()
        {
            if (materials == null)
                return;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                    Destroy(materials[i]);
            }
        }
    }
}
