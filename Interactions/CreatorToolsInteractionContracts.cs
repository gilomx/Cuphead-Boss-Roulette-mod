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
        internal const string BaronessCupcake = "baroness_cupcake";
        internal const string BaronessGumball = "baroness_gumball";
        internal const string BaronessWaffle = "baroness_waffle";
        internal const string BaronessCandyCorn = "baroness_candy_corn";
        internal const string BaronessJawbreaker = "baroness_jawbreaker";

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
            BaronessCupcake,
            BaronessGumball,
            BaronessWaffle,
            BaronessCandyCorn,
            BaronessJawbreaker
        };
    }

    internal interface ICreatorToolsInteractionExecutor : IDisposable
    {
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
