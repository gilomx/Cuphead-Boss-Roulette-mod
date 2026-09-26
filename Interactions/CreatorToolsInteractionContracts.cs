using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal static class CreatorToolsInteractionIds
    {
        internal const string GreenZeppelin = "hilda_green_zeppelin";
        internal const string PurpleZeppelin = "hilda_purple_zeppelin";
        internal const string HomingCarrot = "rootpack_homing_carrot";
        internal const string CagneyHomingPlant = "cagney_homing_plant";
        internal const string FrogsFirefly = "frogs_firefly";
        internal const string RobotHomingBomb = "robot_homing_bomb";
        internal const string BaronessHeadToss = "baroness_head_toss";
        internal const string DragonFireballs = "dragon_fireballs";
        internal const string TrainBoneRing = "train_bone_ring";
        internal const string DevilFireCircle = "devil_fire_circle";
        internal const string BeppiBalloonDog = "beppi_balloon_dog";
        internal const string BeppiPinkBalloonDog = "beppi_pink_balloon_dog";
        internal const string BaronessCupcake = "baroness_cupcake";
        internal const string BaronessGumball = "baroness_gumball";
        internal const string BaronessWaffle = "baroness_waffle";
        internal const string BaronessCandyCorn = "baroness_candy_corn";
        internal const string BaronessJawbreaker = "baroness_jawbreaker";
        internal const string HalfDamageChallenge = "challenge_half_damage";
        internal const string NoExChallenge = "challenge_no_ex";
        internal const string NoDashChallenge = "challenge_no_dash";
        internal const string StiffModeChallenge = "challenge_stiff_mode";
        internal const string BlackAndWhiteChallenge = "challenge_black_and_white";
        internal const string NoBombsChallenge = "challenge_no_bombs";
        internal const string NoPeashooterChallenge = "challenge_no_peashooter";
        internal const string RgbShiftChallenge = "challenge_rgb_shift";
        internal const string UpsideDownChallenge = "challenge_upside_down";
        internal const string InkRainChallenge = "challenge_ink_rain";
        internal const string MiniPlaneOnlyChallenge = "challenge_mini_plane_only";
        internal const string HpOneChallenge = "challenge_hp_one";
        internal const string ExtraLifeHelp = "help_extra_life";

        internal static readonly string[] All =
        {
            GreenZeppelin,
            PurpleZeppelin,
            HomingCarrot,
            CagneyHomingPlant,
            FrogsFirefly,
            RobotHomingBomb,
            BaronessHeadToss,
            DragonFireballs,
            TrainBoneRing,
            DevilFireCircle,
            BeppiBalloonDog,
            BeppiPinkBalloonDog,
            BaronessCupcake,
            BaronessGumball,
            BaronessWaffle,
            BaronessCandyCorn,
            BaronessJawbreaker,
            HalfDamageChallenge,
            NoExChallenge,
            NoDashChallenge,
            StiffModeChallenge,
            BlackAndWhiteChallenge,
            NoBombsChallenge,
            NoPeashooterChallenge,
            RgbShiftChallenge,
            UpsideDownChallenge,
            InkRainChallenge,
            MiniPlaneOnlyChallenge,
            HpOneChallenge,
            ExtraLifeHelp
        };
    }

    internal interface ICreatorToolsInteractionExecutor : IDisposable
    {
        // Failure also settles preparation so one unavailable asset cannot
        // hold the native loading screen indefinitely.
        bool NativeAssetsSettled { get; }

        bool Supports(string item);
        bool IsAvailable(string item);
        void Update();
        bool TrySpawn(
            string item,
            string donor,
            string giftImagePath,
            out ICreatorToolsInteractionHandle handle,
            out string feedbackCode,
            out string error);
        void EndGameplayLevel();
    }

    internal interface ICreatorToolsExclusiveInteractionExecutor
    {
        bool BlocksConcurrentSpawn(string item);
    }

    // Durable player aids survive an internal boss phase transition. They are
    // still cleared by the normal end-of-level lifecycle.
    internal interface ICreatorToolsPhasePersistentInteractionExecutor
    {
    }

    internal interface ICreatorToolsTimedInteractionExecutor
    {
        bool TrySpawn(string item, string donor, string giftImagePath, int duration, int countdown,
            out ICreatorToolsInteractionHandle handle, out string feedbackCode, out string error);
    }

    internal interface ICreatorToolsTimedInteractionHandle
    {
        int SecondsRemaining { get; }
        bool CountingDown { get; }
        int CountdownSecondsRemaining { get; }
    }

    internal interface ICreatorToolsLevelRestrictedInteractionExecutor
    {
        bool SupportsCurrentLevel(string item);
    }

    internal interface ICreatorToolsInteractionHandle : IDisposable
    {
        bool IsComplete { get; }
    }

    internal sealed class CreatorToolsUnityObjectInteractionHandle :
        ICreatorToolsInteractionHandle
    {
        private UnityEngine.Object lifetimeObject;
        private GameObject root;
        private Action cleanup;
        private bool disposed;

        internal CreatorToolsUnityObjectInteractionHandle(
            UnityEngine.Object lifetimeObject,
            GameObject root,
            Action cleanup)
        {
            this.lifetimeObject = lifetimeObject;
            this.root = root;
            this.cleanup = cleanup;
        }

        internal CreatorToolsUnityObjectInteractionHandle(
            Component actor)
            : this(
                actor,
                actor == null ? null : actor.gameObject,
                null)
        {
        }

        public bool IsComplete
        {
            get { return disposed || lifetimeObject == null; }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            try
            {
                if (cleanup != null)
                    cleanup();
            }
            finally
            {
                cleanup = null;
                lifetimeObject = null;
                if (root != null)
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
                root = null;
            }
        }
    }
}
