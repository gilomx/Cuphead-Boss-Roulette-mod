using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class NativeZeppelinSpawnParameters
    {
        internal LevelProperties.FlyingBlimp Properties;
        internal float Lane;
        internal float StopDistance;
        internal bool Parryable;
    }

    internal static class NativeZeppelinSpawnPattern
    {
        private const float MinimumAttackStop = 390f;
        private const float MaximumAttackStop = 535f;
        private const float MinimumRightwardAdjustment = 55f;
        private const float MaximumRightwardAdjustment = 105f;
        private const float MinimumSafeLane = 120f;
        private const float MaximumSafeLane = 610f;
        private const float MinimumLaneSeparation = 165f;
        private const int RandomLaneAttempts = 32;

        internal static bool TryCreate(
            NativeZeppelinVariant variant,
            IList<float> occupiedLanes,
            ref int purpleSpawnCounter,
            out NativeZeppelinSpawnParameters parameters,
            out string error)
        {
            parameters = null;
            error = null;
            try
            {
                var mode = Level.CurrentMode;
                if (mode != Level.Mode.Easy &&
                    mode != Level.Mode.Normal &&
                    mode != Level.Mode.Hard)
                    mode = Level.Mode.Normal;

                var properties = LevelProperties.FlyingBlimp.GetMode(mode);
                if (properties == null || properties.CurrentState == null ||
                    properties.CurrentState.enemy == null)
                    throw new InvalidOperationException(
                        "Cuphead's native Hilda enemy properties are unavailable.");

                var enemy = properties.CurrentState.enemy;
                parameters = new NativeZeppelinSpawnParameters
                {
                    Properties = properties,
                    Lane = ChooseLane(occupiedLanes),
                    StopDistance = ChooseStopDistance(enemy),
                    Parryable = ChooseParryable(
                        enemy, variant, ref purpleSpawnCounter)
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.ToString();
                return false;
            }
        }

        private static float ChooseLane(IList<float> occupiedLanes)
        {
            if (occupiedLanes == null || occupiedLanes.Count == 0)
                return UnityEngine.Random.Range(
                    MinimumSafeLane, MaximumSafeLane);

            var bestLane = MinimumSafeLane;
            var bestDistance = float.MinValue;
            for (var i = 0; i < RandomLaneAttempts; i++)
            {
                var candidate = UnityEngine.Random.Range(
                    MinimumSafeLane, MaximumSafeLane);
                var distance = MinimumDistance(candidate, occupiedLanes);
                if (distance >= MinimumLaneSeparation)
                    return candidate;
                if (distance <= bestDistance)
                    continue;
                bestLane = candidate;
                bestDistance = distance;
            }
            return bestLane;
        }

        private static float MinimumDistance(
            float candidate,
            IList<float> occupiedLanes)
        {
            var minimum = float.MaxValue;
            for (var i = 0; i < occupiedLanes.Count; i++)
            {
                var distance = Mathf.Abs(candidate - occupiedLanes[i]);
                if (distance < minimum)
                    minimum = distance;
            }
            return minimum;
        }

        private static float ChooseStopDistance(
            LevelProperties.FlyingBlimp.Enemy enemy)
        {
            var nativeStop = enemy.stopDistance.RandomFloat();
            var adjustedStop = nativeStop + UnityEngine.Random.Range(
                MinimumRightwardAdjustment,
                MaximumRightwardAdjustment);
            return Mathf.Clamp(
                adjustedStop,
                MinimumAttackStop,
                MaximumAttackStop);
        }

        private static List<float> ParseLanes(string pattern)
        {
            var lanes = new List<float>();
            if (string.IsNullOrEmpty(pattern))
                return lanes;

            var tokens = pattern.Split(',');
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (token.Length == 0 ||
                    token[0] == 'D' || token[0] == 'd')
                    continue;

                var values = token.Split('-');
                for (var j = 0; j < values.Length; j++)
                {
                    float lane;
                    if (float.TryParse(
                        values[j],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out lane))
                        lanes.Add(lane);
                }
            }
            return lanes;
        }

        private static bool ChooseParryable(
            LevelProperties.FlyingBlimp.Enemy enemy,
            NativeZeppelinVariant variant,
            ref int purpleSpawnCounter)
        {
            if (variant != NativeZeppelinVariant.Purple)
                return false;

            if (purpleSpawnCounter >= enemy.APinkOccurance.RandomFloat())
            {
                purpleSpawnCounter = 0;
                return true;
            }

            purpleSpawnCounter++;
            return false;
        }
    }
}
