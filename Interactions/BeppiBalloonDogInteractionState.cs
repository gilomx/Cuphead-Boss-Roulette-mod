using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // Native code owns movement, hitboxes, damage, parry and death animation.
    // This marker only replaces the arena-specific Y limit with camera cleanup.
    internal sealed class BeppiBalloonDogInteractionState : MonoBehaviour
    {
        private static FieldInfo iteratorActor;
        private ClownLevelDogBalloon actor;
        private SpriteRenderer sprite;
        private bool seenInCamera;

        internal void Initialize(ClownLevelDogBalloon actor)
        {
            this.actor = actor;
            sprite = actor.GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (actor == null || CupheadTime.GlobalSpeed <= 0f)
                return;
            var camera = Camera.main;
            if (camera == null || sprite == null || !sprite.enabled || sprite.sprite == null)
                return;
            var bounds = sprite.bounds;
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
            var visible = maximum.x >= 0f && minimum.x <= 1f &&
                maximum.y >= 0f && minimum.y <= 1f;
            if (visible)
                seenInCamera = true;
            else if (seenInCamera)
                Destroy(actor.gameObject);
        }

        internal static void InstallPatches(Harmony harmony)
        {
            foreach (var type in typeof(ClownLevelDogBalloon).GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!type.Name.StartsWith("<move_cr>", StringComparison.Ordinal))
                    continue;
                iteratorActor = AccessTools.Field(type, "$this");
                var move = AccessTools.Method(type, "MoveNext");
                if (iteratorActor == null || move == null)
                    break;
                harmony.Patch(move, transpiler: new HarmonyMethod(
                    AccessTools.Method(typeof(BeppiBalloonDogInteractionState), "AdaptArenaBoundary")));
                return;
            }
            throw new MissingMethodException("ClownLevelDogBalloon.move_cr");
        }

        private static IEnumerable<CodeInstruction> AdaptArenaBoundary(IEnumerable<CodeInstruction> instructions)
        {
            var replacements = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Ldc_R4 || (float)instruction.operand != -560f)
                    continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(BeppiBalloonDogInteractionState), "LowerBoundary"));
                replacements++;
            }
            if (replacements != 1)
                throw new InvalidOperationException("The native Beppi balloon dog boundary contract changed.");
        }

        private static float LowerBoundary(float native, object iterator)
        {
            var dog = iteratorActor.GetValue(iterator) as ClownLevelDogBalloon;
            return dog == null || dog.GetComponentInParent<BeppiBalloonDogInteractionState>() == null
                ? native : float.NegativeInfinity;
        }
    }
}
