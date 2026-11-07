using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Slate = RimWorld.QuestGen.Slate;
using QuestGen = RimWorld.QuestGen.QuestGen;

namespace XenoDirector
{
    /// <summary>
    /// Generates one quest on demand from the currently loaded quest-script defs.
    /// No Quest objects or script candidate lists are retained between calls.
    /// </summary>
    public static class QuestFabricator
    {
        private static readonly string[] EscortKeywords =
        {
            "escort", "rescue", "return", "caravan", "transport", "shuttle"
        };

        public static Quest GenerateReturnEscort(Pawn rescuedPawn, Faction originatingFaction, Map map)
        {
            if (map == null) return null;

            var slate = new Slate();
            slate.Set("map", map);
            slate.Set("pawn", rescuedPawn);
            slate.Set("faction", originatingFaction);
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
            slate.Set("xenoDirectorPurpose", "ReturnEscort");

            var candidates = DefDatabase<QuestScriptDef>.AllDefsListForReading
                .Where(def => def != null && def.IsRootRandomSelected)
                .Select(def => new
                {
                    Def = def,
                    Score = EscortRelevance(def.defName)
                        + EscortRelevance(def.label)
                        + EscortRelevance(def.modContentPack?.PackageId)
                })
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Def.defName, StringComparer.Ordinal)
                .Select(candidate => candidate.Def)
                .ToList();

            if (candidates.Count == 0)
            {
                Log.Warning("[XenoDirector.Quest] No random-selectable quest scripts are loaded.");
                return null;
            }

            int bestScore = candidates.Max(GetEscortScore);
            if (bestScore == 0)
            {
                Log.Warning("[XenoDirector.Quest] No loaded quest script is identified as escort, rescue, return, caravan, transport, or shuttle content.");
                return null;
            }
            var preferred = candidates
                .Where(def => GetEscortScore(def) == bestScore)
                .ToList();
            var remaining = candidates.Except(preferred).ToList();
            Shuffle(preferred);
            Shuffle(remaining);
            preferred.AddRange(remaining);

            foreach (var script in preferred)
            {
                try
                {
                    if (!script.CanRun(slate, map)) continue;
                    var quest = QuestGen.Generate(script, slate);
                    if (quest == null) continue;

                    ApplyReturnEscortContext(quest, script);
                    return quest;
                }
                catch (Exception exception)
                {
                    Log.Warning($"[XenoDirector.Quest] Failed to generate from {script.defName}: {exception}");
                }
            }

            return null;
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Rand.Range(0, i + 1);
                T item = items[i];
                items[i] = items[j];
                items[j] = item;
            }
        }

        private static int EscortRelevance(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int score = 0;
            foreach (var keyword in EscortKeywords)
                if (value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 10;
            return score;
        }

        private static int GetEscortScore(QuestScriptDef def)
        {
            return EscortRelevance(def.defName)
                + EscortRelevance(def.label)
                + EscortRelevance(def.modContentPack?.PackageId);
        }

        private static void ApplyReturnEscortContext(Quest quest, QuestScriptDef source)
        {
            string originalName = string.IsNullOrWhiteSpace(quest.name) ? source.label : quest.name;
            quest.name = "[XD] Return escort: " + originalName;

        }
    }
}
