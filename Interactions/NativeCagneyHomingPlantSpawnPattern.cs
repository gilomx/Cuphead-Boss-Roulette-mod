using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeCagneyHomingPlantSpawnParameters
    {
        internal Vector2 Position;
        internal LevelProperties.Flower Properties;
        internal bool UseVirtualGroundOnly;
    }

    internal static class NativeCagneyHomingPlantSpawnPattern
    {
        private const float MinimumViewportXSeparation = 0.18f;
        private const int RandomPositionAttempts = 24;

        internal static bool TryCreate(
            IList<Vector2> occupiedPositions,
            out NativeCagneyHomingPlantSpawnParameters parameters,
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

                var properties = LevelProperties.Flower.GetMode(mode);
                if (properties == null || properties.CurrentState == null ||
                    properties.CurrentState.enemyPlants == null)
                    throw new InvalidOperationException(
                        "Cuphead's native Cagney plant properties are unavailable.");

                parameters = new NativeCagneyHomingPlantSpawnParameters
                {
                    Position = ChoosePosition(camera, occupiedPositions),
                    Properties = properties,
                    UseVirtualGroundOnly =
                        UnityEngine.Object.FindObjectOfType<
                            PlanePlayerController>() != null
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
            IList<Vector2> occupiedPositions)
        {
            var best = UnityEngine.Random.Range(0f, 1f);
            if (occupiedPositions == null || occupiedPositions.Count == 0)
                return ViewportPosition(camera, best);

            var bestDistance = MinimumViewportDistance(
                camera, best, occupiedPositions);
            for (var i = 1; i < RandomPositionAttempts; i++)
            {
                var candidate = UnityEngine.Random.Range(0f, 1f);
                var distance = MinimumViewportDistance(
                    camera, candidate, occupiedPositions);
                if (distance >= MinimumViewportXSeparation)
                    return ViewportPosition(camera, candidate);
                if (distance <= bestDistance)
                    continue;
                best = candidate;
                bestDistance = distance;
            }
            return ViewportPosition(camera, best);
        }

        private static Vector2 ViewportPosition(
            Camera camera,
            float viewportX)
        {
            var distanceToGameplayPlane = Mathf.Abs(
                camera.transform.position.z);
            var point = camera.ViewportToWorldPoint(new Vector3(
                viewportX,
                1f,
                distanceToGameplayPlane));
            return new Vector2(point.x, point.y);
        }

        private static float MinimumViewportDistance(
            Camera camera,
            float candidate,
            IList<Vector2> occupiedPositions)
        {
            var minimum = float.MaxValue;
            for (var i = 0; i < occupiedPositions.Count; i++)
            {
                var viewport = camera.WorldToViewportPoint(
                    occupiedPositions[i]);
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
