using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class DevilFireCircleInteractionState : MonoBehaviour
    {
        internal float CameraScale = 1f;
        private static readonly MethodInfo DieMethod = AccessTools.Method(
            typeof(DevilLevelPitchforkSpinnerProjectile), "Die");
        private static readonly MethodInfo StopSoundMethod = AccessTools.Method(
            typeof(DevilLevelPitchforkSpinnerProjectile), "OrbitStopSFX");
        private DevilLevelPitchforkSpinnerProjectile center;
        private float exitAfter;
        private float elapsed;

        internal void Initialize(DevilLevelPitchforkSpinnerProjectile center, float exitAfter)
        {
            this.center = center;
            this.exitAfter = exitAfter;
        }

        private void LateUpdate()
        {
            if (center == null)
            {
                Destroy(gameObject);
                return;
            }
            if (CupheadTime.GlobalSpeed <= 0f)
                return;
            elapsed += Time.unscaledDeltaTime * CupheadTime.GlobalSpeed;
            // It can reverse while native homing is active. Only retire the
            // complete formation after that phase, including a parried center
            // whose renderer is hidden but whose four satellites are alive.
            if (elapsed < exitAfter)
                return;
            var camera = Camera.main;
            if (camera == null)
                return;
            foreach (var renderer in center.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.enabled || renderer.sprite == null)
                    continue;
                var bounds = renderer.bounds;
                var minimum = new Vector2(float.MaxValue, float.MaxValue);
                var maximum = new Vector2(float.MinValue, float.MinValue);
                for (var x = 0; x < 2; x++)
                    for (var y = 0; y < 2; y++)
                    {
                        var point = camera.WorldToViewportPoint(new Vector3(
                            x == 0 ? bounds.min.x : bounds.max.x,
                            y == 0 ? bounds.min.y : bounds.max.y, bounds.center.z));
                        minimum = Vector2.Min(minimum, point);
                        maximum = Vector2.Max(maximum, point);
                    }
                if (maximum.x >= 0f && minimum.x <= 1f && maximum.y >= 0f && minimum.y <= 1f)
                    return;
            }
            DieMethod.Invoke(center, null);
        }

        private void OnDestroy()
        {
            if (center != null)
                StopSoundMethod.Invoke(center, null);
        }
    }
}
