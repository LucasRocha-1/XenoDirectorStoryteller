using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using XenoDirector;

var manager = new RestrictionManager { RestrictsRaids = true };
manager.AllowedKinds.Add("allowed");
XenoDirectorGameComponent.Manager = manager;

var originalOptions = new List<PawnGenOption>
{
    new PawnGenOption { kind = new PawnKindDef { defName = "allowed" } },
    new PawnGenOption { kind = new PawnKindDef { defName = "denied" } }
};
var maker = new PawnGroupMaker { options = originalOptions };
var raid = new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat };
IEnumerable<Pawn> result = null;
List<PawnGenOption> state = null;

Assert(Patch_PawnGroupMaker_Instance_GeneratePawns.Prefix(maker, raid, ref result, ref state),
    "allowed raids should continue through generation");
Assert(!ReferenceEquals(maker.options, originalOptions) && maker.options.Count == 1,
    "only permitted pawn kinds should be visible during generation");

result = GenerateLazilyFromOptions(maker);
Patch_PawnGroupMaker_Instance_GeneratePawns.Postfix(ref result, state);
Assert(result.Select(pawn => pawn.KindName).SequenceEqual(new[] { "allowed" }),
    "lazy generation should be evaluated before restoring the options");
Patch_PawnGroupMaker_Instance_GeneratePawns.Finalizer(maker, state, null);
Assert(ReferenceEquals(maker.options, originalOptions),
    "the original options list should be restored by reference");

manager.AllowedKinds.Clear();
result = null;
state = null;
Assert(!Patch_PawnGroupMaker_Instance_GeneratePawns.Prefix(maker, raid, ref result, ref state),
    "generation should stop when no permitted pawn kinds exist");
Assert(!result.Any(), "a blocked maker should produce no pawns");
Assert(ReferenceEquals(maker.options, originalOptions),
    "the empty-filter case must not mutate the original list");

manager.AllowedKinds.Add("allowed");
result = null;
state = null;
Assert(Patch_PawnGroupMaker_Instance_GeneratePawns.Prefix(maker, raid, ref result, ref state),
    "the exception scenario should enter generation");
result = ThrowDuringEnumeration();
Exception failure = null;
try
{
    Patch_PawnGroupMaker_Instance_GeneratePawns.Postfix(ref result, state);
}
catch (InvalidOperationException exception)
{
    failure = exception;
}
Assert(failure != null, "lazy enumeration errors should remain visible");
Assert(ReferenceEquals(
        Patch_PawnGroupMaker_Instance_GeneratePawns.Finalizer(maker, state, failure),
        failure),
    "the finalizer should preserve enumeration errors");
Assert(ReferenceEquals(maker.options, originalOptions),
    "the original options list should be restored after enumeration fails");

var nonRaid = new PawnGroupMakerParms { groupKind = new PawnGroupKindDef() };
state = null;
Assert(Patch_PawnGroupMaker_Instance_GeneratePawns.Prefix(maker, nonRaid, ref result, ref state),
    "non-raid groups should keep vanilla behavior");
Assert(state == null && ReferenceEquals(maker.options, originalOptions),
    "non-raid groups should not mutate maker options");

var matchingIdeology = new FakeIdeology
{
    PreceptsListForReading = new List<object>
    {
        new Precept_HatedXenotype { xenotypeDefName = "Xenotype_Hussar" }
    }
};
Assert(IdeologyXenotypeMatcher.IsHatedByAnyIdeo(
        new object[] { matchingIdeology },
        new[] { "Xenotype_Hussar" }),
    "a modded hatred precept matching the pawn xenotype should be detected");
Assert(IdeologyXenotypeMatcher.IsHatedByAnyIdeo(
        new object[]
        {
            new FakeIdeology
            {
                PreceptsListForReading = new List<object>
                {
                    new Precept_XenotypeRule
                    {
                        def = new FakeDef { defName = "HatedXenotypeRule" },
                        xenotypeDefName = "Xenotype_Hussar"
                    }
                }
            }
        },
        new[] { "Xenotype_Hussar" }),
    "hatred and xenotype data split across modded precept fields should be matched");
Assert(!IdeologyXenotypeMatcher.IsHatedByAnyIdeo(
        new object[] { matchingIdeology },
        new[] { "Xenotype_Pigskin" }),
    "a hatred precept for a different xenotype should not match");

var preferredIdeology = new FakeIdeology
{
    PreceptsListForReading = new List<object>
    {
        new Precept_PreferredXenotype { xenotypeDefName = "Xenotype_Hussar" }
    }
};
Assert(!IdeologyXenotypeMatcher.IsHatedByAnyIdeo(
        new object[] { preferredIdeology },
        new[] { "Xenotype_Hussar" }),
    "preference alone must not be interpreted as hatred");
Assert(IdeologyXenotypeMatcher.IsHatedByAnyIdeo(
        new object[]
        {
            new FakeIdeology
            {
                PreceptsListForReading = new List<object>
                {
                    new Precept_DisfavoredXenotype { xenotypeDefName = "Xenotype_Hussar" }
                }
            }
        },
        new[] { "Xenotype_Hussar" }),
    "modded precepts that disfavor a xenotype should be treated as negative");
var ideologyTracker = new FakeIdeologyTracker
{
    AllIdeos = new object[] { preferredIdeology, matchingIdeology },
    PrimaryIdeo = new FakeIdeology
    {
        PreceptsListForReading = new List<object>()
    }
};
Assert(IdeologyXenotypeMatcher.GetIdeologies(ideologyTracker).Count() == 3,
    "the primary ideology should be checked alongside all minor ideologies");
var pawn = new Pawn
{
    genes = new GeneTracker { Xenotype = new FakeXenotype { defName = "Xenotype_Hussar" } }
};
Assert(IdeologyXenotypeMatcher.GetPawnXenotypeNames(pawn).Contains("Xenotype_Hussar"),
    "the matching input should include a pawn's xenotype def name");

DefDatabase<QuestScriptDef>.AllDefsListForReading = new List<QuestScriptDef>
{
    new QuestScriptDef
    {
        defName = "Mod_ReturnEscort",
        label = "A return escort",
        IsRootRandomSelected = true,
        modContentPack = new ModContentPack { PackageId = "example.escortmod" }
    },
    new QuestScriptDef
    {
        defName = "Vanilla_SpecialEscort",
        label = "Special escort",
        IsRootRandomSelected = false
    },
    new QuestScriptDef
    {
        defName = "Mod_Unrelated",
        label = "Unrelated quest",
        IsRootRandomSelected = true
    }
};
QuestGen.GeneratedCount = 0;
var generatedQuest = QuestFabricator.GenerateReturnEscort(pawn, null, new Map());
Assert(generatedQuest != null && generatedQuest.name.StartsWith("[XD] Return escort: "),
    "the fabricator should transform a matching loaded quest script");
Assert(generatedQuest.TicksUntilExpiry == 1150,
    "active DLC context should adjust the generated quest expiry");
var secondGeneratedQuest = QuestFabricator.GenerateReturnEscort(pawn, null, new Map());
Assert(secondGeneratedQuest != null && !ReferenceEquals(generatedQuest, secondGeneratedQuest),
    "each request should generate a fresh quest instance rather than reuse a cached quest");
Assert(QuestGen.GeneratedCount == 2,
    "quest generation should happen only when the fabricator is called");

Console.WriteLine("Restriction patch tests passed.");

static IEnumerable<Pawn> GenerateLazilyFromOptions(PawnGroupMaker maker)
{
    foreach (var option in maker.options)
        yield return new Pawn { KindName = option.kind.defName };
}

static IEnumerable<Pawn> ThrowDuringEnumeration()
{
    throw new InvalidOperationException("simulated lazy generation failure");
#pragma warning disable CS0162
    yield break;
#pragma warning restore CS0162
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class FakeIdeology
{
    public List<object> PreceptsListForReading { get; set; }
}

sealed class FakeIdeologyTracker
{
    public IEnumerable<object> AllIdeos { get; set; }
    public object PrimaryIdeo { get; set; }
}

sealed class Precept_HatedXenotype
{
    public string xenotypeDefName;
}

sealed class Precept_PreferredXenotype
{
    public string xenotypeDefName;
}

sealed class Precept_DisfavoredXenotype
{
    public string xenotypeDefName;
}

sealed class Precept_XenotypeRule
{
    public FakeDef def;
    public string xenotypeDefName;
}

sealed class FakeDef
{
    public string defName;
}

sealed class FakeXenotype
{
    public string defName;
}
