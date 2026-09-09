using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeBeppiBalloonDogSpawnParameters
    {
        internal Vector2 Position;
        internal float CameraScale;
        internal LevelProperties.Clown.HeliumClown Properties;
    }

    internal static class NativeBeppiBalloonDogSpawnPattern
    {
        private const float MinimumViewportXSeparation = 0.18f;
        private const int RandomPositionAttempts = 24;

        internal static bool TryCreate(
            IList<Vector2> occupiedSpawnPositions,
            out NativeBeppiBalloonDogSpawnParameters parameters,
            out string error)
        {
            parameters = null;
            error = null;
            try
            {
                var camera = FindGameplayCamera();
                if (camera == null)
                    throw new InvalidOperationException(
                        "No gameplay camera is active.");

                var mode = Level.CurrentMode;
                if (mode != Level.Mode.Easy &&
                    mode != Level.Mode.Normal &&
                    mode != Level.Mode.Hard)
                    mode = Level.Mode.Normal;

                var properties = LevelProperties.Clown.GetMode(mode);
                if (properties == null || properties.CurrentState == null ||
                    properties.CurrentState.heliumClown == null)
                    throw new InvalidOperationException(
                        "Cuphead's native Beppi balloon dog properties are unavailable.");

                var cameraScale = Mathf.Max(0.01f, camera.orthographicSize / 360f);
                parameters = new NativeBeppiBalloonDogSpawnParameters
                {
                    Position = ChoosePosition(camera, occupiedSpawnPositions),
                    CameraScale = cameraScale,
                    Properties = properties.CurrentState.heliumClown
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        private static Vector2 ChoosePosition(
            Camera camera,
            IList<Vector2> occupiedSpawnPositions)
        {
            var best = UnityEngine.Random.Range(0.1f, 0.9f);
            if (occupiedSpawnPositions == null ||
                occupiedSpawnPositions.Count == 0)
                return ViewportPosition(camera, best);

            var bestDistance = MinimumViewportDistance(
                camera, best, occupiedSpawnPositions);
            for (var i = 1; i < RandomPositionAttempts; i++)
            {
                var candidate = UnityEngine.Random.Range(0.1f, 0.9f);
                var distance = MinimumViewportDistance(
                    camera, candidate, occupiedSpawnPositions);
                if (distance >= MinimumViewportXSeparation)
                    return ViewportPosition(camera, candidate);
                if (distance <= bestDistance)
                    continue;
                best = candidate;
                bestDistance = distance;
            }
            return ViewportPosition(camera, best);
        }

        private static Vector2 ViewportPosition(Camera camera, float viewportX)
        {
            var distanceToGameplayPlane = Mathf.Abs(
                camera.transform.position.z);
            var point = camera.ViewportToWorldPoint(new Vector3(
                viewportX,
                1f,
                distanceToGameplayPlane));
            // Reserve the full native canvas through any initial rotation.
            return new Vector2(point.x, point.y + 210f * camera.orthographicSize / 360f);
        }

        private static float MinimumViewportDistance(
            Camera camera,
            float candidate,
            IList<Vector2> occupiedSpawnPositions)
        {
            var minimum = float.MaxValue;
            for (var i = 0; i < occupiedSpawnPositions.Count; i++)
            {
                var viewport = camera.WorldToViewportPoint(
                    occupiedSpawnPositions[i]);
                var distance = Mathf.Abs(candidate - viewport.x);
                if (distance < minimum)
                    minimum = distance;
            }
            return minimum;
        }

        private static Camera FindGameplayCamera()
        {
            var main = Camera.main;
            if (main != null && main.enabled && main.orthographic)
                return main;

            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (var i = 0; i < cameras.Length; i++)
                if (cameras[i] != null && cameras[i].enabled &&
                    cameras[i].orthographic)
                    return cameras[i];
            return null;
        }
    }
}
