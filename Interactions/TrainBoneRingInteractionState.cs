using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // The native projectile owns motion, targeting, animation and both hitboxes.
    // This parent only owns its dust and retires a completed offscreen attack.
    internal sealed class TrainBoneRingInteractionState : MonoBehaviour
    {
        private static FieldInfo iteratorActor;
        private TrainLevelEngineBossDropperProjectile actor;
        private SpriteRenderer sprite;
        private BoxCollider2D horizontalCollider;
        private float cameraScale;
        private Action<string> logWarning;
        private bool seenInCamera;

        internal void Initialize(TrainLevelEngineBossDropperProjectile actor, float cameraScale,
            Action<string> logWarning)
        {
            this.actor = actor;
            this.cameraScale = cameraScale;
            this.logWarning = logWarning;
            sprite = actor.GetComponent<SpriteRenderer>();
            horizontalCollider = actor.GetComponent<BoxCollider2D>();
        }

        private void LateUpdate()
        {
            if (actor == null || CupheadTime.GlobalSpeed <= 0f)
                return;
            var camera = Camera.main;
            if (camera == null || sprite == null || !sprite.enabled)
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
            else if (seenInCamera && horizontalCollider != null && horizontalCollider.enabled)
                Destroy(actor.gameObject);
        }

        internal static void InstallPatches(Harmony harmony)
        {
            foreach (var type in typeof(TrainLevelEngineBossDropperProjectile).GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!type.Name.StartsWith("<go_cr>", StringComparison.Ordinal))
                    continue;
                iteratorActor = AccessTools.Field(type, "$this");
                var move = AccessTools.Method(type, "MoveNext");
                if (iteratorActor == null || move == null)
                    break;
                harmony.Patch(move, transpiler: new HarmonyMethod(
                    AccessTools.Method(typeof(TrainBoneRingInteractionState), "TrackDust")));
                return;
            }
            throw new MissingMethodException("TrainLevelEngineBossDropperProjectile.go_cr");
        }

        private static IEnumerable<CodeInstruction> TrackDust(IEnumerable<CodeInstruction> instructions)
        {
            var create = AccessTools.Method(typeof(Effect), "Create",
                new[] { typeof(Vector3), typeof(Vector3) });
            var register = AccessTools.Method(typeof(TrainBoneRingInteractionState), "RegisterDust");
            var replacements = 0;
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (!instruction.Calls(create))
                    continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, register);
                replacements++;
            }
            if (replacements != 1)
                throw new InvalidOperationException("The native train bone ring dust contract changed.");
        }

        private static Effect RegisterDust(Effect effect, object iterator)
        {
            var projectile = iteratorActor.GetValue(iterator) as TrainLevelEngineBossDropperProjectile;
            var state = projectile == null ? null :
                projectile.GetComponentInParent<TrainBoneRingInteractionState>();
            // Original train projectiles have no marker and remain untouched.
            if (state == null || effect == null)
                return effect;
            try
            {
                var scale = effect.transform.localScale;
                effect.transform.localScale = new Vector3(
                    scale.x * state.cameraScale, scale.y * state.cameraScale, scale.z);
                effect.transform.SetParent(state.transform, true);
                CreatorToolsInteractionPresentation.MarkInheritedGameplayCameraScale(
                    effect.gameObject, state.cameraScale);
                CreatorToolsInteractionPresentation.BringActorToFront(effect.gameObject);
            }
            catch (Exception exception)
            {
                if (state.logWarning != null)
                    state.logWarning("Could not prepare the train bone ring dust: " + exception.Message);
            }
            return effect;
        }
    }
}
