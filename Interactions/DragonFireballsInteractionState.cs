using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class DragonFireballsInteractionState : MonoBehaviour
    {
        private const float NativeIdleSeconds = 16f / 24f;
        private const float NativeMeteorStartSeconds = 8f / 24f;
        private const float NativeAnticipationSeconds = 8f / 24f;
        private const float NativeAnticipationEndSeconds = 4f / 24f;
        private const float NativeAttackSeconds = 9f / 24f;
        private const float NativeAttackEndSeconds = 8f / 24f;
        private const float ArmAttackSeconds =
            NativeIdleSeconds + NativeMeteorStartSeconds;
        private const float StopAnticipationSeconds =
            ArmAttackSeconds + NativeAnticipationSeconds;
        private const float AttackStartSeconds =
            StopAnticipationSeconds + NativeAnticipationEndSeconds;
        private const float FireballsReleaseSeconds =
            AttackStartSeconds + 7f / 24f;
        private const float ExitStartSeconds =
            AttackStartSeconds + NativeAttackSeconds + NativeAttackEndSeconds;
        private const float EntranceDurationSeconds = 20f / 24f;
        private const float ExitDurationSeconds = 20f / 24f;
        private const float MaximumLifetimeSeconds = 24f;
        private const float OffscreenCleanupMarginPixels = 220f;
        private const string MeteorTriggerName = "OnMeteor";
        private const string MeteorStartSound =
            "level_dragon_left_dragon_meteor_start";
        private const string MeteorAnticipationSound =
            "level_dragon_left_dragon_meteor_anticipation_loop";
        private const string MeteorAttackSound =
            "level_dragon_left_dragon_meteor_attack";
        private const string MeteorSpitSound =
            "level_dragon_left_dragon_meteor_spit";

        private GameObject dragonRoot;
        private Animator animator;
        private SpriteRenderer[] bodyRenderers;
        private bool[] bodyRendererVisibility;
        private Transform mouthRoot;
        private DragonLevelMeteor meteorTemplate;
        private LevelProperties.Dragon.Meteor meteorProperties;
        private Vector3 fallbackMouthLocalPosition;
        private Vector3 offscreenPosition;
        private Vector3 attackPosition;
        private Vector3 initialCameraPosition;
        private CreatorToolsDonorLabel donorLabel;
        private DragonLevelMeteor upperFireball;
        private DragonLevelMeteor lowerFireball;
        private Action<string> logWarning;
        private string donor;
        private string giftImagePath = string.Empty;
        private float cameraScale = 1f;
        private float elapsed;
        private float upperOffscreenElapsed;
        private float lowerOffscreenElapsed;
        private bool meteorStartSoundPlayed;
        private bool attackArmed;
        private bool anticipationStopped;
        private bool attackSoundPlayed;
        private bool fireballsReleased;
        private bool bodyHidden;
        private bool cleaningUp;

        internal void Initialize(
            GameObject dragonRoot,
            Animator animator,
            SpriteRenderer[] bodyRenderers,
            bool[] bodyRendererVisibility,
            Transform mouthRoot,
            DragonLevelMeteor meteorTemplate,
            LevelProperties.Dragon.Meteor meteorProperties,
            Vector3 fallbackMouthLocalPosition,
            Vector3 offscreenPosition,
            Vector3 attackPosition,
            float cameraScale,
            string donor,
            string giftImagePath,
            Action<string> logWarning)
        {
            this.dragonRoot = dragonRoot;
            this.animator = animator;
            this.bodyRenderers = bodyRenderers;
            this.bodyRendererVisibility = bodyRendererVisibility;
            this.mouthRoot = mouthRoot;
            this.meteorTemplate = meteorTemplate;
            this.meteorProperties = meteorProperties;
            this.fallbackMouthLocalPosition = fallbackMouthLocalPosition;
            this.offscreenPosition = offscreenPosition;
            this.attackPosition = attackPosition;
            this.cameraScale = Mathf.Max(0.01f, cameraScale);
            this.donor = donor;
            this.giftImagePath = giftImagePath ?? string.Empty;
            this.logWarning = logWarning;
            donorLabel = dragonRoot == null
                ? null
                : dragonRoot.GetComponent<CreatorToolsDonorLabel>();
            var camera = FindGameplayCamera();
            initialCameraPosition = camera == null
                ? Vector3.zero
                : camera.transform.position;
            if (dragonRoot != null)
                dragonRoot.transform.position = offscreenPosition;
            ApplyBodyVisibility();
        }

        internal bool TryGetActorPosition(out Vector2 position)
        {
            if (upperFireball != null)
            {
                position = upperFireball.transform.position;
                return true;
            }
            if (lowerFireball != null)
            {
                position = lowerFireball.transform.position;
                return true;
            }
            if (dragonRoot != null)
            {
                position = dragonRoot.transform.position;
                return true;
            }
            position = Vector2.zero;
            return false;
        }

        private void Update()
        {
            if (cleaningUp)
                return;

            var speed = Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (animator != null && !bodyHidden)
                animator.speed = speed;
            if (speed <= 0f)
                return;

            var delta = Time.unscaledDeltaTime * speed;
            elapsed += delta;
            UpdateBodyMotion();

            if (!meteorStartSoundPlayed && elapsed >= NativeIdleSeconds)
            {
                meteorStartSoundPlayed = true;
                PlaySound(MeteorStartSound);
            }
            if (!attackArmed && elapsed >= ArmAttackSeconds)
            {
                attackArmed = true;
                if (animator != null)
                    animator.SetTrigger(MeteorTriggerName);
                PlaySound(MeteorAnticipationSound);
            }
            if (!anticipationStopped && elapsed >= StopAnticipationSeconds)
            {
                anticipationStopped = true;
                StopSound(MeteorAnticipationSound);
            }
            if (!attackSoundPlayed && elapsed >= AttackStartSeconds)
            {
                attackSoundPlayed = true;
                PlaySound(MeteorAttackSound);
            }
            if (!fireballsReleased && elapsed >= FireballsReleaseSeconds)
                ReleaseFireballs();
            if (!bodyHidden && elapsed >=
                    ExitStartSeconds + ExitDurationSeconds)
                HideDragon();

            if (bodyHidden)
            {
                CleanupIfOutside(ref upperFireball,
                    ref upperOffscreenElapsed, delta);
                CleanupIfOutside(ref lowerFireball,
                    ref lowerOffscreenElapsed, delta);
            }

            if (fireballsReleased && upperFireball == null &&
                lowerFireball == null && bodyHidden)
            {
                Destroy(gameObject);
                return;
            }

            if (elapsed >= MaximumLifetimeSeconds)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            ApplyBodyVisibility();
        }

        private void UpdateBodyMotion()
        {
            if (dragonRoot == null || bodyHidden)
                return;

            var camera = FindGameplayCamera();
            var cameraShift = camera == null
                ? Vector3.zero
                : camera.transform.position - initialCameraPosition;
            cameraShift.z = 0f;
            var hidden = offscreenPosition + cameraShift;
            var attack = attackPosition + cameraShift;

            if (elapsed < EntranceDurationSeconds)
            {
                var progress = Mathf.SmoothStep(
                    0f, 1f, Mathf.Clamp01(elapsed / EntranceDurationSeconds));
                dragonRoot.transform.position = Vector3.Lerp(
                    hidden, attack, progress);
                return;
            }
            if (elapsed <= ExitStartSeconds)
            {
                dragonRoot.transform.position = attack;
                return;
            }

            var exitProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    (elapsed - ExitStartSeconds) / ExitDurationSeconds));
            dragonRoot.transform.position = Vector3.Lerp(
                attack, hidden, exitProgress);
        }

        private void ReleaseFireballs()
        {
            fireballsReleased = true;
            try
            {
                if (dragonRoot == null || meteorTemplate == null ||
                    meteorProperties == null)
                    throw new InvalidOperationException(
                        "The native Dragon fireball attack is incomplete.");

                var mouthPosition = mouthRoot == null
                    ? dragonRoot.transform.TransformPoint(
                        fallbackMouthLocalPosition)
                    : mouthRoot.position;
                upperFireball = CreateFireball(
                    mouthPosition, DragonLevelMeteor.State.Up,
                    "CreatorTools_NativeDragonFireball_Up");
                lowerFireball = CreateFireball(
                    mouthPosition, DragonLevelMeteor.State.Down,
                    "CreatorTools_NativeDragonFireball_Down");
                PlaySound(MeteorSpitSound);
                TransferPresentationToFireball();
            }
            catch (Exception exception)
            {
                Warn("Could not release the Dragon's native fireballs: ",
                    exception);
                DestroyFireball(ref upperFireball);
                DestroyFireball(ref lowerFireball);
                Destroy(gameObject);
            }
        }

        private DragonLevelMeteor CreateFireball(
            Vector3 position,
            DragonLevelMeteor.State state,
            string actorName)
        {
            var fireball = meteorTemplate.Create(
                position,
                new DragonLevelMeteor.Properties(
                    meteorProperties.timeY,
                    meteorProperties.speedX,
                    state));
            if (fireball == null)
                throw new InvalidOperationException(
                    "Cuphead did not create a native Dragon fireball.");

            fireball.gameObject.name = actorName;
            CreatorToolsInteractionPresentation.MatchGameplayCameraScale(
                fireball.gameObject, logWarning);
            CreatorToolsInteractionPresentation.BringActorToFront(
                fireball.gameObject);
            fireball.gameObject.SetActive(true);
            return fireball;
        }

        private void TransferPresentationToFireball()
        {
            if (upperFireball == null)
                return;
            var anchor = FindLabelAnchor(upperFireball.gameObject);
            if (donorLabel != null && donorLabel.RebindTo(
                    upperFireball.gameObject, anchor, 0.2f))
                return;

            CreatorToolsInteractionPresentation.PrepareActor(
                upperFireball.gameObject, anchor, donor, logWarning);
            var label = upperFireball.gameObject.GetComponent<
                CreatorToolsDonorLabel>();
            if (label != null && !string.IsNullOrEmpty(giftImagePath))
                label.SetGiftImage(giftImagePath);
        }

        private void CleanupIfOutside(
            ref DragonLevelMeteor fireball,
            ref float offscreenElapsed,
            float delta)
        {
            if (fireball == null)
                return;
            if (IsFullyOutsideGameplayView(
                    fireball.gameObject,
                    OffscreenCleanupMarginPixels * cameraScale))
                offscreenElapsed += delta;
            else
                offscreenElapsed = 0f;
            if (offscreenElapsed < 0.6f)
                return;
            DestroyFireball(ref fireball);
            offscreenElapsed = 0f;
        }

        private static void DestroyFireball(ref DragonLevelMeteor fireball)
        {
            if (fireball != null)
                Destroy(fireball.gameObject);
            fireball = null;
        }

        private void HideDragon()
        {
            bodyHidden = true;
            if (animator != null)
                animator.enabled = false;
            ApplyBodyVisibility();
        }

        private void ApplyBodyVisibility()
        {
            if (bodyRenderers == null)
                return;
            for (var i = 0; i < bodyRenderers.Length; i++)
            {
                var renderer = bodyRenderers[i];
                if (renderer == null)
                    continue;
                var originallyVisible = bodyRendererVisibility == null ||
                    i >= bodyRendererVisibility.Length ||
                    bodyRendererVisibility[i];
                renderer.enabled = !bodyHidden && originallyVisible;
            }
        }

        private static SpriteRenderer FindLabelAnchor(GameObject actor)
        {
            if (actor == null)
                return null;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer best = null;
            var bestArea = -1f;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                var size = renderer.sprite.bounds.size;
                var area = Mathf.Abs(size.x * size.y);
                if (area <= bestArea)
                    continue;
                best = renderer;
                bestArea = area;
            }
            return best;
        }

        private static bool IsFullyOutsideGameplayView(
            GameObject actor, float margin)
        {
            var camera = FindGameplayCamera();
            if (actor == null || camera == null)
                return false;
            var distance = Mathf.Abs(
                camera.transform.position.z - actor.transform.position.z);
            var bottomLeft = camera.ViewportToWorldPoint(
                new Vector3(0f, 0f, distance));
            var topRight = camera.ViewportToWorldPoint(
                new Vector3(1f, 1f, distance));
            var bounds = BaronessHeadTossInteractionState.VisibleBounds(actor);
            if (!bounds.HasValue)
                return false;
            var value = bounds.Value;
            return value.max.x < bottomLeft.x - margin ||
                value.min.x > topRight.x + margin ||
                value.max.y < bottomLeft.y - margin ||
                value.min.y > topRight.y + margin;
        }

        internal static Camera FindGameplayCamera()
        {
            return BaronessHeadTossInteractionState.FindGameplayCamera();
        }

        private static void PlaySound(string sound)
        {
            try { AudioManager.Play(sound); }
            catch { }
        }

        private static void StopSound(string sound)
        {
            try { AudioManager.Stop(sound); }
            catch { }
        }

        private void Warn(string prefix, Exception exception)
        {
            if (logWarning != null)
                logWarning(prefix + exception);
        }

        private void OnDestroy()
        {
            if (cleaningUp)
                return;
            cleaningUp = true;
            StopSound(MeteorAnticipationSound);
            DestroyFireball(ref upperFireball);
            DestroyFireball(ref lowerFireball);
            dragonRoot = null;
            animator = null;
            bodyRenderers = null;
            bodyRendererVisibility = null;
            mouthRoot = null;
            meteorTemplate = null;
            meteorProperties = null;
            donorLabel = null;
        }
    }

    internal sealed class CreatorToolsDragonFireballsMarker : MonoBehaviour
    {
    }
}
