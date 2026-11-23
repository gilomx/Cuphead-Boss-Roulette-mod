using System;
using System.Globalization;
using System.Reflection;

namespace Gilomx.CupheadBossRoulette
{
    // Tipsy Troop keeps three local pools; its generic 500 HP pool only
    // advances the level after all three actors have finished dying.
    internal sealed class CreatorToolsTapFarmingBoozeHealth
    {
        private static readonly FieldInfo PropertiesField = Field(typeof(DicePalaceBoozeLevel), "properties");
        private static readonly FieldInfo[] ActorFields = {
            Field(typeof(DicePalaceBoozeLevel), "decanter"),
            Field(typeof(DicePalaceBoozeLevel), "martini"),
            Field(typeof(DicePalaceBoozeLevel), "tumbler")
        };
        private static readonly FieldInfo HealthField = Field(typeof(DicePalaceBoozeLevelBossBase), "health");
        private static readonly PropertyInfo IsDead = typeof(DicePalaceBoozeLevelBossBase).GetProperty(
            "isDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo ActorProperties = typeof(LevelProperties.DicePalaceBooze.Entity).GetProperty(
            "properties", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly Level level;
        private readonly int levelInstanceId;
        private readonly object[] actors;
        private readonly double[] originals;
        internal object Properties { get; private set; }
        internal readonly double OriginalHealth;

        private CreatorToolsTapFarmingBoozeHealth(Level level, object properties, object[] actors, double[] originals)
        {
            this.level = level;
            levelInstanceId = level.GetInstanceID();
            Properties = properties;
            this.actors = actors;
            this.originals = originals;
            for (var i = 0; i < originals.Length; i++) OriginalHealth += originals[i];
        }

        internal static bool TryCapture(Level level, out CreatorToolsTapFarmingBoozeHealth adapter)
        {
            adapter = null;
            if (!(level is DicePalaceBoozeLevel) || level == null ||
                !ReferenceEquals(Level.Current, level) || PropertiesField == null || HealthField == null ||
                IsDead == null || ActorProperties == null) return false;
            try
            {
                var properties = PropertiesField.GetValue(level) as LevelProperties.DicePalaceBooze;
                if (properties == null || properties.CurrentState == null) return false;
                var state = properties.CurrentState;
                if (state.decanter == null || state.martini == null || state.tumbler == null) return false;
                var originals = new double[] { state.decanter.decanterHP, state.martini.martiniHP, state.tumbler.tumblerHP };
                var actors = new object[ActorFields.Length];
                for (var i = 0; i < actors.Length; i++)
                {
                    if (ActorFields[i] == null || !(originals[i] > 0d) || double.IsInfinity(originals[i])) return false;
                    actors[i] = ActorFields[i].GetValue(level);
                    if (actors[i] == null || !ReferenceEquals(ActorProperties.GetValue(actors[i], null), properties)) return false;
                }
                adapter = new CreatorToolsTapFarmingBoozeHealth(level, properties, actors, originals);
                return true;
            }
            catch { return false; }
        }

        internal bool ContainsActor(object actor)
        {
            if (actor == null || !IsCurrentLevel()) return false;
            for (var i = 0; i < actors.Length; i++)
                if (ReferenceEquals(actors[i], actor)) return true;
            return false;
        }

        internal bool OwnsActor(object actor)
        {
            if (!ContainsActor(actor)) return false;
            try
            {
                var health = Convert.ToDouble(HealthField.GetValue(actor), CultureInfo.InvariantCulture);
                return !(bool)IsDead.GetValue(actor, null) && health >= 0d && !double.IsInfinity(health) &&
                    ReferenceEquals(ActorProperties.GetValue(actor, null), Properties);
            }
            catch { return false; }
        }

        internal bool TryRead(out double currentHealth, out double totalHealth)
        {
            currentHealth = 0d;
            totalHealth = OriginalHealth;
            if (!IsCurrentLevel()) return false;
            try
            {
                for (var i = 0; i < actors.Length; i++)
                {
                    if (!ReferenceEquals(ActorProperties.GetValue(actors[i], null), Properties)) return false;
                    var health = Convert.ToDouble(HealthField.GetValue(actors[i]), CultureInfo.InvariantCulture);
                    if (double.IsNaN(health) || double.IsInfinity(health)) return false;
                    currentHealth += Math.Max(0d, Math.Min(originals[i], health));
                }
                return true;
            }
            catch { return false; }
        }

        private bool IsCurrentLevel()
        {
            return level != null && ReferenceEquals(Level.Current, level) && level.GetInstanceID() == levelInstanceId &&
                ReferenceEquals(PropertiesField.GetValue(level), Properties);
        }

        private static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
