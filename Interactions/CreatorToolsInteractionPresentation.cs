using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsInteractionPresentation
    {
        // Leave a small range above the actor for its donor label while still
        // rendering everything through Cuphead's gameplay camera and filters.
        private const int FrontActorSortingOrder = short.MaxValue - 64;

        internal static void BringActorToFront(GameObject actor)
        {
            if (actor == null)
                return;

            var renderers = actor.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            var highestLayerId = HighestSortingLayerId();
            var maximumOrder = int.MinValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    maximumOrder = Mathf.Max(
                        maximumOrder, renderers[i].sortingOrder);
            }
            if (maximumOrder == int.MinValue)
                return;

            var shift = FrontActorSortingOrder - maximumOrder;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;
                renderer.sortingLayerID = highestLayerId;
                renderer.sortingOrder = Mathf.Clamp(
                    renderer.sortingOrder + shift,
                    short.MinValue,
                    FrontActorSortingOrder);
            }
        }

        internal static void PrepareActor(
            GameObject actor,
            string donor,
            Action<string> logWarning)
        {
            if (actor == null)
                return;

            try
            {
                BringActorToFront(actor);
            }
            catch (Exception exception)
            {
                Warn(logWarning,
                    "Could not move the interaction actor forward: ",
                    exception);
            }

            try
            {
                var label = actor.AddComponent<CreatorToolsDonorLabel>();
                label.Initialize(donor);
            }
            catch (Exception exception)
            {
                // Presentation must never invalidate an already-created
                // gameplay actor. Keep the enemy and report the label issue.
                Warn(logWarning,
                    "The interaction actor spawned without its donor label: ",
                    exception);
            }
        }

        private static void Warn(
            Action<string> logWarning,
            string prefix,
            Exception exception)
        {
            if (logWarning != null)
                logWarning(prefix + exception);
        }

        private static int HighestSortingLayerId()
        {
            var layers = SortingLayer.layers;
            if (layers == null || layers.Length == 0)
                return 0;

            var highest = layers[0];
            for (var i = 1; i < layers.Length; i++)
                if (layers[i].value > highest.value)
                    highest = layers[i];
            return highest.id;
        }
    }
}
