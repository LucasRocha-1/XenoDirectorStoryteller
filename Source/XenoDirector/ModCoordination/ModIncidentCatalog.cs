using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace XenoDirector
{
    /// <summary>
    /// Catálogo de IncidentDefs e QuestScripts vindos de TODOS os mods carregados.
    /// Permite ao Director + micro-cérebros CrossMod coordenar eventos de múltiplas fontes
    /// como se fossem parte de uma história única.
    ///
    /// Otimizações para listas enormes de mods:
    /// - Construção lazy + cache
    /// - Indexação por categoria / packageId
    /// - Evita reflexão pesada a cada tick
    /// </summary>
    public class ModIncidentCatalog
    {
        private static ModIncidentCatalog _instance;
        public static ModIncidentCatalog Instance => _instance ??= new ModIncidentCatalog();

        private Dictionary<string, List<IncidentDef>> _byPackageId = new Dictionary<string, List<IncidentDef>>();
        private Dictionary<IncidentCategoryDef, List<IncidentDef>> _byCategory = new Dictionary<IncidentCategoryDef, List<IncidentDef>>();
        private List<IncidentDef> _allCrossModCandidates = new List<IncidentDef>();
        private bool _built;
        private int _lastRebuildTick = -999999;

        public void EnsureBuilt(bool force = false)
        {
            // Rebuild no máximo a cada 2 dias de jogo ou quando forçado (load de save / mudança de mods)
            if (_built && !force && Find.TickManager.TicksGame - _lastRebuildTick < 120000)
                return;

            _byPackageId.Clear();
            _byCategory.Clear();
            _allCrossModCandidates.Clear();

            foreach (var def in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (def == null) continue;

                string packageId = def.modContentPack?.PackageId ?? "ludeon.rimworld";
                if (!_byPackageId.TryGetValue(packageId, out var list))
                {
                    list = new List<IncidentDef>();
                    _byPackageId[packageId] = list;
                }
                list.Add(def);

                if (def.category != null)
                {
                    if (!_byCategory.TryGetValue(def.category, out var catList))
                    {
                        catList = new List<IncidentDef>();
                        _byCategory[def.category] = catList;
                    }
                    catList.Add(def);
                }

                // Candidatos "interessantes" para coordenação narrativa (não-raids genéricos)
                if (def.category != IncidentCategoryDefOf.ThreatBig &&
                    def.category != IncidentCategoryDefOf.ThreatSmall)
                {
                    _allCrossModCandidates.Add(def);
                }
            }

            _built = true;
            _lastRebuildTick = Find.TickManager.TicksGame;
            Log.Message($"[XenoDirector] ModIncidentCatalog built: {_byPackageId.Count} packages, {_allCrossModCandidates.Count} cross-mod candidates");
        }

        public IReadOnlyList<IncidentDef> GetIncidentsFromMod(string packageId)
        {
            EnsureBuilt();
            return _byPackageId.TryGetValue(packageId, out var list) ? list : System.Array.Empty<IncidentDef>();
        }

        public IReadOnlyList<IncidentDef> GetByCategory(IncidentCategoryDef category)
        {
            EnsureBuilt();
            return _byCategory.TryGetValue(category, out var list) ? list : System.Array.Empty<IncidentDef>();
        }

        public IReadOnlyList<IncidentDef> GetCrossModCandidates()
        {
            EnsureBuilt();
            return _allCrossModCandidates;
        }

        /// <summary>
        /// Escolhe um incidente de um mod diferente do último usado, para sensação de história multi-mod.
        /// Respeita RestrictionManager se fornecido.
        /// </summary>
        public IncidentDef PickCoordinatedIncident(string lastPackageId, IncidentCategoryDef preferredCategory = null, RestrictionManager restrictions = null)
        {
            EnsureBuilt();

            IEnumerable<IncidentDef> source = preferredCategory != null
                ? GetByCategory(preferredCategory)
                : _allCrossModCandidates;

            var pool = source
                .Where(d => d.modContentPack?.PackageId != lastPackageId)
                .Where(d => restrictions == null || !restrictions.IsIncidentBlocked(d))
                .ToList();

            // Se há forced tags/defs, prefere-os
            if (restrictions != null && restrictions.HasForcedIncidents)
            {
                var forced = pool.Where(d => restrictions.IsIncidentForced(d)).ToList();
                if (forced.Count > 0) pool = forced;
            }

            if (pool.Count == 0) return null;
            return pool[Rand.Range(0, pool.Count)];
        }

        /// <summary>
        /// Lista todos os desastres naturais de todos os mods (para Nature's Wrath).
        /// </summary>
        public List<IncidentDef> GetNaturalDisasters(RestrictionManager restrictions = null)
        {
            EnsureBuilt();
            var list = new List<IncidentDef>();

            foreach (var def in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (def == null) continue;
                if (restrictions != null)
                {
                    if (restrictions.IsIncidentBlocked(def)) continue;
                    if (restrictions.IsNaturalDisaster(def)) list.Add(def);
                }
                else
                {
                    // Heurística fallback
                    string n = (def.defName ?? "") + " " + (def.category?.defName ?? "");
                    n = n.ToLowerInvariant();
                    if (n.Contains("disaster") || n.Contains("weather") || n.Contains("storm")
                        || n.Contains("flashstorm") || n.Contains("toxic") || n.Contains("eclipse")
                        || n.Contains("coldsnap") || n.Contains("heatwave") || n.Contains("meteor"))
                        list.Add(def);
                }
            }
            return list;
        }
    }
}
