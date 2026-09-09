using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Gilomx.CupheadBossRoulette
{
    internal static class DevilFireCircleInteractionPatches
    {
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(DevilLevelPitchforkSpinnerProjectile), "FixedUpdate"),
                transpiler: new HarmonyMethod(AccessTools.Method(
                    typeof(DevilFireCircleInteractionPatches), "AdaptCameraCoordinates")));
        }

        private static IEnumerable<CodeInstruction> AdaptCameraCoordinates(
            IEnumerable<CodeInstruction> instructions)
        {
            var amplitudeCount = 0;
            var boundaryCount = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Ldc_R4)
                    continue;
                var value = (float)instruction.operand;
                string helper;
                if (value == 10f)
                {
                    helper = "VerticalAmplitude";
                    amplitudeCount++;
                }
                else if (value == 1500f)
                {
                    helper = "HorizontalBoundary";
                    boundaryCount++;
                }
                else continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(DevilFireCircleInteractionPatches), helper));
            }
            if (amplitudeCount != 1 || boundaryCount != 1)
                throw new InvalidOperationException("The native Devil spinner coordinate contract changed.");
        }

        private static float VerticalAmplitude(float native, DevilLevelPitchforkSpinnerProjectile actor)
        {
            var state = actor.GetComponentInParent<DevilFireCircleInteractionState>();
            return state == null ? native : native * state.CameraScale;
        }

        private static float HorizontalBoundary(float native, DevilLevelPitchforkSpinnerProjectile actor)
        {
            // The original absolute +/-1500 limit is invalid in scrolling
            // arenas. The marked formation owns cleanup against camera bounds.
            return actor.GetComponentInParent<DevilFireCircleInteractionState>() == null
                ? native : float.PositiveInfinity;
        }
    }
}
