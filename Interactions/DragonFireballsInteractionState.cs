using System;
using System.Collections.Generic;
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
        private const int AttackCount = 3;
        private const float EntranceDurationSeconds = 20f / 24f;
        private const float ExitDurationSeconds = 20f / 24f;
        private const float MaximumLifetimeSeconds = 24f;
        private const float OffscreenCleanupMarginPixels = 220f;
        private static readonly string[] AttackPatternCodes =
        {
            "UDU",
            "DUD",
            "UBD",
            "DBU",
            "BDU",
            "UDB"
        };
        private const string MeteorTriggerName = "OnMeteor";
        private const string MeteorRepeatName = "Repeat";
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
        private readonly List<DragonLevelMeteor> fireballs =
            new List<DragonLevelMeteor>();
        private readonly List<float> fireballOffscreenElapsed =
            new List<float>();
        private DragonLevelMeteor.State[] attackPattern;
        private Action<string> logWarning;
        private string donor;
        private string giftImagePath = string.Empty;
        private float cameraScale = 1f;
        private float elapsed;
        private float repeatAnticipationStartSeconds = float.PositiveInfinity;
        private float repeatTriggerSeconds = float.PositiveInfinity;
        private float repeatAttackStartSeconds = float.PositiveInfinity;
        private float repeatReleaseSeconds = float.PositiveInfinity;
        private float exitStartSeconds = float.PositiveInfinity;
        private int attacksReleased;
        private bool meteorStartSoundPlayed;
        private bool attackArmed;
        private bool anticipationStopped;
        private bool attackSoundPlayed;
        private bool fireballsReleased;
        private bool repeatAnticipationStarted;
        private bool repeatTriggered;
        private bool repeatAttackSoundPlayed;
        private bool attackSequenceComplete;
        private bool presentationTransferred;
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
            attackPattern = SelectAttackPattern();
            if (animator != null)
                animator.SetBool(MeteorRepeatName, AttackCount > 1);
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
            for (var i = 0; i < fireballs.Count; i++)
                if (fireballs[i] != null)
                {
                    position = fireballs[i].transform.position;
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

        internal bool BlocksConcurrentDragon
        {
            get { return !cleaningUp && !bodyHidden; }
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
            {
                fireballsReleased = true;
                ReleaseFireballs(AttackStartSeconds);
            }
            UpdateRepeatedAttacks();
            if (!bodyHidden && attackSequenceComplete && elapsed >=
                    exitStartSeconds + ExitDurationSeconds)
                HideDragon();

            CleanupFireballs(delta);

            if (attackSequenceComplete && fireballs.Count == 0 && bodyHidden)
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
            if (elapsed <= exitStartSeconds)
            {
                dragonRoot.transform.position = attack;
                return;
            }

            var exitProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    (elapsed - exitStartSeconds) / ExitDurationSeconds));
            dragonRoot.transform.position = Vector3.Lerp(
                attack, hidden, exitProgress);
        }

        private void ReleaseFireballs(float currentAttackStartSeconds)
        {
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
                var selectedState = attackPattern[attacksReleased];
                if (selectedState == DragonLevelMeteor.State.Both)
                {
                    var upperFireball = CreateFireball(
                        mouthPosition, DragonLevelMeteor.State.Up,
                        "CreatorTools_NativeDragonFireball_Up");
                    CreateFireball(
                        mouthPosition, DragonLevelMeteor.State.Down,
                        "CreatorTools_NativeDragonFireball_Down");
                    TransferPresentationToFireball(upperFireball);
                }
                else
                {
                    var fireball = CreateFireball(
                        mouthPosition,
                        selectedState,
                        selectedState == DragonLevelMeteor.State.Up
                            ? "CreatorTools_NativeDragonFireball_Up"
                            : "CreatorTools_NativeDragonFireball_Down");
                    TransferPresentationToFireball(fireball);
                }
                PlaySound(MeteorSpitSound);
                attacksReleased++;
                ScheduleNextAttack(currentAttackStartSeconds);
            }
            catch (Exception exception)
            {
                Warn("Could not release the Dragon's native fireballs: ",
                    exception);
                DestroyFireballs();
                Destroy(gameObject);
            }
        }

        private static DragonLevelMeteor.State[] SelectAttackPattern()
        {
            var code = AttackPatternCodes[
                UnityEngine.Random.Range(0, AttackPatternCodes.Length)];
            var pattern = new DragonLevelMeteor.State[AttackCount];
            for (var i = 0; i < pattern.Length; i++)
            {
                switch (code[i])
                {
                    case 'U':
                        pattern[i] = DragonLevelMeteor.State.Up;
                        break;
                    case 'D':
                        pattern[i] = DragonLevelMeteor.State.Down;
                        break;
                    default:
                        pattern[i] = DragonLevelMeteor.State.Both;
                        break;
                }
            }
            return pattern;
        }

        private void ScheduleNextAttack(float currentAttackStartSeconds)
        {
            if (attacksReleased >= attackPattern.Length)
            {
                attackSequenceComplete = true;
                exitStartSeconds = currentAttackStartSeconds +
                    NativeAttackSeconds + NativeAttackEndSeconds;
                if (animator != null)
                    animator.SetBool(MeteorRepeatName, false);
                ClearRepeatSchedule();
                return;
            }

            repeatAnticipationStartSeconds = currentAttackStartSeconds +
                NativeAttackSeconds;
            repeatTriggerSeconds = repeatAnticipationStartSeconds +
                Mathf.Max(0f, meteorProperties.shotDelay);
            repeatAttackStartSeconds = repeatTriggerSeconds +
                NativeAnticipationEndSeconds;
            repeatReleaseSeconds = repeatAttackStartSeconds + 7f / 24f;
            repeatAnticipationStarted = false;
            repeatTriggered = false;
            repeatAttackSoundPlayed = false;
        }

        private void UpdateRepeatedAttacks()
        {
            if (!fireballsReleased || attackSequenceComplete)
                return;

            for (var i = 0; i < AttackCount; i++)
            {
                if (!repeatAnticipationStarted &&
                    elapsed >= repeatAnticipationStartSeconds)
                {
                    repeatAnticipationStarted = true;
                    if (animator != null)
                        animator.SetBool(
                            MeteorRepeatName,
                            attacksReleased < attackPattern.Length - 1);
                    PlaySound(MeteorAnticipationSound);
                }
                if (!repeatTriggered && elapsed >= repeatTriggerSeconds)
                {
                    repeatTriggered = true;
                    if (animator != null)
                        animator.SetTrigger(MeteorTriggerName);
                    StopSound(MeteorAnticipationSound);
                }
                if (!repeatAttackSoundPlayed &&
                    elapsed >= repeatAttackStartSeconds)
                {
                    repeatAttackSoundPlayed = true;
                    PlaySound(MeteorAttackSound);
                }
                if (elapsed < repeatReleaseSeconds)
                    return;

                var attackStart = repeatAttackStartSeconds;
                repeatReleaseSeconds = float.PositiveInfinity;
                ReleaseFireballs(attackStart);
                if (attackSequenceComplete)
                    return;
            }
        }

        private void ClearRepeatSchedule()
        {
            repeatAnticipationStartSeconds = float.PositiveInfinity;
            repeatTriggerSeconds = float.PositiveInfinity;
            repeatAttackStartSeconds = float.PositiveInfinity;
            repeatReleaseSeconds = float.PositiveInfinity;
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
            fireballs.Add(fireball);
            fireballOffscreenElapsed.Add(0f);
            return fireball;
        }

        private void TransferPresentationToFireball(DragonLevelMeteor fireball)
        {
            if (presentationTransferred || fireball == null)
                return;
            presentationTransferred = true;
            var anchor = FindLabelAnchor(fireball.gameObject);
            if (donorLabel != null && donorLabel.RebindTo(
                    fireball.gameObject, anchor, 0.2f))
                return;

            CreatorToolsInteractionPresentation.PrepareActor(
                fireball.gameObject, anchor, donor, logWarning);
            var label = fireball.gameObject.GetComponent<
                CreatorToolsDonorLabel>();
            if (label != null && !string.IsNullOrEmpty(giftImagePath))
                label.SetGiftImage(giftImagePath);
        }

        private void CleanupFireballs(float delta)
        {
            for (var i = fireballs.Count - 1; i >= 0; i--)
            {
                var fireball = fireballs[i];
                if (fireball == null)
                {
                    RemoveFireballAt(i);
                    continue;
                }
                if (IsFullyOutsideGameplayView(
                        fireball.gameObject,
                        OffscreenCleanupMarginPixels * cameraScale))
                    fireballOffscreenElapsed[i] += delta;
                else
                    fireballOffscreenElapsed[i] = 0f;
                if (fireballOffscreenElapsed[i] < 0.6f)
                    continue;
                Destroy(fireball.gameObject);
                RemoveFireballAt(i);
            }
        }

        private void RemoveFireballAt(int index)
        {
            fireballs.RemoveAt(index);
            fireballOffscreenElapsed.RemoveAt(index);
        }

        private void DestroyFireballs()
        {
            for (var i = fireballs.Count - 1; i >= 0; i--)
                if (fireballs[i] != null)
                    Destroy(fireballs[i].gameObject);
            fireballs.Clear();
            fireballOffscreenElapsed.Clear();
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
            DestroyFireballs();
            dragonRoot = null;
            animator = null;
            bodyRenderers = null;
            bodyRendererVisibility = null;
            mouthRoot = null;
            meteorTemplate = null;
            meteorProperties = null;
            attackPattern = null;
            donorLabel = null;
        }
    }

    internal sealed class CreatorToolsDragonFireballsMarker : MonoBehaviour
    {
    }
}
