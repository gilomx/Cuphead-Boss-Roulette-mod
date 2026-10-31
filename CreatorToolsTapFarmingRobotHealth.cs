using System;
using System.Globalization;
using System.Reflection;

namespace Gilomx.CupheadBossRoulette
{
    // Robot's first phase uses four local HP pools rather than the generic
    // property's initial health segment. Read only this level's owned actors.
    internal sealed class CreatorToolsTapFarmingRobotHealth
    {
        private static readonly FieldInfo LevelPropertiesField = Field(typeof(RobotLevel), "properties");
        private static readonly FieldInfo RobotField = Field(typeof(RobotLevel), "robot");
        private static readonly FieldInfo HeadField = Field(typeof(RobotLevelRobot), "head");
        private static readonly FieldInfo ChestField = Field(typeof(RobotLevelRobot), "chest");
        private static readonly FieldInfo HatchField = Field(typeof(RobotLevelRobot), "hatch");
        private static readonly FieldInfo PartPropertiesField = Field(typeof(RobotLevelRobotBodyPart), "properties");
        private static readonly FieldInfo PartHealthField = Field(typeof(RobotLevelRobotBodyPart), "currentHealth");
        private static readonly FieldInfo StatesField = Field(typeof(LevelProperties.Robot), "states");
        private static readonly FieldInfo StateIndexField = Field(typeof(LevelProperties.Robot), "stateIndex");
        private static readonly FieldInfo TotalField = Field(typeof(LevelProperties.Robot), "TotalHealth");
        private static readonly PropertyInfo CurrentProperty = typeof(LevelProperties.Robot).GetProperty(
            "CurrentHealth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly Level level;
        private readonly object properties;
        private readonly int[] nativeStateIndices;
        private readonly double[] phaseEnds;
        private readonly double totalGenericHealth;
        private readonly double headOriginal, chestOriginal, hatchOriginal, heartOriginal;
        private float[] headHealth, chestHealth, hatchHealth;

        internal readonly int LevelInstanceId;
        // Captured once; callers must treat this array as read-only.
        internal readonly double[] OriginalHealth;
        internal object Properties { get { return properties; } }

        private CreatorToolsTapFarmingRobotHealth(Level level, object properties,
            double[] original, int[] indices, double[] ends, double genericTotal,
            double head, double chest, double hatch, double heart)
        {
            this.level = level;
            this.properties = properties;
            LevelInstanceId = level.GetInstanceID();
            OriginalHealth = original;
            nativeStateIndices = indices;
            phaseEnds = ends;
            totalGenericHealth = genericTotal;
            headOriginal = head;
            chestOriginal = chest;
            hatchOriginal = hatch;
            heartOriginal = heart;
        }

        internal static bool TryCapture(Level level, object properties,
            out CreatorToolsTapFarmingRobotHealth adapter)
        {
            adapter = null;
            if (!(level is RobotLevel) || level == null ||
                !ReferenceEquals(Level.Current, level) || !MetadataAvailable())
                return false;
            try
            {
                var ownedProperties = LevelPropertiesField.GetValue(level);
                if (properties == null) properties = ownedProperties;
                if (!(properties is LevelProperties.Robot) ||
                    !ReferenceEquals(properties, ownedProperties)) return false;
                var states = StatesField.GetValue(properties) as Array;
                if (states == null || (states.Length != 1 && states.Length != 3)) return false;
                var main = states.GetValue(0);
                if (StateName(main) != 0) return false;
                var head = NestedHealth(main, "hose", "health");
                var chest = NestedHealth(main, "orb", "chestHP");
                var hatch = NestedHealth(main, "shotBot", "hatchGateHealth");
                var heart = NestedHealth(main, "heart", "heartHP");
                var total = Number(TotalField.GetValue(properties));
                if (!Positive(head) || !Positive(chest) || !Positive(hatch) ||
                    !Positive(heart) || !Positive(total)) return false;
                var original = new double[states.Length];
                var ends = new double[states.Length];
                var indices = new int[states.Length];
                original[0] = head + chest + hatch + heart;
                if (states.Length == 3)
                {
                    var flying = states.GetValue(1);
                    var inventor = states.GetValue(2);
                    if (StateName(flying) != 2 || StateName(inventor) != 3) return false;
                    var flyingStart = HealthTrigger(flying);
                    var inventorStart = HealthTrigger(inventor);
                    if (flyingStart >= 1d || flyingStart <= inventorStart || inventorStart <= 0d) return false;
                    original[1] = total * (flyingStart - inventorStart);
                    original[2] = total * inventorStart;
                    ends[1] = inventorStart;
                    indices[1] = 1;
                    indices[2] = 2;
                }
                adapter = new CreatorToolsTapFarmingRobotHealth(level, properties,
                    original, indices, ends, total, head, chest, hatch, heart);
                return true;
            }
            catch { return false; }
        }

        internal bool TryRead(out int phaseIndex, out double currentNative,
            out double currentPhaseNative)
        {
            phaseIndex = 0;
            currentNative = currentPhaseNative = 0d;
            if (level == null || !ReferenceEquals(Level.Current, level) ||
                level.GetInstanceID() != LevelInstanceId) return false;
            try
            {
                if (!ReferenceEquals(LevelPropertiesField.GetValue(level), properties)) return false;
                var nativeIndex = Convert.ToInt32(StateIndexField.GetValue(properties), CultureInfo.InvariantCulture);
                var index = Array.IndexOf(nativeStateIndices, nativeIndex);
                if (index < 0) return false;
                phaseIndex = index + 1;
                if (index == 0)
                {
                    // Awake allocates zeroed arrays; IntroEnded initializes
                    // all three parts later. Before that, show original HP.
                    if (headHealth == null) TryBindParts();
                    currentPhaseNative = headHealth == null ? OriginalHealth[0] :
                        Clamp(headHealth[0], headOriginal) +
                        Clamp(chestHealth[0], chestOriginal) +
                        Clamp(hatchHealth[0], hatchOriginal) +
                        Clamp(chestHealth[1], heartOriginal);
                }
                else
                {
                    var raw = Number(CurrentProperty.GetValue(properties, null));
                    if (double.IsNaN(raw) || double.IsInfinity(raw)) return false;
                    currentPhaseNative = Clamp(raw - totalGenericHealth * phaseEnds[index], OriginalHealth[index]);
                }
                currentNative = currentPhaseNative;
                for (var next = index + 1; next < OriginalHealth.Length; next++)
                    currentNative += OriginalHealth[next];
                return !double.IsNaN(currentNative) && !double.IsInfinity(currentNative);
            }
            catch { return false; }
        }

        private void TryBindParts()
        {
            var robot = RobotField.GetValue(level) as RobotLevelRobot;
            if (robot == null) return;
            var head = HeadField.GetValue(robot) as RobotLevelRobotBodyPart;
            var chest = ChestField.GetValue(robot) as RobotLevelRobotBodyPart;
            var hatch = HatchField.GetValue(robot) as RobotLevelRobotBodyPart;
            var headArray = PartArray(head);
            var chestArray = PartArray(chest);
            var hatchArray = PartArray(hatch);
            if (headArray == null || chestArray == null || hatchArray == null) return;
            headHealth = headArray;
            chestHealth = chestArray;
            hatchHealth = hatchArray;
        }

        private float[] PartArray(RobotLevelRobotBodyPart part)
        {
            if (part == null || !ReferenceEquals(PartPropertiesField.GetValue(part), properties)) return null;
            var health = PartHealthField.GetValue(part) as float[];
            return health != null && health.Length == 2 ? health : null;
        }

        private static bool MetadataAvailable()
        {
            return LevelPropertiesField != null && RobotField != null && HeadField != null &&
                ChestField != null && HatchField != null && PartPropertiesField != null &&
                PartHealthField != null && StatesField != null && StateIndexField != null &&
                TotalField != null && CurrentProperty != null;
        }
        private static int StateName(object state)
        { return Convert.ToInt32(Field(state.GetType(), "stateName").GetValue(state), CultureInfo.InvariantCulture); }
        private static double HealthTrigger(object state)
        { return Number(Field(state.GetType(), "healthTrigger").GetValue(state)); }
        private static double NestedHealth(object state, string group, string member)
        {
            var value = Field(state.GetType(), group).GetValue(state);
            return Number(Field(value.GetType(), member).GetValue(value));
        }
        private static double Number(object value)
        { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
        private static bool Positive(double value)
        { return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value); }
        private static double Clamp(double value, double maximum)
        { return Math.Max(0d, Math.Min(maximum, value)); }
        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }
    }
}
