using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Ponte de acesso profundo aos sistemas internos do RimWorld para
    /// sincronizar eventos, quests, facções, world tiles, xenotypes e ideologia.
    ///
    /// Todos os métodos são defensivos (null-checks, try/catch) para não
    /// quebrar com DLC ausente ou mods que alteram defs.
    /// </summary>
    public class NarrativeWorldAccess
    {
        private static NarrativeWorldAccess _instance;
        public static NarrativeWorldAccess Instance => _instance ??= new NarrativeWorldAccess();

        // ---------- Rescue chain ----------

        public bool TryFireXenotypeHunterRaid(StoryArc arc)
        {
            if (string.IsNullOrEmpty(arc.AnchorXenotypeDefName)) return false;
            if (!ModsConfig.BiotechActive) return false;

            var map = Find.AnyPlayerHomeMap;
            if (map == null) return false;

            // Facção hostil qualquer; preferência por quem odeia o xenotipo (esqueleto)
            var faction = Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
            if (faction == null) return false;

            var parms = new IncidentParms
            {
                target = map,
                faction = faction,
                points = StorytellerUtility.DefaultThreatPointsNow(map) * 0.7f,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                customLetterLabel = "Xenotype hunters",
                customLetterText = $"Hunters seek those with the {arc.AnchorXenotypeDefName} xenotype after your recent rescue."
            };

            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("RaidEnemy") ?? IncidentDefOf.RaidEnemy;
            bool fired = def != null && def.Worker.TryExecute(parms);
            if (fired)
                Letters.Archive.Add(LetterMaker.MakeLetter("Xenotype hunters", parms.customLetterText, LetterDefOf.ThreatBig, map));
            return fired;
        }

        public bool TryFireAssassination(StoryArc arc)
        {
            var map = Find.AnyPlayerHomeMap;
            if (map == null) return false;

            Pawn target = ResolveAnchorPawn(arc);
            if (target == null || target.Map != map) return false;

            // Usa raid pequena / assassinagem se disponível; fallback letter + threat
            var faction = Find.FactionManager.RandomEnemyFaction();
            if (faction == null) return false;

            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall, map);
            parms.faction = faction;
            parms.points = Math.Max(parms.points * 0.35f, 100f);
            parms.customLetterLabel = "Assassination attempt";
            parms.customLetterText = $"{target.LabelShort} is being targeted after the rescue.";

            var def = IncidentDefOf.RaidEnemy;
            return def.Worker.TryExecute(parms);
        }

        public bool TryOfferReturnEscortQuest(StoryArc arc)
        {
            // Esqueleto: letter + marca flag; em produção geraria QuestScript custom
            var target = ResolveAnchorPawn(arc);
            string name = target?.LabelShort ?? "the rescued";
            Find.LetterStack.ReceiveLetter(
                "Return escort",
                $"An opportunity arises to return {name} under escort. Travel may not be safe.",
                LetterDefOf.NeutralEvent);
            arc.Flags["returnEscortOffered"] = "1";
            return true;
        }

        public bool TryScheduleAmbush(StoryArc arc)
        {
            if (!arc.Flags.ContainsKey("returnEscortOffered")) return false;
            var map = Find.AnyPlayerHomeMap;
            if (map == null) return false;

            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.points *= 0.6f;
            parms.customLetterLabel = "Ambush";
            parms.customLetterText = "The escort route was compromised.";
            return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        }

        public bool TryLinkedFactionRaid(StoryArc arc)
        {
            var map = Find.AnyPlayerHomeMap;
            if (map == null) return false;
            Faction faction = ResolveFaction(arc.AnchorFactionId) ?? Find.FactionManager.RandomEnemyFaction();
            if (faction == null) return false;

            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = faction;
            return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        }

        // ---------- Global: drought / migration / war ----------

        public bool TryApplyRegionalDrought(StoryArc arc)
        {
            if (arc.AnchorTile < 0) return false;

            // GameCondition no mapa do jogador se estiver na região; senão letter mundial
            var map = Find.AnyPlayerHomeMap;
            if (map != null)
            {
                var drought = DefDatabase<GameConditionDef>.GetNamedSilentFail("Drought")
                    ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("HeatWave")
                    ?? GameConditionDefOf.HeatWave;
                if (drought != null)
                {
                    map.GameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(drought, 60000 * 8));
                }
            }

            Find.LetterStack.ReceiveLetter(
                "Regional drought",
                "A severe drought grips a region of the world. Nearby factions may be forced to move.",
                LetterDefOf.NegativeEvent);
            return true;
        }

        public bool TryForceFactionMigration(StoryArc arc)
        {
            // Esqueleto: marca relações e letter. Migração real de settlement exige
            // WorldObject manipulação cuidadosa (destroy + create) — feito de forma segura.
            int moved = 0;
            foreach (var fid in arc.InvolvedFactionIds.ToList())
            {
                var faction = ResolveFactionByLoadId(fid);
                if (faction == null) continue;

                var settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == faction);
                if (settlement == null) continue;

                // Escolhe tile vizinho válido
                int newTile = settlement.Tile;
                Find.WorldGrid.GetTileNeighbors(settlement.Tile, _neighborBuffer);
                foreach (var t in _neighborBuffer)
                {
                    if (Find.World.Impassable(t)) continue;
                    if (Find.WorldObjects.AnyWorldObjectAt(t)) continue;
                    newTile = t;
                    break;
                }

                if (newTile != settlement.Tile)
                {
                    // Não destruímos settlements vanilla à toa em esqueleto —
                    // registramos intenção e letter (implementação completa: relocate API)
                    arc.Flags["migrated_" + fid] = newTile.ToString();
                    moved++;
                }
            }

            if (moved > 0)
            {
                Find.LetterStack.ReceiveLetter(
                    "Faction migration",
                    $"{moved} faction(s) are abandoning drought-stricken lands.",
                    LetterDefOf.NeutralEvent);
            }
            return moved > 0 || arc.InvolvedFactionIds.Count == 0;
        }

        private readonly List<int> _neighborBuffer = new List<int>();

        public bool TryStartRegionalHostility(StoryArc arc)
        {
            var factions = arc.InvolvedFactionIds
                .Select(ResolveFactionByLoadId)
                .Where(f => f != null && !f.IsPlayer)
                .ToList();

            if (factions.Count < 2) return false;

            // Aumenta hostilidade entre pares
            for (int i = 0; i < factions.Count; i++)
            {
                for (int j = i + 1; j < factions.Count; j++)
                {
                    try
                    {
                        factions[i].TryAffectGoodwillWith(factions[j], -40, canSendMessage: false, canSendHostilityLetter: true);
                    }
                    catch { /* goodwill caps */ }
                }
            }

            Find.LetterStack.ReceiveLetter(
                "Regional tension",
                "Displaced factions clash over new territory.",
                LetterDefOf.ThreatSmall);
            return true;
        }

        // ---------- Pandemic + ideology ----------

        public bool TryOutbreakMysteryDisease(StoryArc arc)
        {
            var map = Find.AnyPlayerHomeMap;
            if (map == null) return false;

            // Aplica hediff genérico se existir; senão letter + flu
            HediffDef hediff = DefDatabase<HediffDef>.GetNamedSilentFail(arc.DiseaseDefName)
                ?? HediffDefOf.Flu;

            int infected = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p.health?.hediffSet == null) continue;
                if (Rand.Chance(0.35f))
                {
                    p.health.AddHediff(hediff);
                    infected++;
                }
            }

            Find.LetterStack.ReceiveLetter(
                "Mystery outbreak",
                $"A strange illness spreads. {infected} colonist(s) show symptoms. Factions across the world react according to their beliefs.",
                LetterDefOf.ThreatBig);
            return true;
        }

        public bool TryIdeologyBasedCureAttempt(StoryArc arc)
        {
            if (!ModsConfig.IdeologyActive)
            {
                Find.LetterStack.ReceiveLetter(
                    "Cure research",
                    "Without Ideology, factions pursue conventional medicine against the outbreak.",
                    LetterDefOf.NeutralEvent);
                return true;
            }

            // Escolhe uma facção ainda não processada
            Faction pick = null;
            foreach (var fid in arc.InvolvedFactionIds)
            {
                if (arc.Flags.ContainsKey("ideoCure_" + fid)) continue;
                pick = ResolveFactionByLoadId(fid);
                if (pick != null) break;
            }
            if (pick == null) return true;

            arc.Flags["ideoCure_" + pick.GetUniqueLoadID()] = "1";
            string approach = DescribeIdeoCureApproach(pick);

            Find.LetterStack.ReceiveLetter(
                $"{pick.Name}: response to the plague",
                approach,
                LetterDefOf.NeutralEvent);
            return true;
        }

        private static string DescribeIdeoCureApproach(Faction f)
        {
            var ideo = f.ideos?.PrimaryIdeo;
            if (ideo == null)
                return $"{f.Name} seeks conventional medical knowledge.";

            // Heurísticas por memes / precepts
            string text = ideo.memberName + " of " + f.Name + " ";
            bool genetic = ideo.memes?.Any(m =>
                (m.defName?.IndexOf("Gene", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (m.label?.IndexOf("gene", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) ?? false;
            bool pain = ideo.memes?.Any(m =>
                (m.defName?.IndexOf("Pain", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) ?? false;
            bool nature = ideo.memes?.Any(m =>
                (m.defName?.IndexOf("Nature", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (m.defName?.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (m.defName?.IndexOf("Animist", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) ?? false;
            bool darkness = ideo.memes?.Any(m =>
                (m.defName?.IndexOf("Darkness", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                (m.defName?.IndexOf("PainIsVirtue", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) ?? false;

            if (genetic)
                text += "pursue genetic resistance — seeking pawns and gene-modding against the plague.";
            else if (nature)
                text += "turn to sacred trees (Anima, Gauranlen, Polux and similar) and druidic rites.";
            else if (darkness || pain)
                text += "lean into void-touched or extreme rituals, risking anomaly-like cures.";
            else
                text += $"follow the doctrines of {ideo.name} to contain the disease.";

            return text;
        }

        // ---------- Helpers ----------

        public Pawn ResolveAnchorPawn(StoryArc arc)
        {
            if (string.IsNullOrEmpty(arc.AnchorPawnLoadId)) return null;
            foreach (var map in Find.Maps)
            {
                foreach (var p in map.mapPawns.AllPawns)
                    if (p.GetUniqueLoadID() == arc.AnchorPawnLoadId) return p;
            }
            return Find.WorldPawns?.AllPawnsAlive?.FirstOrDefault(p => p.GetUniqueLoadID() == arc.AnchorPawnLoadId);
        }

        public Faction ResolveFaction(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            return Find.FactionManager?.AllFactions?.FirstOrDefault(f => f.def?.defName == defName);
        }

        public Faction ResolveFactionByLoadId(string loadId)
        {
            if (string.IsNullOrEmpty(loadId)) return null;
            return Find.FactionManager?.AllFactions?.FirstOrDefault(f => f.GetUniqueLoadID() == loadId);
        }

        /// <summary>
        /// Mutação leve de quest vanilla: altera recompensa / prazo / nome para variar.
        /// </summary>
        public void TryMutateQuest(Quest quest)
        {
            if (quest == null || !XenoDirectorMod.Settings.EnableQuestMutation) return;
            if (quest.State != QuestState.Ongoing) return;

            // Marca para não mutar duas vezes
            string key = "xd_mutated_" + quest.id;
            // Quest não tem dictionary livre; usamos name prefix
            if (quest.name != null && quest.name.StartsWith("[XD] ")) return;

            quest.name = "[XD] " + (quest.name ?? "Quest");
            // Estende ou reduz ticks restantes se existir
            try
            {
                if (quest.TicksUntilExpiry > 0)
                    quest.TicksUntilExpiry = (int)(quest.TicksUntilExpiry * Rand.Range(0.75f, 1.4f));
            }
            catch { }

            PerformanceGuards.ReportActivity(GameSubsystemId.QuestManager);
        }
    }
}
