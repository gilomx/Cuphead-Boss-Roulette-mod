using System;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CagneyHomingPlantInteractionState : MonoBehaviour
    {
        private const float VirtualGroundMargin = 16f;
        private const float GrowingLabelFollowSeconds = 0.55f;
        private const float DonorLabelFadeInSeconds = 0.45f;
        private const float DonorLabelVerticalOffsetPixels = 10f;

        private static readonly System.Reflection.FieldInfo FallingSpeedField =
            AccessTools.Field(typeof(FlowerLevelEnemySeed), "fallingSpeed");
        private static readonly System.Reflection.MethodInfo SeedLandMethod =
            AccessTools.Method(typeof(FlowerLevelEnemySeed), "OnSeedLand");

        private FlowerLevelEnemySeed seed;
        private FlowerLevelVenusSpawn plant;
        private GameObject plantScaleRoot;
        private CreatorToolsDonorLabel seedLabel;
        private Action<string> logWarning;
        private string donor;
        private float cameraScale = 1f;
        private bool virtualLandingTriggered;
        private bool plantWasAttached;
        private bool cleaningUp;

        internal bool SuppressNativeGround
        {
            get
            {
                return virtualLandingTriggered ||
                    UseVirtualGroundOnly;
            }
        }

        internal bool UseVirtualGroundOnly { get; private set; }

        internal bool TryGetActorPosition(out Vector2 position)
        {
            if (plant != null)
            {
                position = plant.transform.position;
                return true;
            }
            if (seed != null)
            {
                position = seed.transform.position;
                return true;
            }
            position = Vector2.zero;
            return false;
        }

        internal void Initialize(
            FlowerLevelEnemySeed seed,
            CreatorToolsDonorLabel seedLabel,
            string donor,
            float cameraScale,
            bool useVirtualGroundOnly,
            Action<string> logWarning)
        {
            this.seed = seed;
            this.seedLabel = seedLabel;
            this.donor = donor;
            this.cameraScale = Mathf.Max(0.01f, cameraScale);
            UseVirtualGroundOnly = useVirtualGroundOnly;
            this.logWarning = logWarning;
        }

        private void Update()
        {
            if (plantWasAttached)
            {
                if (plant == null)
                    Destroy(gameObject);
                return;
            }

            if (seed == null)
            {
                Destroy(gameObject);
                return;
            }
            if (virtualLandingTriggered ||
                Mathf.Max(0f, CupheadTime.GlobalSpeed) <= 0f)
                return;

            var camera = FindGameplayCamera();
            if (camera == null)
                return;

            var distanceToGameplayPlane = Mathf.Abs(
                camera.transform.position.z);
            var bottom = camera.ViewportToWorldPoint(new Vector3(
                0f, 0f, distanceToGameplayPlane)).y;
            var highest = HighestVisiblePoint(seed.gameObject);
            if (highest == float.MinValue ||
                highest >= bottom - VirtualGroundMargin * cameraScale)
                return;

            virtualLandingTriggered = true;
            try
            {
                var targetHighest = bottom -
                    VirtualGroundMargin * cameraScale;
                var position = seed.transform.position;
                position.y += targetHighest - highest;
                seed.transform.position = position;
                if (FallingSpeedField != null)
                    FallingSpeedField.SetValue(seed, 0);
                if (SeedLandMethod == null)
                    throw new MissingMethodException(
                        "Cuphead did not expose the Cagney seed landing method.");
                SeedLandMethod.Invoke(seed, null);
            }
            catch (Exception exception)
            {
                Warn("Could not trigger Cagney's virtual seed landing: ",
                    exception);
                Destroy(gameObject);
            }
        }

        internal void AttachPlant(FlowerLevelVenusSpawn spawnedPlant)
        {
            if (spawnedPlant == null || plantWasAttached)
                return;

            try
            {
                plant = spawnedPlant;
                plantWasAttached = true;
                WrapPlantScaleWithoutChangingNativeMovement();
                CreatorToolsInteractionPresentation.
                    MarkInheritedGameplayCameraScale(
                        plant.gameObject,
                        cameraScale);
                CreatorToolsInteractionPresentation.BringActorToFront(
                    plant.gameObject);

                var anchor = FindLabelAnchor(plant.gameObject);
                var activeLabel = seedLabel;
                if (activeLabel == null || !activeLabel.RebindTo(
                        plant.gameObject,
                        anchor,
                        GrowingLabelFollowSeconds))
                {
                    CreatorToolsInteractionPresentation.PrepareActor(
                        plant.gameObject,
                        anchor,
                        donor,
                        logWarning);
                    activeLabel = plant.gameObject.GetComponent<
                        CreatorToolsDonorLabel>();
                    if (activeLabel != null)
                        activeLabel.Hide();
                }
                if (activeLabel != null)
                {
                    activeLabel.SetVerticalOffsetPixels(
                        DonorLabelVerticalOffsetPixels);
                    activeLabel.FadeInWhenActorVisible(
                        DonorLabelFadeInSeconds);
                }
            }
            catch (Exception exception)
            {
                Warn("Could not finish the Cagney seed-to-plant transition: ",
                    exception);
                Destroy(gameObject);
            }
        }

        private void WrapPlantScaleWithoutChangingNativeMovement()
        {
            if (plant == null)
                return;

            var plantTransform = plant.transform;
            var worldPosition = plantTransform.position;
            var worldRotation = plantTransform.rotation;
            var scaledNative = plantTransform.localScale;
            var nativeScale = new Vector3(
                scaledNative.x / cameraScale,
                scaledNative.y / cameraScale,
                scaledNative.z);

            plantScaleRoot = new GameObject(
                "CreatorTools_CagneyHomingPlant_ScaleRoot");
            plantScaleRoot.transform.position = worldPosition;
            plantScaleRoot.transform.rotation = Quaternion.identity;
            plantScaleRoot.transform.localScale = new Vector3(
                cameraScale,
                cameraScale,
                1f);
            plantTransform.SetParent(plantScaleRoot.transform, false);
            plantTransform.localPosition = Vector3.zero;
            plantTransform.rotation = worldRotation;
            plantTransform.localScale = nativeScale;
        }

        private static float HighestVisiblePoint(GameObject actor)
        {
            if (actor == null)
                return float.MinValue;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            var highest = float.MinValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.sprite == null ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                highest = Mathf.Max(highest, renderer.bounds.max.y);
            }
            return highest;
        }

        private static SpriteRenderer FindLabelAnchor(GameObject actor)
        {
            if (actor == null)
                return null;
            var renderers = actor.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer fallback = null;
            SpriteRenderer best = null;
            var bestArea = -1f;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;
                if (fallback == null ||
                    (renderer.enabled && renderer.gameObject.activeInHierarchy))
                    fallback = renderer;
                if (renderer.sprite == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                    continue;
                var size = renderer.sprite.bounds.size;
                var area = Mathf.Abs(size.x * size.y);
                if (area <= bestArea)
                    continue;
                best = renderer;
                bestArea = area;
            }
            return best == null ? fallback : best;
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
            if (seed != null)
                Destroy(seed.gameObject);
            if (plant != null)
                Destroy(plant.gameObject);
            if (plantScaleRoot != null)
                Destroy(plantScaleRoot);
            seed = null;
            plant = null;
            plantScaleRoot = null;
            seedLabel = null;
        }
    }

    internal sealed class CreatorToolsCagneySeedMarker : MonoBehaviour
    {
        internal CagneyHomingPlantInteractionState State;
    }
}
