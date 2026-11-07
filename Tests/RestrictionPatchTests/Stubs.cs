using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string methodName) { }
    }

    public static class AccessTools
    {
        public static Type TypeByName(string typeName) => null;
        public static MethodBase Method(Type type, string methodName) => null;
    }
}

namespace Verse
{
    public sealed class Pawn
    {
        public string KindName;
        public GeneTracker genes;
    }

    public sealed class GeneTracker
    {
        public object Xenotype;
        public object CustomXenotype;
    }

    public static class Log
    {
        public static void Warning(string message) { }
    }

    public sealed class Map { }

    public sealed class Slate
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        public void Set(string key, object value) => _values[key] = value;
    }

    public static class Rand
    {
        public static int Range(int min, int max) => min;
    }

    public static class DefDatabase<T>
    {
        public static List<T> AllDefsListForReading { get; set; } = new List<T>();
    }
}

namespace RimWorld
{
    using System.Collections.Generic;
    using System.Linq;
    using Verse;

    public sealed class IncidentDef { }
    public sealed class IncidentParms { }
    public sealed class IncidentWorker
    {
        public IncidentDef def;
        public bool CanFireNow(IncidentParms parms) => true;
    }

    public sealed class PawnKindDef
    {
        public string defName;
    }

    public sealed class PawnGenOption
    {
        public PawnKindDef kind;
    }

    public sealed class PawnGroupKindDef { }

    public static class PawnGroupKindDefOf
    {
        public static readonly PawnGroupKindDef Combat = new PawnGroupKindDef();
        public static readonly PawnGroupKindDef Settlement = new PawnGroupKindDef();
    }

    public sealed class PawnGroupMakerParms
    {
        public PawnGroupKindDef groupKind;
    }

    public sealed class PawnGroupMaker
    {
        public List<PawnGenOption> options;
        public IEnumerable<Pawn> GeneratePawns(PawnGroupMakerParms parms, bool errorOnZeroResults)
            => Enumerable.Empty<Pawn>();
    }

    public static class PawnGroupMakerUtility
    {
        public static IEnumerable<Pawn> GeneratePawns(PawnGroupMakerParms parms)
            => Enumerable.Empty<Pawn>();
    }

    public sealed class QuestPart_SpawnPawn
    {
        public void Notify_QuestSignalReceived() { }
    }

    public sealed class Faction { }

    public sealed class Quest
    {
        public string name;
        public int TicksUntilExpiry;
    }

    public sealed class QuestScriptDef
    {
        public string defName;
        public string label;
        public bool IsRootRandomSelected;
        public bool Runnable = true;
        public ModContentPack modContentPack;
        public bool CanRun(RimWorld.QuestGen.Slate slate, Verse.Map map) => Runnable;
    }

    public sealed class ModContentPack
    {
        public string PackageId;
    }

    public static class StorytellerUtility
    {
        public static float DefaultThreatPointsNow(Verse.Map map) => 500f;
    }

    public static class ModsConfig
    {
        public static bool BiotechActive = true;
        public static bool IdeologyActive = true;
    }
}

namespace RimWorld.QuestGen
{
    using RimWorld;

    public sealed class Slate
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        public void Set(string key, object value) => _values[key] = value;
    }

    public static class QuestGen
    {
        public static int GeneratedCount;
        public static Quest Generate(QuestScriptDef script, Slate slate)
        {
            GeneratedCount++;
            return new Quest { name = script.label, TicksUntilExpiry = 1000 };
        }
    }
}

namespace XenoDirector
{
    using RimWorld;

    public sealed class XenoDirectorSettings
    {
        public bool EnableRestrictionsAlways = true;
    }

    public static class XenoDirectorMod
    {
        public static XenoDirectorSettings Settings = new XenoDirectorSettings();
    }

    public sealed class RestrictionManager
    {
        public bool RestrictsRaids;
        public readonly HashSet<string> AllowedKinds = new HashSet<string>();

        public bool IsRaidPawnKindAllowed(PawnKindDef kind)
            => kind != null && AllowedKinds.Contains(kind.defName);

        public bool IsIncidentBlocked(IncidentDef def) => false;
        public bool BlocksQuestPawnRewards => false;
        public bool BlocksRecruitment => false;
    }

    public static class XenoDirectorGameComponent
    {
        public static RestrictionManager Manager;
        public static RestrictionManager GetRestrictions() => Manager;
        public static bool IsXenoStorytellerActive() => false;
    }
}
