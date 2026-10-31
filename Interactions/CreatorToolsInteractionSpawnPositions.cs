using System.Collections.Generic;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // Spawn planners already know their own actors. Add the other catalog
    // actors and real players without searching every object in the scene.
    internal static class CreatorToolsInteractionSpawnPositions
    {
        internal static void AppendTo(List<Vector2> positions)
        {
            var actors = CreatorToolsDonorLabel.Instances;
            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor != null && actor.gameObject != null &&
                    actor.gameObject.activeInHierarchy)
                    positions.Add(actor.transform.position);
            }

            // Cuphead exposes its player dictionary's live ValueCollection.
            // Keep the native collection type so foreach does not box its
            // enumerator. During transitions the dictionary may not exist yet.
            try
            {
                var players = PlayerManager.GetAllPlayers();
                if (players != null)
                {
                    foreach (var player in players)
                        if (player != null && player.gameObject != null &&
                            player.gameObject.activeInHierarchy)
                            positions.Add(player.transform.position);
                    return;
                }
            }
            catch (System.NullReferenceException) { }

            // Preserve scene discovery if a caller plans a spawn before the
            // native player registry is initialized.
            var fallback = Object.FindObjectsOfType<AbstractPlayerController>();
            for (var i = 0; i < fallback.Length; i++)
                if (fallback[i] != null && fallback[i].gameObject != null &&
                    fallback[i].gameObject.activeInHierarchy)
                    positions.Add(fallback[i].transform.position);
        }
    }
}
