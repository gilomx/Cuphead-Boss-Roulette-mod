using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeFrogsFireflySpawnParameters
    {
        internal Vector2 Position;
        internal Vector2 InitialTarget;
        internal float Speed;
        internal int Health;
        internal float FollowDelay;
        internal float FollowTime;
        internal float FollowDistance;
        internal float InvincibleDuration;
    }

    internal static class NativeFrogsFireflySpawnPattern
    {
        private const float MinimumViewportY = 0.2f;
        private const float MaximumViewportY = 0.72f;
        private const float MinimumInitialTargetViewportX = 0.78f;
        private const float MaximumInitialTargetViewportX = 0.84f;
        private const float MinimumViewportYSeparation = 0.18f;
        private const int RandomPositionAttempts = 24;

        internal static bool TryCreate(
            IList<Vector2> occupiedPositions,
            out NativeFrogsFireflySpawnParameters parameters,
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

                var properties = LevelProperties.Frogs.GetMode(mode);
                if (properties == null || properties.CurrentState == null ||
                    properties.CurrentState.tallFireflies == null)
                    throw new InvalidOperationException(
                        "Cuphead's native Ribby and Croaks firefly " +
                        "properties are unavailable.");

                var fireflies = properties.CurrentState.tallFireflies;
                var viewportY = ChooseViewportY(camera, occupiedPositions);
                var initialTargetViewportX = UnityEngine.Random.Range(
                    MinimumInitialTargetViewportX,
                    MaximumInitialTargetViewportX);
                parameters = new NativeFrogsFireflySpawnParameters
                {
                    Position = ViewportPosition(camera, 1f, viewportY),
                    InitialTarget = ViewportPosition(
                        camera,
                        initialTargetViewportX,
                        viewportY),
                    Speed = fireflies.speed,
                    Health = fireflies.hp,
                    FollowDelay = fireflies.followDelay,
                    FollowTime = fireflies.followTime,
                    FollowDistance = fireflies.followDistance,
                    InvincibleDuration = fireflies.invincibleDuration
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        private static float ChooseViewportY(
            Camera camera,
            IList<Vector2> occupiedPositions)
        {
            var best = UnityEngine.Random.Range(
                MinimumViewportY,
                MaximumViewportY);
            if (occupiedPositions == null || occupiedPositions.Count == 0)
                return best;

            var bestDistance = MinimumDistance(
                camera, best, occupiedPositions);
            for (var i = 1; i < RandomPositionAttempts; i++)
            {
                var candidate = UnityEngine.Random.Range(
                    MinimumViewportY,
                    MaximumViewportY);
                var distance = MinimumDistance(
                    camera, candidate, occupiedPositions);
                if (distance >= MinimumViewportYSeparation)
                    return candidate;
                if (distance <= bestDistance)
                    continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static float MinimumDistance(
            Camera camera,
            float candidate,
            IList<Vector2> occupiedPositions)
        {
            var minimum = float.MaxValue;
            for (var i = 0; i < occupiedPositions.Count; i++)
            {
                var viewport = camera.WorldToViewportPoint(
                    occupiedPositions[i]);
                var distance = Mathf.Abs(candidate - viewport.y);
                if (distance < minimum)
                    minimum = distance;
            }
            return minimum;
        }

        private static Vector2 ViewportPosition(
            Camera camera,
            float viewportX,
            float viewportY)
        {
            var distanceToGameplayPlane = Mathf.Abs(
                camera.transform.position.z);
            var point = camera.ViewportToWorldPoint(new Vector3(
                viewportX,
                viewportY,
                distanceToGameplayPlane));
            return new Vector2(point.x, point.y);
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
