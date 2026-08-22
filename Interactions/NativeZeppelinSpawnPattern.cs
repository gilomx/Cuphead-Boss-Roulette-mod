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

        internal static bool TryCreate(
            NativeZeppelinVariant variant,
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
                    Lane = ChooseLane(enemy.spawnString),
                    StopDistance = ChooseStopDistance(enemy),
                    Parryable = ChooseParryable(
                        enemy, variant, ref purpleSpawnCounter)
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static float ChooseLane(string[] patterns)
        {
            if (patterns == null || patterns.Length == 0)
                return 300f;

            var pattern = patterns[
                UnityEngine.Random.Range(0, patterns.Length)];
            var lanes = ParseLanes(pattern);
            if (lanes.Count == 0)
            {
                for (var i = 0; i < patterns.Length; i++)
                {
                    lanes = ParseLanes(patterns[i]);
                    if (lanes.Count > 0)
                        break;
                }
            }
            return lanes.Count == 0
                ? 300f
                : lanes[UnityEngine.Random.Range(0, lanes.Count)];
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
