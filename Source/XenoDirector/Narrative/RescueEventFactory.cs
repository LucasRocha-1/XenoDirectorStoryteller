using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    public interface INarrativeContentFactory
    {
        RescueEventPlan CreateEvent(StoryArc arc);
        Quest CreateMission(StoryArc arc);
    }

    public enum RescueEventKind
    {
        XenotypePursuit,
        RivalAmbush,
        RescueInterference,
        RouteDisruption,
        ReturnEscort
    }

    public sealed class RescueEventPlan
    {
        public RescueEventKind Kind;
        public Faction Faction;
        public string LetterLabel;
        public string LetterText;
    }

    /// <summary>
    /// Builds a short-lived rescue event from current world state. Candidate plans
    /// are deliberately recreated for each arc step and are never serialized.
    /// </summary>
    public sealed class RescueEventFactory : INarrativeContentFactory
    {
        public static readonly RescueEventFactory Instance = new RescueEventFactory();

        private RescueEventFactory()
        {
        }

        public RescueEventPlan CreateEvent(StoryArc arc)
        {
            if (arc == null) return null;
            if (arc.CurrentStep == 2)
            {
                return new RescueEventPlan { Kind = RescueEventKind.ReturnEscort };
            }

            var hostileFactions = (Find.FactionManager?.AllFactionsVisible ?? Enumerable.Empty<Faction>())
                .Where(IsPlayerHostile)
                .ToList();
            var origin = NarrativeWorldAccess.Instance.ResolveFaction(arc.AnchorFactionId);
            var rivals = origin == null
                ? new List<Faction>()
                : hostileFactions.Where(faction => faction != origin && faction.HostileTo(origin)).ToList();
            var xenotypeHunters = FindXenotypeHunters(arc, hostileFactions);
            var candidates = new List<RescueEventPlan>();

            if (arc.CurrentStep == 0 || arc.CurrentStep == 3)
            {
                candidates.AddRange(xenotypeHunters.Select(faction => CreateFactionPlan(
                    RescueEventKind.XenotypePursuit,
                    faction,
                    "Xenotype hunters",
                    $"{faction.Name} is pursuing {GetPawnLabel(arc)} because its ideology rejects the pawn's xenotype.")));
            }

            if (arc.CurrentStep == 0 || arc.CurrentStep == 1 || arc.CurrentStep == 3)
            {
                candidates.AddRange(rivals.Select(faction => CreateFactionPlan(
                    RescueEventKind.RivalAmbush,
                    faction,
                    "Rival ambush",
                    $"{faction.Name}, a rival of the rescued pawn's former faction, has learned of the rescue and is trying to intercept the return.")));
            }

            if (arc.CurrentStep == 1 || arc.CurrentStep == 3 || arc.CurrentStep >= 4)
            {
                candidates.AddRange(hostileFactions.Select(faction => CreateFactionPlan(
                    RescueEventKind.RescueInterference,
                    faction,
                    "Rescue interrupted",
                    $"{faction.Name} has discovered the rescue and is attacking before the rescued pawn can be returned.")));
            }

            if (candidates.Count == 0)
            {
                return new RescueEventPlan
                {
                    Kind = RescueEventKind.RouteDisruption,
                    LetterLabel = "Return route disrupted",
                    LetterText = $"News of {GetPawnLabel(arc)}'s rescue has spread. Unrest along the return route is delaying the handover."
                };
            }

            return candidates[Rand.Range(0, candidates.Count)];
        }

        public Quest CreateMission(StoryArc arc)
        {
            if (arc == null) return null;
            var access = NarrativeWorldAccess.Instance;
            var target = access.ResolveAnchorPawn(arc);
            var map = target?.Map ?? Find.AnyPlayerHomeMap;
            var sourceFaction = access.ResolveFaction(arc.AnchorFactionId);
            return QuestFabricator.GenerateReturnEscort(target, sourceFaction, map);
        }

        private static List<Faction> FindXenotypeHunters(StoryArc arc, IEnumerable<Faction> hostileFactions)
        {
            if (!ModsConfig.BiotechActive || !ModsConfig.IdeologyActive)
                return new List<Faction>();

            var target = NarrativeWorldAccess.Instance.ResolveAnchorPawn(arc);
            var xenotypeNames = IdeologyXenotypeMatcher.GetPawnXenotypeNames(target).ToList();
            if (!string.IsNullOrWhiteSpace(arc.AnchorXenotypeDefName))
                xenotypeNames.Add(arc.AnchorXenotypeDefName);

            if (xenotypeNames.Count == 0) return new List<Faction>();

            return hostileFactions
                .Where(faction => IdeologyXenotypeMatcher.IsHatedByAnyIdeo(
                    IdeologyXenotypeMatcher.GetIdeologies(faction.ideos),
                    xenotypeNames))
                .ToList();
        }

        private static bool IsPlayerHostile(Faction faction)
        {
            return faction != null
                && !faction.IsPlayer
                && !faction.defeated
                && Faction.OfPlayer != null
                && faction.HostileTo(Faction.OfPlayer);
        }

        private static RescueEventPlan CreateFactionPlan(
            RescueEventKind kind,
            Faction faction,
            string label,
            string text)
        {
            return new RescueEventPlan
            {
                Kind = kind,
                Faction = faction,
                LetterLabel = label,
                LetterText = text
            };
        }

        private static string GetPawnLabel(StoryArc arc)
        {
            return NarrativeWorldAccess.Instance.ResolveAnchorPawn(arc)?.LabelShort ?? "the rescued pawn";
        }
    }
}
