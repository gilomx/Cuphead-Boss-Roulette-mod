using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // The native miniboss controllers already own health and their death
    // animations. Only BaronessLevel subscribes their damage event to its
    // timeline; interaction actors deliberately never enter that registry.
    internal sealed class BaronessMiniBossInteractionState : MonoBehaviour
    {
        // Aircraft and The Howling Aces use the same smaller miniboss bodies.
        // Keep this reduction stable; temporary player transformations must
        // not resize enemies in the middle of an attack.
        private const float AircraftSizeMultiplier = 0.8f;
        private static readonly FieldInfo JawbreakerSprite =
            AccessTools.Field(typeof(BaronessLevelJawbreaker), "sprite");
        private static readonly FieldInfo WaffleMouth =
            AccessTools.Field(typeof(BaronessLevelWaffle), "mouth");
        private static readonly FieldInfo GumballLid =
            AccessTools.Field(typeof(BaronessLevelGumball), "lid");
        private static readonly FieldInfo MermaidWaveOne =
            AccessTools.Field(typeof(FlyingMermaidLevelMerdusaHead), "wave1");
        private static readonly FieldInfo MermaidWaveTwo =
            AccessTools.Field(typeof(FlyingMermaidLevelMerdusaHead), "wave2");
        private static readonly FieldInfo MermaidProperties =
            AccessTools.Field(typeof(FlyingMermaidLevel), "properties");
        private static readonly FieldInfo DevilLowerPlatforms =
            AccessTools.Field(typeof(DevilLevel), "phase3Platforms");
        private static readonly FieldInfo CornBottomPoint =
            AccessTools.Field(typeof(BaronessLevelCandyCorn), "bottomPoint");
        private static readonly FieldInfo WaffleStartPosition =
            AccessTools.Field(typeof(BaronessLevelWaffle), "startPos");
        private static readonly FieldInfo WaffleOriginalPivotPosition =
            AccessTools.Field(typeof(BaronessLevelWaffle), "originalPivotPos");
        private static Level waterLevel;
        private static LevelProperties.FlyingMermaid waterProperties;
        private static Collider2D waterCollider;
        private static SpriteRenderer waterWaveOne;
        private static SpriteRenderer waterWaveTwo;
        private static int waterFrame = -1;
        private static bool waterVisible;
        private static float waterGround;

        private readonly List<GameObject> secondaryObjects = new List<GameObject>();
        private BaronessLevelMiniBossBase actor;
        private Action<string> warning;
        private Camera gameplayCamera;
        private Transform pivot;
        private Vector3 initialCameraPosition;
        private float cameraScale = 1f;
        private float bodySizeMultiplier = 1f;
        private float endedTime;
        private bool initialized;
        private bool cleaningUp;
        private bool usesAircraftArena;
        private bool usesWaterFloor;
        private bool usesViewportFloor;
        private bool usesDevilLowerArena;
        private bool usesNativeBodyScale;
        private float viewportFloorY;
        private float waterFloorY;

        internal static bool CanSpawnInCurrentLevel(string item)
        {
            if (Level.Current == null || !HasActivePlayer())
                return false;
            var camera = BaronessHeadTossInteractionState.FindGameplayCamera();
            if (camera == null)
                return false;
            if (IsDevilLowerArena())
            {
                // ZoomOut activates the lower arena before the descent ends.
                // Wait for native controls even if transition protection is off.
                foreach (var player in PlayerManager.GetAllPlayers())
                {
                    var groundPlayer = player as LevelPlayerController;
                    if (groundPlayer != null && !groundPlayer.IsDead &&
                        groundPlayer.weaponManager != null && groundPlayer.weaponManager.allowInput)
                        return true;
                }
                return false;
            }
            if (UnityEngine.Object.FindObjectOfType<PlanePlayerController>() != null ||
                Level.Current.CurrentLevel == Levels.Airplane)
            {
                // Cala keeps its visible water contract, including the cave
                // transition. Aircraft and The Howling Aces use a viewport
                // floor even when the native logical floor is out of view.
                if (Level.Current.CurrentLevel != Levels.FlyingMermaid)
                    return true;
                float groundOnWater;
                return TryGetWaterGround(camera, out groundOnWater);
            }
            var ground = (float)Level.Current.Ground;
            var halfHeight = camera.orthographicSize;
            // A usable floor is necessary for the original ground patterns.
            // Scrolling/platform-only levels can have a logical Ground far
            // outside the screen, which would strand a cupcake permanently.
            return ground >= camera.transform.position.y - halfHeight * 1.15f &&
                ground < camera.transform.position.y + halfHeight * 0.25f;
        }

        private static bool IsDevilLowerArena()
        {
            var level = Level.Current as DevilLevel;
            if (level == null || DevilLowerPlatforms == null)
                return false;
            // The enum advances while phase one's arena is still visible.
            // This root activates at ZoomOut and stays active through Hands
            // and Tears, even as its individual platforms disappear.
            var platforms = DevilLowerPlatforms.GetValue(level) as GameObject;
            return platforms != null && platforms.activeSelf;
        }

        internal static bool HasNativeArenaFields
        {
            get
            {
                return DevilLowerPlatforms != null && CornBottomPoint != null &&
                    WaffleStartPosition != null && WaffleOriginalPivotPosition != null;
            }
        }

        private static bool TryGetWaterGround(Camera camera, out float ground)
        {
            ground = 0f;
            var level = Level.Current;
            if (level == null || level.CurrentLevel != Levels.FlyingMermaid || camera == null)
                return false;
            if (waterLevel != level)
            {
                waterLevel = level;
                waterProperties = MermaidProperties == null ? null :
                    MermaidProperties.GetValue(level) as LevelProperties.FlyingMermaid;
                waterCollider = null;
                waterWaveOne = null;
                waterWaveTwo = null;
                waterFrame = -1;
            }
            // Head phase changes the water's rendering layers and enters the
            // cave. It can leave the splash trigger and wave objects enabled,
            // so gate on the native phase as well as visible scene geometry.
            if (waterProperties == null || waterProperties.CurrentState == null ||
                waterProperties.CurrentState.stateName == LevelProperties.FlyingMermaid.States.Head)
                return false;
            if (waterFrame != Time.frameCount)
            {
                waterFrame = Time.frameCount;
                waterVisible = false;
                if (waterCollider == null)
                {
                    // Instance creates a new manager when absent. Find only
                    // the native scene's existing splash-detection surface.
                    var manager = UnityEngine.Object.FindObjectOfType<FlyingMermaidLevelSplashManager>();
                    if (manager != null)
                        waterCollider = manager.GetComponent<Collider2D>();
                }
                if (waterWaveOne == null || waterWaveTwo == null)
                {
                    var head = UnityEngine.Object.FindObjectOfType<FlyingMermaidLevelMerdusaHead>();
                    if (head != null)
                    {
                        waterWaveOne = MermaidWaveOne == null ? null : MermaidWaveOne.GetValue(head) as SpriteRenderer;
                        waterWaveTwo = MermaidWaveTwo == null ? null : MermaidWaveTwo.GetValue(head) as SpriteRenderer;
                    }
                }
                if (waterCollider != null && waterCollider.enabled &&
                    waterCollider.gameObject.activeInHierarchy &&
                    (VisibleWaterWave(waterWaveOne, camera) || VisibleWaterWave(waterWaveTwo, camera)))
                {
                    var bounds = waterCollider.bounds;
                    waterGround = bounds.max.y;
                    var center = camera.transform.position;
                    var halfHeight = camera.orthographicSize;
                    var halfWidth = halfHeight * camera.aspect;
                    waterVisible = waterGround >= center.y - halfHeight &&
                        waterGround < center.y + halfHeight * 0.25f &&
                        bounds.min.x <= center.x + halfWidth &&
                        bounds.max.x >= center.x - halfWidth;
                }
            }
            ground = waterGround;
            return waterVisible;
        }

        internal static void ResetArenaCache()
        {
            waterLevel = null;
            waterProperties = null;
            waterCollider = null;
            waterWaveOne = null;
            waterWaveTwo = null;
            waterFrame = -1;
            waterVisible = false;
            waterGround = 0f;
        }

        private static bool VisibleWaterWave(SpriteRenderer wave, Camera camera)
        {
            // Check the native foreground waves themselves. A still-existing
            // splash trigger must not become an invisible floor.
            if (wave == null || wave.sprite == null || !wave.enabled ||
                !wave.gameObject.activeInHierarchy || wave.color.a <= 0f)
                return false;
            var bounds = wave.bounds;
            var center = camera.transform.position;
            var halfHeight = camera.orthographicSize;
            var halfWidth = halfHeight * camera.aspect;
            return bounds.max.y >= center.y - halfHeight &&
                bounds.min.y <= center.y + halfHeight &&
                bounds.max.x >= center.x - halfWidth &&
                bounds.min.x <= center.x + halfWidth;
        }

        private static bool HasActivePlayer()
        {
            foreach (var player in PlayerManager.GetAllPlayers())
                if (player != null && !player.IsDead)
                    return true;
            return false;
        }

        internal void Initialize(
            BaronessLevelMiniBossBase actor, string item, string donor,
            string giftImagePath, Action<string> warning)
        {
            if (!BaronessMiniBossInteractionPatches.InstalledSuccessfully)
                throw new InvalidOperationException("The native Baroness miniboss integration could not be prepared.");
            if (actor == null || !CanSpawnInCurrentLevel(item))
                throw new InvalidOperationException("The native Baroness miniboss requires visible ground, an aircraft arena or Cala Maria's visible water.");
            this.actor = actor;
            this.warning = warning;
            gameplayCamera = BaronessHeadTossInteractionState.FindGameplayCamera();
            initialCameraPosition = gameplayCamera.transform.position;
            cameraScale = Mathf.Max(0.01f, gameplayCamera.orthographicSize / 360f);
            usesAircraftArena = UnityEngine.Object.FindObjectOfType<PlanePlayerController>() != null;
            usesWaterFloor = usesAircraftArena && Level.Current.CurrentLevel == Levels.FlyingMermaid;
            usesDevilLowerArena = IsDevilLowerArena();
            usesNativeBodyScale = usesDevilLowerArena ||
                Level.Current.CurrentLevel == Levels.Saltbaker ||
                Level.Current.CurrentLevel == Levels.OldMan;
            // The Howling Aces retains ground controls and contacts, while
            // sharing the smaller bodies and visible floor of aircraft arenas.
            // Devil's lower arena, Saltbaker and Glumstone keep the player's
            // native world size under a wider camera. Cancel body zoom
            // compensation there.
            // Labels continue to use their independent live camera scale.
            bodySizeMultiplier = usesNativeBodyScale ? 1f / cameraScale :
                (usesAircraftArena || Level.Current.CurrentLevel == Levels.Airplane
                    ? AircraftSizeMultiplier : 1f);
            usesViewportFloor = !usesWaterFloor && (usesAircraftArena ||
                Level.Current.CurrentLevel == Levels.Airplane || usesDevilLowerArena);
            // Freeze this plane arena's reference at spawn: Gumball, Corn and
            // Waffle store world positions in their native routines. Following
            // camera shake/player tracking only for Cupcake would split floors.
            // Howling Aces keeps world-down gravity when its camera rotates.
            // Use the upright viewport's bottom: a floor captured from a side
            // view's wider world-Y span would vanish when the camera turns back.
            // Devil alone follows its moving lower viewport; SyncDevilFloor
            // rebases the native cached points together with every floor actor.
            viewportFloorY = initialCameraPosition.y - gameplayCamera.orthographicSize;
            actor.gameObject.AddComponent<CreatorToolsBaronessMiniBossMarker>().Owner = this;
            actor.gameObject.name = "CreatorTools_NativeBaronessMiniBoss_" + item;

            var mode = Level.CurrentMode;
            if (mode != Level.Mode.Easy && mode != Level.Mode.Normal && mode != Level.Mode.Hard)
                mode = Level.Mode.Normal;
            var properties = LevelProperties.Baroness.GetMode(mode).CurrentState;
            if (properties == null)
                throw new InvalidOperationException("Cuphead's native miniboss properties are unavailable.");

            // Activation runs native Awake exactly once before Init starts
            // attack coroutines. The cache preserves inactive prefab clones.
            actor.gameObject.SetActive(true);
            PrepareAircraftDamageColliders(actor);
            ApplyActorSize(actor.gameObject);
            var right = WorldX(640f) + 100f * cameraScale;
            var ground = ArenaGround(Level.Current.Ground);
            if (actor is BaronessLevelGumball)
            {
                var p = properties.gumball;
                var scaled = new LevelProperties.Baroness.Gumball(p.HP,
                    p.gumballMovementSpeed, p.gumballDeathSpeed * cameraScale,
                    p.gumballAttackDurationOffRange, p.gumballAttackDurationOnRange,
                    p.gravity * cameraScale, Scale(p.velocityX), p.rateOfFire,
                    Scale(p.velocityY), Scale(p.offsetX));
                ((BaronessLevelGumball)actor).Init(scaled,
                    new Vector2(right, ground + ScaleBodyDistance(182f)), p.HP);
            }
            else if (actor is BaronessLevelWaffle)
            {
                var p = properties.waffle;
                var scaled = new LevelProperties.Baroness.Waffle(p.HP,
                    p.movementSpeed, p.anticipation, p.attackDelayRange,
                    p.explodeSpeed, p.explodeTwoDuration, p.explodeDistance,
                    p.explodeReturnSpeed, p.XAxisSpeed * cameraScale,
                    p.pivotPointMoveAmount * cameraScale);
                pivot = new GameObject("CreatorTools_BaronessWafflePivot").transform;
                pivot.SetParent(transform, false);
                pivot.position = new Vector3(WorldX(-74f), ground + 226f * cameraScale, 0f);
                ScaleActorField("loopSize");
                ((BaronessLevelWaffle)actor).Init(scaled,
                    new Vector2(right, ground + 82f * cameraScale), pivot, p.movementSpeed, p.HP);
            }
            else if (actor is BaronessLevelCandyCorn)
            {
                var p = properties.candyCorn;
                var scaled = new LevelProperties.Baroness.CandyCorn(p.HP,
                    p.movementSpeed * cameraScale, p.changeLevelString,
                    WorldX(p.centerPosition), p.deathMoveSpeed * cameraScale,
                    p.deathAcceleration, p.miniCornSpawnDelay, p.miniCornHP,
                    p.miniCornMovementSpeed * cameraScale, p.spawnMinis);
                ((BaronessLevelCandyCorn)actor).Init(scaled,
                    new Vector2(right, ground + ScaleBodyDistance(122f)), scaled.movementSpeed, p.HP);
            }
            else if (actor is BaronessLevelCupcake)
            {
                var p = properties.cupcake;
                var scaled = new LevelProperties.Baroness.Cupcake(p.HP,
                    ScalePatterns(p.XSpeedString), p.hold,
                    ScaleBodyDistance(p.splashOriginalOffset), ScaleBodyDistance(p.splashOffset),
                    p.projectileOn);
                ScaleActorField("ySpeedUp");
                ScaleActorField("ySpeedDown");
                ScaleActorField("offset");
                ((BaronessLevelCupcake)actor).Init(scaled,
                    new Vector2(right + 100f * cameraScale, ground + ScaleBodyDistance(82f)), p.HP);
            }
            else if (actor is BaronessLevelJawbreaker)
            {
                var p = properties.jawbreaker;
                var scaled = new LevelProperties.Baroness.Jawbreaker(p.jawbreakerMinis,
                    p.jawbreakerMiniSpace * cameraScale, p.jawbreakerHomeDuration,
                    p.jawbreakerHomingHP, p.jawbreakerHomingSpeed * cameraScale,
                    p.jawbreakerHomingRotation);
                ((BaronessLevelJawbreaker)actor).Init(scaled, PlayerManager.GetNext(),
                    new Vector2(right, ground + 92f * cameraScale),
                    p.jawbreakerHomingRotation, p.jawbreakerHomingHP);
            }
            else
                throw new InvalidOperationException("Unknown native Baroness miniboss controller.");

            RestoreActorSize();
            PrepareDonorLabel(donor);
            CreatorToolsInteractionPresentation.SetGiftImage(actor.gameObject, giftImagePath, warning);
            initialized = true;
        }

        private void PrepareDonorLabel(string donor)
        {
            var primary = actor.GetComponent<SpriteRenderer>();
            SpriteRenderer secondary = null;
            var includeSecondary = false;
            if (actor is BaronessLevelJawbreaker)
            {
                // Its root turns toward the player but has no sprite. The
                // visible child stays upright while following that rotation.
                var spriteTransform = JawbreakerSprite == null ? null :
                    JawbreakerSprite.GetValue(actor) as Transform;
                primary = spriteTransform == null ? null :
                    spriteTransform.GetComponent<SpriteRenderer>();
            }
            else if (actor is BaronessLevelWaffle)
            {
                // Follow the central mouth only while the assembled body is
                // hidden. Expelled waffle pieces are not label anchors.
                var mouth = WaffleMouth == null ? null :
                    WaffleMouth.GetValue(actor) as Transform;
                secondary = mouth == null ? null : mouth.GetComponent<SpriteRenderer>();
            }
            else if (actor is BaronessLevelGumball)
            {
                secondary = GumballLid == null ? null :
                    GumballLid.GetValue(actor) as SpriteRenderer;
                includeSecondary = true;
            }
            CreatorToolsInteractionPresentation.PrepareActor(
                actor.gameObject, primary, donor, warning);
            var label = actor.GetComponent<CreatorToolsDonorLabel>();
            if (label != null)
                label.FollowAnimatedBody(primary, secondary, includeSecondary, RestoreActorSize);
        }

        internal bool TryGetActorPosition(out Vector2 position)
        {
            position = actor == null ? Vector2.zero : (Vector2)actor.transform.position;
            return actor != null;
        }

        private MinMax Scale(MinMax value)
        {
            return new MinMax(value.min * cameraScale, value.max * cameraScale);
        }

        private void ApplyActorSize(GameObject root)
        {
            if (usesNativeBodyScale)
            {
                // Preserve prefab scale, including secondary objects spawned
                // while the native zoom is still settling. Mark it so later
                // presentation setup does not apply camera compensation again.
                CreatorToolsInteractionPresentation.MarkInheritedGameplayCameraScale(root, 1f);
                return;
            }
            CreatorToolsInteractionPresentation.MatchGameplayCameraScale(root, warning);
            if (bodySizeMultiplier == 1f)
                return;
            // Apply to the whole root so every sprite and collider stays aligned.
            // Main actors and tracked secondary objects call this exactly once.
            var scale = root.transform.localScale;
            root.transform.localScale = new Vector3(
                scale.x * bodySizeMultiplier, scale.y * bodySizeMultiplier, scale.z);
            CreatorToolsInteractionPresentation.MarkInheritedGameplayCameraScale(
                root, cameraScale * bodySizeMultiplier);
        }

        private string[] ScalePatterns(string[] patterns)
        {
            var result = new string[patterns.Length];
            for (var i = 0; i < patterns.Length; i++)
            {
                var parts = patterns[i].Split(',');
                for (var j = 0; j < parts.Length; j++)
                    parts[j] = Mathf.RoundToInt(float.Parse(parts[j].Trim(), CultureInfo.InvariantCulture) * cameraScale)
                        .ToString(CultureInfo.InvariantCulture);
                result[i] = string.Join(",", parts);
            }
            return result;
        }

        private void ScaleActorField(string name)
        {
            var field = AccessTools.Field(actor.GetType(), name);
            if (field != null)
                field.SetValue(actor, (float)field.GetValue(actor) * cameraScale);
        }

        internal float WorldX(float native)
        {
            var center = gameplayCamera == null ? initialCameraPosition.x : gameplayCamera.transform.position.x;
            var halfWidth = gameplayCamera == null ? 640f * cameraScale : gameplayCamera.orthographicSize * gameplayCamera.aspect;
            if (native <= -540f)
                return center - halfWidth + (native + 640f) * cameraScale;
            if (native >= 540f)
                return center + halfWidth + (native - 640f) * cameraScale;
            return center + native * cameraScale;
        }

        internal float WorldY(float native)
        {
            var center = gameplayCamera == null ? initialCameraPosition.y : gameplayCamera.transform.position.y;
            return center + native * cameraScale;
        }

        internal float ScaleDistance(float native) { return native * cameraScale; }

        internal float ScaleBodyDistance(float native)
        {
            return native * cameraScale * bodySizeMultiplier;
        }

        internal float ArenaGround(float native)
        {
            if (usesViewportFloor)
                return viewportFloorY;
            if (!usesWaterFloor)
                return native;
            float ground;
            if (TryGetWaterGround(gameplayCamera, out ground))
                waterFloorY = ground;
            // Keep the last water height only until Update removes the actor;
            // never substitute a plane level's unrelated logical Ground.
            return waterFloorY;
        }

        private void PrepareAircraftDamageColliders(Component component)
        {
            if (!usesAircraftArena ||
                (!(component is BaronessLevelCandyCorn) && !(component is BaronessLevelCandyCornMini)))
                return;
            // These two native prefabs use solid kinematic colliders. Basic
            // plane bullets have solid colliders without a Rigidbody2D, so
            // that kinematic/static pair does not deliver contact callbacks.
            // Triggers reach the same native checkCollision/OnDamageTaken path as
            // the other Baroness minibosses, without replacing their health.
            var colliders = component.GetComponents<Collider2D>();
            for (var i = 0; i < colliders.Length; i++)
                colliders[i].isTrigger = true;
        }

        internal void TrackSecondary(Component component)
        {
            if (cleaningUp || component == null || component.gameObject == null)
                return;
            var root = component.gameObject;
            if (secondaryObjects.Contains(root) || (actor != null && root == actor.gameObject))
                return;
            secondaryObjects.Add(root);
            PrepareAircraftDamageColliders(component);
            var marker = root.GetComponent<CreatorToolsBaronessMiniBossMarker>();
            if (marker == null)
                marker = root.AddComponent<CreatorToolsBaronessMiniBossMarker>();
            marker.Owner = this;
            // Preserve world position; using the state root makes retry and
            // level-end cleanup include projectiles and following candies.
            root.transform.SetParent(transform, true);
            ApplyActorSize(root);
            CreatorToolsInteractionPresentation.BringActorToFront(root);
        }

        private void Update()
        {
            if (!initialized || cleaningUp)
                return;
            float currentWaterGround;
            if (Level.Current == null || ((usesViewportFloor || usesWaterFloor) && gameplayCamera == null) ||
                usesDevilLowerArena != IsDevilLowerArena() ||
                (usesWaterFloor &&
                !TryGetWaterGround(gameplayCamera, out currentWaterGround)))
            {
                // Clear the old arena's actors even if global transition
                // protection is disabled. Destroy clears owned projectiles.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            for (var i = secondaryObjects.Count - 1; i >= 0; i--)
                if (secondaryObjects[i] == null)
                    secondaryObjects.RemoveAt(i);
            if (actor != null)
                return;
            endedTime += Time.unscaledDeltaTime * Mathf.Max(0f, CupheadTime.GlobalSpeed);
            if (endedTime >= 2f)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            RestoreActorSize();
        }

        private void RestoreActorSize()
        {
            // Also called before the label measures its anchor, making floor
            // following independent of the two components' LateUpdate order.
            SyncDevilFloor();
            if (actor == null || cleaningUp)
                return;
            // Native turn animation events reset root scale to +/-1.
            // Restore camera and aircraft size factors while preserving facing.
            var scale = actor.transform.localScale;
            var bodyScale = cameraScale * bodySizeMultiplier;
            actor.transform.localScale = new Vector3(
                Mathf.Sign(scale.x) * bodyScale, bodyScale, scale.z);
        }

        private void SyncDevilFloor()
        {
            if (!initialized || cleaningUp || !usesDevilLowerArena || gameplayCamera == null)
                return;
            var floor = gameplayCamera.transform.position.y - gameplayCamera.orthographicSize;
            var deltaY = floor - viewportFloorY;
            if (deltaY == 0f)
                return;
            viewportFloorY = floor;
            if (actor != null && !(actor is BaronessLevelJawbreaker))
            {
                // Shift the body, not the ownership root: already launched
                // projectiles keep their independent world trajectories.
                ShiftTransformY(actor.transform, deltaY);
                if (actor is BaronessLevelCandyCorn)
                    CornBottomPoint.SetValue(actor, (float)CornBottomPoint.GetValue(actor) + deltaY);
                else if (actor is BaronessLevelWaffle)
                {
                    ShiftTransformY(pivot, deltaY);
                    ShiftWafflePointY(WaffleStartPosition, deltaY);
                    ShiftWafflePointY(WaffleOriginalPivotPosition, deltaY);
                }
            }
            // Cupcake's floor splashes are independently owned effects. They
            // must stay on the same floor after landing, including after death.
            if (actor is BaronessLevelCupcake)
                for (var i = 0; i < secondaryObjects.Count; i++)
                    if (secondaryObjects[i] != null && secondaryObjects[i].GetComponent<Effect>() != null)
                        ShiftTransformY(secondaryObjects[i].transform, deltaY);
        }

        private static void ShiftTransformY(Transform target, float deltaY)
        {
            if (target == null)
                return;
            var position = target.position;
            position.y += deltaY;
            target.position = position;
        }

        private void ShiftWafflePointY(FieldInfo field, float deltaY)
        {
            var position = (Vector3)field.GetValue(actor);
            position.y += deltaY;
            field.SetValue(actor, position);
        }

        private void OnDestroy()
        {
            cleaningUp = true;
            for (var i = 0; i < secondaryObjects.Count; i++)
                if (secondaryObjects[i] != null)
                    Destroy(secondaryObjects[i]);
            secondaryObjects.Clear();
            actor = null;
            pivot = null;
        }
    }

    internal sealed class CreatorToolsBaronessMiniBossMarker : MonoBehaviour
    {
        internal BaronessMiniBossInteractionState Owner;
    }

    // Adjust only interaction actors. Baroness's own controllers and global
    // Level boundaries keep their original behavior, including co-op damage.
    internal static class BaronessMiniBossInteractionPatches
    {
        private static readonly Type[] ActorTypes =
        {
            typeof(BaronessLevelGumball), typeof(BaronessLevelWaffle),
            typeof(BaronessLevelCandyCorn), typeof(BaronessLevelCupcake),
            typeof(BaronessLevelJawbreaker), typeof(BaronessLevelCandyCornMini),
            typeof(BaronessLevelGumballProjectile)
        };
        private static readonly Dictionary<Type, FieldInfo> IteratorActors = new Dictionary<Type, FieldInfo>();
        private static readonly Dictionary<Type, FieldInfo[]> IteratorSpawns = new Dictionary<Type, FieldInfo[]>();
        private static BaronessMiniBossInteractionState firingOwner;
        internal static bool InstalledSuccessfully { get; private set; }

        internal static void InstallPatches(Harmony harmony, Action<string> warning)
        {
            if (harmony == null)
                return;
            InstalledSuccessfully = BaronessMiniBossInteractionState.HasNativeArenaFields;
            if (!InstalledSuccessfully && warning != null)
                warning("Could not prepare Baroness miniboss arena fields for Devil's lower arena.");
            for (var i = 0; i < ActorTypes.Length; i++)
            {
                var methods = ActorTypes[i].GetMethods(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (var j = 0; j < methods.Length; j++)
                    if (NeedsCoordinates(methods[j]))
                        Patch(harmony, methods[j], null, null, "AdaptCoordinates", null, warning);
                var nested = ActorTypes[i].GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                for (var j = 0; j < nested.Length; j++)
                {
                    // Enum states and serialized piece records are nested here
                    // as well; only coroutine iterators carry a native owner.
                    if (!typeof(IEnumerator).IsAssignableFrom(nested[j]))
                        continue;
                    var owner = AccessTools.Field(nested[j], "$this");
                    var move = AccessTools.Method(nested[j], "MoveNext");
                    if (owner == null || move == null)
                        continue;
                    IteratorActors[nested[j]] = owner;
                    var fields = nested[j].GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var spawns = new List<FieldInfo>();
                    for (var k = 0; k < fields.Length; k++)
                        if (fields[k] != owner && IsSecondaryType(fields[k].FieldType))
                            spawns.Add(fields[k]);
                    IteratorSpawns[nested[j]] = spawns.ToArray();
                    if (spawns.Count != 0 || NeedsCoordinates(move))
                        Patch(harmony, move, null, spawns.Count == 0 ? null : "TrackIteratorSpawns",
                            NeedsCoordinates(move) ? "AdaptCoordinates" : null, null, warning);
                    if (ActorTypes[i] == typeof(BaronessLevelGumballProjectile) &&
                        nested[j].Name.StartsWith("<spawn_trail_cr>", StringComparison.Ordinal))
                        Patch(harmony, move, "BeginFiring", null, null, "EndFiring", warning);
                }
            }
            Patch(harmony, AccessTools.Method(typeof(BaronessLevelMiniBossBase), "OnDamageTaken"),
                "AllowBossDamageEvent", null, null, null, warning);
            Patch(harmony, AccessTools.Method(typeof(BaronessLevelGumball), "fireProjectiles"),
                "BeginFiring", null, null, "EndFiring", warning);
            // Without the parameter types, Harmony can resolve the inherited
            // AbstractProjectile.Create() with an incompatible return type.
            Patch(harmony, AccessTools.Method(typeof(BaronessLevelGumballProjectile), "Create",
                    new[] { typeof(Vector2), typeof(Vector2), typeof(float) }),
                null, "TrackGumballProjectile", null, null, warning);
            Patch(harmony, AccessTools.Method(typeof(Effect), "Create", new[] { typeof(Vector3) }),
                null, "TrackFiringEffect", null, null, warning);
        }

        private static bool IsSecondaryType(Type type)
        {
            return typeof(BaronessLevelCandyCornMini).IsAssignableFrom(type) ||
                typeof(BaronessLevelJawbreakerMini).IsAssignableFrom(type) ||
                typeof(BaronessLevelJawbreakerGhost).IsAssignableFrom(type) ||
                typeof(Effect).IsAssignableFrom(type);
        }

        private static void Patch(Harmony harmony, MethodBase method,
            string prefix, string postfix, string transpiler, string finalizer, Action<string> warning)
        {
            try
            {
                if (method == null)
                    throw new MissingMethodException("Native Baroness miniboss method is missing.");
                harmony.Patch(method, Hook(prefix), Hook(postfix), Hook(transpiler), Hook(finalizer), null);
            }
            catch (Exception exception)
            {
                InstalledSuccessfully = false;
                if (warning != null)
                    warning("Could not prepare Baroness miniboss method " + method + ": " + exception);
            }
        }

        private static HarmonyMethod Hook(string name)
        {
            return name == null ? null : new HarmonyMethod(AccessTools.Method(typeof(BaronessMiniBossInteractionPatches), name));
        }

        private static BaronessMiniBossInteractionState FindOwner(object instance)
        {
            var component = instance as Component;
            if (component == null && instance != null)
            {
                FieldInfo field;
                if (IteratorActors.TryGetValue(instance.GetType(), out field))
                    component = field.GetValue(instance) as Component;
            }
            if (component == null)
                return null;
            var marker = component.GetComponent<CreatorToolsBaronessMiniBossMarker>();
            return marker == null ? null : marker.Owner;
        }

        private static bool AllowBossDamageEvent(BaronessLevelMiniBossBase __instance)
        {
            return FindOwner(__instance) == null;
        }

        private static void TrackIteratorSpawns(object __instance)
        {
            var owner = FindOwner(__instance);
            FieldInfo[] fields;
            if (owner == null || !IteratorSpawns.TryGetValue(__instance.GetType(), out fields))
                return;
            for (var i = 0; i < fields.Length; i++)
                owner.TrackSecondary(fields[i].GetValue(__instance) as Component);
        }

        private static void BeginFiring(object __instance, out BaronessMiniBossInteractionState __state)
        {
            __state = firingOwner;
            firingOwner = FindOwner(__instance);
        }

        private static void EndFiring(BaronessMiniBossInteractionState __state)
        {
            firingOwner = __state;
        }

        private static void TrackGumballProjectile(BaronessLevelGumballProjectile __result)
        {
            if (firingOwner != null)
                firingOwner.TrackSecondary(__result);
        }

        private static void TrackFiringEffect(Effect __result)
        {
            if (firingOwner != null)
                firingOwner.TrackSecondary(__result);
        }

        private static bool NeedsCoordinates(MethodBase method)
        {
            var type = method.DeclaringType;
            return (type == typeof(BaronessLevelCandyCorn) &&
                    (method.Name == "MoveAlongX" || method.Name == "MoveAlongY")) ||
                (type == typeof(BaronessLevelCupcake) &&
                    (method.Name == "GoingUp" || method.Name == "GoingDown" || method.Name == "BoundaryCheck")) ||
                (type == typeof(BaronessLevelCandyCornMini) && method.Name == "FixedUpdate") ||
                (type == typeof(BaronessLevelGumballProjectile) && method.Name == "Update") ||
                (method.Name == "MoveNext" && type.DeclaringType == typeof(BaronessLevelGumball) &&
                    type.Name.StartsWith("<move_cr>", StringComparison.Ordinal)) ||
                (method.Name == "MoveNext" && type.DeclaringType == typeof(BaronessLevelCandyCorn) &&
                    type.Name.StartsWith("<death_cr>", StringComparison.Ordinal)) ||
                (method.Name == "MoveNext" && type.DeclaringType == typeof(BaronessLevelWaffle) &&
                    type.Name.StartsWith("<enter_cr>", StringComparison.Ordinal)) ||
                (method.Name == "MoveNext" && type.DeclaringType == typeof(BaronessLevelCupcake) &&
                    type.Name.StartsWith("<splash_cr>", StringComparison.Ordinal));
        }

        private static IEnumerable<CodeInstruction> AdaptCoordinates(
            IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            foreach (var instruction in instructions)
            {
                yield return instruction;
                string adapter = null;
                if (instruction.opcode == OpCodes.Ldc_R4)
                {
                    var value = (float)instruction.operand;
                    var type = __originalMethod.DeclaringType;
                    if ((type == typeof(BaronessLevelCandyCorn) && value == -640f) ||
                        (type == typeof(BaronessLevelCupcake) && (value == -540f || value == 540f)) ||
                        (type.DeclaringType == typeof(BaronessLevelGumball) &&
                            (value == 640f || value == -640f || value == -940f)))
                        adapter = "WorldX";
                    else if (value == 360f &&
                        (type == typeof(BaronessLevelCandyCorn) || type == typeof(BaronessLevelCupcake)))
                        adapter = "WorldY";
                    else if ((type == typeof(BaronessLevelCandyCornMini) && value == 720f) ||
                        (type == typeof(BaronessLevelGumballProjectile) && value == -360f) ||
                        (type.DeclaringType == typeof(BaronessLevelCandyCorn) && value == 560f))
                        adapter = "WorldY";
                    else if (type == typeof(BaronessLevelCupcake) && value == 120f)
                        adapter = "BodyDistance";
                    else if ((type == typeof(BaronessLevelCandyCorn) &&
                            (value == 50f || value == 10f || value == 125f)) ||
                        (type.DeclaringType == typeof(BaronessLevelWaffle) && value == 300f))
                        adapter = "Distance";
                }
                else if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo &&
                    ((MethodInfo)instruction.operand).DeclaringType == typeof(Level))
                {
                    var name = ((MethodInfo)instruction.operand).Name;
                    if (name == "get_Right") adapter = "Right";
                    else if (name == "get_Ground") adapter = "Ground";
                }
                if (adapter == null)
                    continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(BaronessMiniBossInteractionPatches), adapter));
            }
        }

        private static float WorldX(float value, object instance)
        {
            var owner = FindOwner(instance);
            return owner == null ? value : owner.WorldX(value);
        }
        private static float WorldY(float value, object instance)
        {
            var owner = FindOwner(instance);
            return owner == null ? value : owner.WorldY(value);
        }
        private static float Distance(float value, object instance)
        {
            var owner = FindOwner(instance);
            return owner == null ? value : owner.ScaleDistance(value);
        }
        private static float BodyDistance(float value, object instance)
        {
            var owner = FindOwner(instance);
            return owner == null ? value : owner.ScaleBodyDistance(value);
        }
        private static int Right(int value, object instance)
        {
            var owner = FindOwner(instance);
            return owner == null ? value : Mathf.RoundToInt(owner.WorldX(640f));
        }
        private static int Ground(int value, object instance)
        {
            var owner = FindOwner(instance);
            return owner == null ? value : Mathf.RoundToInt(owner.ArenaGround(value));
        }
    }
}
