using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeRobotHomingBombSpawnParameters
    {
        internal Vector2 Position;
        internal LevelProperties.Robot.BombBot Properties;
        internal float InitialMovementDuration;
    }

    internal static class NativeRobotHomingBombSpawnPattern
    {
        private const float MinimumViewportY = 0.15f;
        private const float MaximumViewportY = 0.8f;
        private const float MinimumViewportYSeparation = 0.18f;
        private const int RandomPositionAttempts = 24;

        internal static bool TryCreate(
            IList<Vector2> occupiedPositions,
            out NativeRobotHomingBombSpawnParameters parameters,
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

                var properties = LevelProperties.Robot.GetMode(mode);
                if (properties == null || properties.CurrentState == null ||
                    properties.CurrentState.bombBot == null)
                    throw new InvalidOperationException(
                        "Cuphead's native Dr. Kahl homing bomb " +
                        "properties are unavailable.");

                var bomb = properties.CurrentState.bombBot;
                var viewportY = ChooseViewportY(camera, occupiedPositions);
                parameters = new NativeRobotHomingBombSpawnParameters
                {
                    Position = ViewportPosition(camera, 1f, viewportY),
                    Properties = bomb,
                    InitialMovementDuration =
                        bomb.bombInitialMovementDuration.RandomFloat()
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
