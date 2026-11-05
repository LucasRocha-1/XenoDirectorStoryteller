using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Orquestra arcos narrativos sincronizados com sistemas internos do RimWorld:
    /// QuestManager, Faction, World, IncidentQueue, Xenotype, Ideo.
    /// Ativo mesmo sem XenoDirector como storyteller (se settings permitirem).
    /// </summary>
    public class StoryArcManager : IExposable
    {
        private List<StoryArc> _arcs = new List<StoryArc>();
        private int _lastPulseTick;

        public IReadOnlyList<StoryArc> ActiveArcs => _arcs;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _arcs, "Arcs", LookMode.Deep);
            if (_arcs == null) _arcs = new List<StoryArc>();
        }

        public void Tick(XenoDirectorGameComponent gc)
        {
            if (!XenoDirectorMod.Settings.EnableNarrativeArcs) return;
            int tick = Find.TickManager.TicksGame;
            if (tick - _lastPulseTick < 900) return; // ~15s
            _lastPulseTick = tick;

            // Limpa concluídos antigos
            _arcs.RemoveAll(a => (a.Completed || a.Failed) && tick - a.CreatedTick > 60000 * 30);

            foreach (var arc in _arcs.Where(a => !a.Completed && !a.Failed).ToList())
            {
                try { PulseArc(arc, gc); }
                catch (Exception e)
                {
                    Log.Warning($"[XenoDirector.Arc] Pulse failed ({arc.ArcId}): {e.Message}");
                }
            }
        }

        public StoryArc StartRescueChain(Pawn rescued, Faction fromFaction)
        {
            if (rescued == null) return null;

            string xeno = null;
            try
            {
                // Biotech
                if (ModsConfig.BiotechActive && rescued.genes?.Xenotype != null)
                    xeno = rescued.genes.Xenotype.defName;
            }
            catch { /* biotech ausente */ }

            var arc = new StoryArc
            {
                ArcId = "rescue_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ArcType = "RescueChain",
                Title = "Aftermath of rescue: " + rescued.LabelShort,
                CreatedTick = Find.TickManager.TicksGame,
                CurrentStep = 0,
                TotalSteps = 5,
                AnchorPawnLoadId = rescued.GetUniqueLoadID(),
                AnchorFactionId = fromFaction?.def?.defName,
                AnchorXenotypeDefName = xeno,
                PendingEventTags = new List<string>
                {
                    "XenotypeHunters",
                    "AssassinationAttempt",
                    "ReturnEscort",
                    "AmbushOnReturn",
                    "FactionRaidLinked"
                }
            };

            if (fromFaction != null)
                arc.InvolvedFactionIds.Add(fromFaction.GetUniqueLoadID());

            _arcs.Add(arc);
            Log.Message($"[XenoDirector.Arc] Rescue chain started for {rescued.LabelShort} (xeno={xeno ?? "none"})");
            return arc;
        }

        public StoryArc StartDroughtMigration(int centerTile, int radius)
        {
            var arc = new StoryArc
            {
                ArcId = "drought_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ArcType = "DroughtMigration",
                Title = "Regional drought",
                CreatedTick = Find.TickManager.TicksGame,
                CurrentStep = 0,
                TotalSteps = 4,
                AnchorTile = centerTile,
                PendingEventTags = new List<string>
                {
                    "GlobalDrought",
                    "FactionMigration",
                    "RegionalWar",
                    "Custom"
                }
            };

            // Facções com settlements próximos
            if (Find.WorldObjects?.Settlements != null)
            {
                foreach (var s in Find.WorldObjects.Settlements)
                {
                    if (s.Faction == null || s.Faction.IsPlayer) continue;
                    if (Find.WorldGrid.ApproxDistanceInTiles(centerTile, s.Tile) <= radius)
                        arc.InvolvedFactionIds.Add(s.Faction.GetUniqueLoadID());
                }
            }

            _arcs.Add(arc);
            Log.Message($"[XenoDirector.Arc] Drought migration arc at tile {centerTile}, factions={arc.InvolvedFactionIds.Count}");
            return arc;
        }

        public StoryArc StartPandemic(string diseaseLabel = "MysteryFever")
        {
            var arc = new StoryArc
            {
                ArcId = "pandemic_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                ArcType = "Pandemic",
                Title = "Mysterious outbreak: " + diseaseLabel,
                CreatedTick = Find.TickManager.TicksGame,
                CurrentStep = 0,
                TotalSteps = 6,
                DiseaseDefName = diseaseLabel,
                PendingEventTags = new List<string>
                {
                    "MysteryDiseaseOutbreak",
                    "IdeologyCureAttempt",
                    "IdeologyCureAttempt",
                    "IdeologyCureAttempt",
                    "Custom",
                    "Custom"
                }
            };

            foreach (var f in Find.FactionManager?.AllFactionsVisible ?? Enumerable.Empty<Faction>())
            {
                if (f.IsPlayer || f.defeated) continue;
                arc.InvolvedFactionIds.Add(f.GetUniqueLoadID());
            }

            _arcs.Add(arc);
            Log.Message($"[XenoDirector.Arc] Pandemic arc started: {diseaseLabel}");
            return arc;
        }

        private void PulseArc(StoryArc arc, XenoDirectorGameComponent gc)
        {
            if (arc.PendingEventTags == null || arc.PendingEventTags.Count == 0)
            {
                arc.Completed = true;
                return;
            }

            // Espaça etapas (não dispara tudo de uma vez)
            int minGap = 60000 * 2; // ~2 dias
            if (arc.Flags.TryGetValue("lastStepTick", out var lastStr) &&
                int.TryParse(lastStr, out var last) &&
                Find.TickManager.TicksGame - last < minGap)
                return;

            string tag = arc.PendingEventTags[0];
            arc.PendingEventTags.RemoveAt(0);
            arc.Flags["lastStepTick"] = Find.TickManager.TicksGame.ToString();

            var coordinator = NarrativeWorldAccess.Instance;
            bool ok = false;

            switch (tag)
            {
                case "XenotypeHunters":
                    ok = coordinator.TryFireXenotypeHunterRaid(arc);
                    break;
                case "AssassinationAttempt":
                    ok = coordinator.TryFireAssassination(arc);
                    break;
                case "ReturnEscort":
                    ok = coordinator.TryOfferReturnEscortQuest(arc);
                    break;
                case "AmbushOnReturn":
                    ok = coordinator.TryScheduleAmbush(arc);
                    break;
                case "FactionRaidLinked":
                    ok = coordinator.TryLinkedFactionRaid(arc);
                    break;
                case "GlobalDrought":
                    ok = coordinator.TryApplyRegionalDrought(arc);
                    break;
                case "FactionMigration":
                    ok = coordinator.TryForceFactionMigration(arc);
                    break;
                case "RegionalWar":
                    ok = coordinator.TryStartRegionalHostility(arc);
                    break;
                case "MysteryDiseaseOutbreak":
                    ok = coordinator.TryOutbreakMysteryDisease(arc);
                    break;
                case "IdeologyCureAttempt":
                    ok = coordinator.TryIdeologyBasedCureAttempt(arc);
                    break;
                default:
                    ok = true;
                    break;
            }

            if (ok) arc.Advance(tag);
            else
            {
                // Re-enfileira no fim se falhou (facção sumiu, etc.)
                arc.PendingEventTags.Add(tag);
            }

            gc.OptDirector?.NotifyActivity(GameSubsystemId.Incidents);
        }
    }
}
