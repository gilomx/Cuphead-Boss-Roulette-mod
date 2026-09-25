using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // Persistent, reversible variant of the full HP.1 rejection effect. The
    // native heart keeps following Ms. Chalice, but its shield flag remains
    // false until the timed challenge releases it.
    internal sealed class TimedHpOneSuspendedHeartEffect : MonoBehaviour
    {
        private const float BaseOpacity = 0.50f;

        private Renderer[] renderers;
        private Material[] originalMaterials;
        private Material[] suspendedMaterials;
        private PlayerDamageReceiver receiver;
        private bool initialized;
        private bool restored;

        internal PlayerDamageReceiver Receiver
        {
            get { return receiver; }
        }

        internal void Initialize(Shader shader, LevelPlayerController player)
        {
            if (initialized)
                return;

            initialized = true;
            receiver = player == null ? null : player.damageReceiver;
            renderers = GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[renderers.Length];
            suspendedMaterials = new Material[renderers.Length];

            for (var i = 0; i < renderers.Length; i++)
            {
                var source = renderers[i];
                if (source == null)
                    continue;

                var original = source.sharedMaterial;
                originalMaterials[i] = original;
                var material = shader == null
                    ? original == null ? null : new Material(original)
                    : new Material(shader);
                if (material == null)
                    continue;
                if (original != null && original.mainTexture != null)
                    material.mainTexture = original.mainTexture;
                suspendedMaterials[i] = material;
                source.sharedMaterial = material;
                if (material.HasProperty("_Opacity"))
                    material.SetFloat("_Opacity", BaseOpacity);
            }
        }

        internal void Restore()
        {
            if (!initialized || restored)
                return;
            restored = true;
            for (var i = 0; i < renderers.Length; i++)
            {
                var source = renderers[i];
                if (source == null)
                    continue;
                source.sharedMaterial = originalMaterials[i];
            }
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (suspendedMaterials == null)
                return;
            for (var i = 0; i < suspendedMaterials.Length; i++)
                if (suspendedMaterials[i] != null)
                    Destroy(suspendedMaterials[i]);
        }
    }
}
