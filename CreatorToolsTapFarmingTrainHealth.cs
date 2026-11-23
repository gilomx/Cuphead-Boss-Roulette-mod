using System;
using System.Globalization;
using System.Reflection;

namespace Gilomx.CupheadBossRoulette
{
    // Phantom Express owns five local HP pools. Its generic properties stay
    // unchanged while the level advances its own currentPhase counter.
    internal sealed class CreatorToolsTapFarmingTrainHealth
    {
        private static readonly FieldInfo PropertiesField = Field(typeof(TrainLevel), "properties");
        private static readonly FieldInfo PhaseField = Field(typeof(TrainLevel), "currentPhase");
        private static readonly FieldInfo SpecterField = Field(typeof(TrainLevel), "blindSpecter");
        private static readonly FieldInfo SkeletonField = Field(typeof(TrainLevel), "skeleton");
        private static readonly FieldInfo GhoulsField = Field(typeof(TrainLevel), "ghouls");
        private static readonly FieldInfo EngineField = Field(typeof(TrainLevel), "engine");
        private static readonly FieldInfo LeftGhoulField = Field(typeof(TrainLevelLollipopGhoulsManager), "ghoulLeft");
        private static readonly FieldInfo RightGhoulField = Field(typeof(TrainLevelLollipopGhoulsManager), "ghoulRight");
        private static readonly FieldInfo[] HealthFields = {
            Field(typeof(TrainLevelBlindSpecter), "health"),
            Field(typeof(TrainLevelSkeleton), "health"),
            Field(typeof(TrainLevelLollipopGhoul), "health"),
            Field(typeof(TrainLevelLollipopGhoul), "health"),
            Field(typeof(TrainLevelEngineBoss), "health")
        };
        private static readonly PropertyInfo ActorProperties = typeof(LevelProperties.Train.Entity).GetProperty(
            "properties", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly Level level;
        private readonly object[] actors;
        private readonly double[] originalParts;
        private readonly int levelInstanceId;
        internal readonly double[] OriginalHealth;
        internal object Properties { get; private set; }

        private CreatorToolsTapFarmingTrainHealth(Level level, LevelProperties.Train properties,
            object[] actors, double[] originalParts)
        {
            this.level = level;
            Properties = properties;
            this.actors = actors;
            this.originalParts = originalParts;
            levelInstanceId = level.GetInstanceID();
            OriginalHealth = actors.Length == 4
                ? new[] { originalParts[0], originalParts[1], originalParts[2] + originalParts[3] }
                : new[] { originalParts[0], originalParts[1], originalParts[2] + originalParts[3], originalParts[4] };
        }

        internal static bool TryCapture(Level level, object properties,
            out CreatorToolsTapFarmingTrainHealth adapter)
        {
            adapter = null;
            if (!(level is TrainLevel) || level == null ||
                !ReferenceEquals(Level.Current, level) || !MetadataAvailable()) return false;
            try
            {
                var owned = PropertiesField.GetValue(level) as LevelProperties.Train;
                if (owned == null || (properties != null && !ReferenceEquals(properties, owned))) return false;
                var state = owned.CurrentState;
                if (state == null || state.blindSpecter == null || state.skeleton == null ||
                    state.lollipopGhouls == null || (level.mode != Level.Mode.Easy && state.engine == null)) return false;
                var manager = GhoulsField.GetValue(level);
                if (manager == null) return false;
                var originals = new double[] { state.blindSpecter.health, state.skeleton.health,
                    state.lollipopGhouls.health, state.lollipopGhouls.health,
                    level.mode == Level.Mode.Easy ? 0d : state.engine.health };
                var count = level.mode == Level.Mode.Easy ? 4 : 5;
                var allActors = new[] { SpecterField.GetValue(level), SkeletonField.GetValue(level),
                    LeftGhoulField.GetValue(manager), RightGhoulField.GetValue(manager), EngineField.GetValue(level) };
                var actors = new object[count];
                for (var i = 0; i < count; i++)
                {
                    var actor = allActors[i];
                    if (actor == null || !ReferenceEquals(ActorProperties.GetValue(actor, null), owned) ||
                        !(originals[i] > 0d) || double.IsInfinity(originals[i])) return false;
                    actors[i] = actor;
                }
                adapter = new CreatorToolsTapFarmingTrainHealth(level, owned, actors, originals);
                return true;
            }
            catch { return false; }
        }

        internal bool OwnsActor(object actor)
        {
            if (actor == null || !IsCurrentLevel()) return false;
            for (var i = 0; i < actors.Length; i++)
                if (ReferenceEquals(actors[i], actor)) return true;
            return false;
        }

        internal bool TryRead(out int phaseIndex, out double currentNative,
            out double currentPhaseNative)
        {
            phaseIndex = 0;
            currentNative = currentPhaseNative = 0d;
            if (!IsCurrentLevel()) return false;
            try
            {
                if (!ReferenceEquals(PropertiesField.GetValue(level), Properties)) return false;
                // Before OnLevelStart the level may still be initializing its
                // actors. Keep the original HP until currentPhase becomes one.
                var nativePhase = Convert.ToInt32(PhaseField.GetValue(level), CultureInfo.InvariantCulture);
                phaseIndex = Math.Max(1, nativePhase);
                if (phaseIndex > OriginalHealth.Length) return false;
                if (nativePhase == 0)
                    currentPhaseNative = OriginalHealth[0];
                else if (phaseIndex == 3)
                    currentPhaseNative = ReadPart(2) + ReadPart(3);
                else
                    currentPhaseNative = ReadPart(phaseIndex == 4 ? 4 : phaseIndex - 1);
                currentNative = currentPhaseNative;
                for (var next = phaseIndex; next < OriginalHealth.Length; next++)
                    currentNative += OriginalHealth[next];
                return !double.IsNaN(currentNative) && !double.IsInfinity(currentNative);
            }
            catch { return false; }
        }

        private double ReadPart(int index)
        {
            var health = Convert.ToDouble(HealthFields[index].GetValue(actors[index]), CultureInfo.InvariantCulture);
            return Math.Max(0d, Math.Min(originalParts[index], health));
        }

        private bool IsCurrentLevel()
        {
            return level != null && ReferenceEquals(Level.Current, level) && level.GetInstanceID() == levelInstanceId;
        }

        private static bool MetadataAvailable()
        {
            if (PropertiesField == null || PhaseField == null || SpecterField == null ||
                SkeletonField == null || GhoulsField == null || EngineField == null ||
                LeftGhoulField == null || RightGhoulField == null || ActorProperties == null) return false;
            for (var i = 0; i < HealthFields.Length; i++)
                if (HealthFields[i] == null) return false;
            return true;
        }

        private static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
