using System;
using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    // Spawn pressure is independent of Cuphead's native difficulty and of
    // the existing attack/mini_boss catalog type. Unknown future IDs are
    // deliberately conservative until they receive an explicit assignment.
    internal static class CreatorToolsInteractionGroups
    {
        internal const string Light = "light";
        internal const string Strong = "strong";
        internal const string MiniBoss = "mini_boss";

        internal static bool CanAddStrongInteraction(string item,
            bool allowConcurrent, int activeStrongCount)
        {
            return ForItem(item) != Strong || allowConcurrent || activeStrongCount == 0;
        }

        internal static string ForItem(string item)
        {
            switch (item)
            {
                case "hilda_purple_zeppelin":
                case "rootpack_homing_carrot":
                case "cagney_homing_plant":
                case "frogs_firefly":
                case "beppi_pink_balloon_dog":
                    return Light;
                case "hilda_green_zeppelin":
                case "robot_homing_bomb":
                case "baroness_head_toss":
                case "dragon_fireballs":
                case "train_bone_ring":
                case "devil_fire_circle":
                case "beppi_balloon_dog":
                    return Strong;
                case "baroness_cupcake":
                case "baroness_gumball":
                case "baroness_waffle":
                case "baroness_candy_corn":
                case "baroness_jawbreaker":
                    return MiniBoss;
                default:
                    return Strong;
            }
        }

        internal static CreatorToolsAutomaticSpawnPlan PlanAutomaticSpawn(
            IEnumerable<string> catalog, bool miniBossReady, bool commonReady,
            Func<string, bool> isEnabled, Func<string, bool> isAvailable,
            Func<string, bool> canAdmitCommon, bool otherMiniBossReserved = false)
        {
            var candidates = new List<string>();
            if (miniBossReady)
            {
                foreach (var item in catalog)
                    if (ForItem(item) == MiniBoss && isEnabled(item) && isAvailable(item))
                        candidates.Add(item);
            }
            // Reserve before considering common admissions. A crowded arena
            // can drain for its due mini; an incompatible or disabled mini
            // never prevents ordinary generation.
            if (candidates.Count > 0)
                return new CreatorToolsAutomaticSpawnPlan(true, candidates);

            // Reuse the empty candidate buffer and reject unavailable slots
            // before querying Unity. A ready clock can stay blocked for many
            // frames while existing actors finish.
            if (commonReady && !otherMiniBossReserved)
                foreach (var item in catalog)
                    if (ForItem(item) != MiniBoss && isEnabled(item) &&
                        canAdmitCommon(item) && isAvailable(item))
                        candidates.Add(item);
            return new CreatorToolsAutomaticSpawnPlan(false, candidates);
        }

        // A batch consists of existing, eligible entries. Selection and
        // capacity are reevaluated after every successful spawn; the caller
        // retains all entries that were not admitted. No quantity is minted.
        internal static int DispatchBatch<T>(T first, int maximum,
            Func<T, bool> tryDispatch, Func<T> next) where T : class
        {
            var dispatched = 0;
            var entry = first;
            while (entry != null && dispatched < maximum)
            {
                if (!tryDispatch(entry))
                    break;
                dispatched++;
                if (dispatched < maximum)
                    entry = next();
            }
            return dispatched;
        }
    }

    internal sealed class CreatorToolsAutomaticSpawnPlan
    {
        internal readonly bool MiniBossReserved;
        internal readonly List<string> Candidates;

        internal CreatorToolsAutomaticSpawnPlan(bool miniBossReserved, List<string> candidates)
        {
            MiniBossReserved = miniBossReserved;
            Candidates = candidates;
        }
    }
}
