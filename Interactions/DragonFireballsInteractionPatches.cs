using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsDragonFireballMarker : MonoBehaviour
    {
        internal float CameraScale = 1f;
        internal float CenterY;
        internal float HorizontalSpeed;
        internal DragonLevelMeteor Meteor;
    }

    internal static class DragonFireballsInteractionPatches
    {
        private static readonly Dictionary<Type, FieldInfo> IteratorActors =
            new Dictionary<Type, FieldInfo>();
        private static readonly PropertyInfo LocalDeltaTime =
            typeof(AbstractMonoBehaviour).GetProperty("LocalDeltaTime",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool InstalledSuccessfully { get; private set; }

        internal static void Install(Harmony harmony, Action<string> warning)
        {
            InstalledSuccessfully = false;
            try
            {
                if (LocalDeltaTime == null)
                    throw new MissingMemberException("AbstractMonoBehaviour.LocalDeltaTime");
                PatchIterator(harmony, "moveX_cr", "AdaptHorizontalLimit");
                PatchIterator(harmony, "moveY_cr", "AdaptVerticalTargets");
                PatchIterator(harmony, "rotate_cr", "AdaptRotation");
                InstalledSuccessfully = true;
            }
            catch (Exception exception)
            {
                if (warning != null)
                    warning("Could not adapt the native Dragon fireball path: " + exception);
            }
        }

        private static void PatchIterator(Harmony harmony, string name, string adapter)
        {
            foreach (var type in typeof(DragonLevelMeteor).GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!type.Name.StartsWith("<" + name + ">", StringComparison.Ordinal))
                    continue;
                var owner = AccessTools.Field(type, "$this");
                var move = AccessTools.Method(type, "MoveNext");
                if (owner == null || move == null)
                    break;
                IteratorActors[type] = owner;
                harmony.Patch(move, transpiler: new HarmonyMethod(
                    AccessTools.Method(typeof(DragonFireballsInteractionPatches), adapter)));
                return;
            }
            throw new MissingMethodException("DragonLevelMeteor." + name);
        }

        private static CreatorToolsDragonFireballMarker FindMarker(object iterator)
        {
            FieldInfo owner;
            if (iterator == null || !IteratorActors.TryGetValue(iterator.GetType(), out owner))
                return null;
            var meteor = owner.GetValue(iterator) as DragonLevelMeteor;
            return meteor == null ? null : meteor.GetComponent<CreatorToolsDragonFireballMarker>();
        }

        private static IEnumerable<CodeInstruction> AdaptHorizontalLimit(
            IEnumerable<CodeInstruction> instructions)
        {
            var replacements = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Ldc_R4 || (float)instruction.operand != -840f)
                    continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(DragonFireballsInteractionPatches), "HorizontalLimit"));
                replacements++;
            }
            if (replacements != 1)
                throw new InvalidOperationException("The native Dragon horizontal limit changed.");
        }

        private static float HorizontalLimit(float native, object iterator)
        {
            // Native Die freezes the looping meteor at X=-840 without removing
            // it. An imported meteor instead flies until its rendered bounds
            // leave the actual camera, where the owning state destroys it.
            return FindMarker(iterator) == null ? native : float.NegativeInfinity;
        }

        private static IEnumerable<CodeInstruction> AdaptVerticalTargets(
            IEnumerable<CodeInstruction> instructions)
        {
            var vector = AccessTools.Constructor(typeof(Vector2),
                new[] { typeof(float), typeof(float) });
            var replacements = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Newobj || !Equals(instruction.operand, vector))
                    continue;
                // Both vectors are native end points, after 300 * state has
                // selected its sign. Do not multiply a camera offset by state.
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(DragonFireballsInteractionPatches), "VerticalTarget"));
                replacements++;
            }
            if (replacements != 2)
                throw new InvalidOperationException("The native Dragon vertical targets changed.");
        }

        private static Vector2 VerticalTarget(Vector2 native, object iterator)
        {
            var marker = FindMarker(iterator);
            if (marker != null)
                native.y = marker.CenterY + native.y * marker.CameraScale;
            return native;
        }

        private static IEnumerable<CodeInstruction> AdaptRotation(
            IEnumerable<CodeInstruction> instructions)
        {
            var look = AccessTools.Method(typeof(TransformExtensions), "LookAt2D",
                new[] { typeof(Transform), typeof(Vector3) });
            var replacements = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, look))
                {
                    instruction.operand = AccessTools.Method(
                        typeof(DragonFireballsInteractionPatches), "LookAlongMovement");
                    replacements++;
                }
                yield return instruction;
            }
            if (replacements != 1)
                throw new InvalidOperationException("The native Dragon rotation changed.");
        }

        private static void LookAlongMovement(Transform actor, Vector3 previous)
        {
            var marker = actor.GetComponent<CreatorToolsDragonFireballMarker>();
            if (marker == null)
            {
                TransformExtensions.LookAt2D(actor, previous);
                return;
            }
            // X moves in fixed updates, but TweenPositionY and rotation run
            // every rendered frame. Reconstruct X over the tween's local time
            // interval so a Y-only frame cannot turn the sprite by 90 degrees.
            // Preserve the native reverse tangent: the art points left at zero.
            var delta = (float)LocalDeltaTime.GetValue(marker.Meteor, null);
            if (delta <= 0f)
                return;
            actor.rotation = Quaternion.Euler(0f, 0f, RotationDegrees(
                marker.HorizontalSpeed, delta, actor.position.y - previous.y));
        }

        internal static float RotationDegrees(float horizontalSpeed,
            float localDelta, float verticalDisplacement)
        {
            return (float)(Math.Atan2(-verticalDisplacement,
                horizontalSpeed * localDelta) * 180.0 / Math.PI);
        }
    }
}
