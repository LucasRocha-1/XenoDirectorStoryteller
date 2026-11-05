using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Gerenciador central de restrições.
    /// Persistido no GameComponent. Consultado por Director, MicroBrains, Catálogo e patches de raid.
    /// </summary>
    public class RestrictionManager : IExposable
    {
        private List<StoryRestriction> _active = new List<StoryRestriction>();
        private Dictionary<string, StoryRestriction> _registry = new Dictionary<string, StoryRestriction>();

        // Cache de tags/defs bloqueados e forçados (reconstruído quando restrições mudam)
        private HashSet<string> _blockedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _forcedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _forcedDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _allowedRaidTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _allowedRaidPawnKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _blockRecruitment;
        private bool _blockQuestPawnRewards;
        private bool _forceNaturalDisasterSequence;
        private float _menaceBias;
        private bool _dirty = true;

        public IReadOnlyList<StoryRestriction> ActiveRestrictions => _active;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _active, "ActiveRestrictions", LookMode.Deep);
            if (_active == null) _active = new List<StoryRestriction>();
            _dirty = true;
        }

        /// <summary>
        /// Registra restrições conhecidas (built-in + futuras via Def/XML).
        /// </summary>
        public void EnsureRegistry()
        {
            if (_registry.Count > 0) return;

            Register(StoryRestriction.CreateSingle());
            Register(StoryRestriction.CreateZombieApocalypse());
            Register(StoryRestriction.CreateNaturesWrath());

            // Espaço para restrições vindas de Defs no futuro
        }

        public void Register(StoryRestriction r)
        {
            if (r == null || string.IsNullOrEmpty(r.Id)) return;
            _registry[r.Id] = r;
        }

        public bool TryGetRegistered(string id, out StoryRestriction r) => _registry.TryGetValue(id, out r);

        public IEnumerable<StoryRestriction> AllRegistered()
        {
            EnsureRegistry();
            return _registry.Values;
        }

        public void Activate(string id)
        {
            EnsureRegistry();
            if (!_registry.TryGetValue(id, out var template)) return;

            // Clona para não mutar o template
            var instance = Clone(template);
            instance.IsActive = true;
            instance.ActivatedTick = Find.TickManager?.TicksGame ?? 0;

            // Remove versão anterior se existir
            _active.RemoveAll(x => x.Id == id);
            _active.Add(instance);
            _active = _active.OrderByDescending(x => x.Priority).ToList();
            _dirty = true;

            Log.Message($"[XenoDirector] Restriction activated: {id}");
            ApplyImmediateEffects(instance);
        }

        public void Deactivate(string id)
        {
            int removed = _active.RemoveAll(x => x.Id == id);
            if (removed > 0)
            {
                _dirty = true;
                Log.Message($"[XenoDirector] Restriction deactivated: {id}");
            }
        }

        public bool IsActive(string id) => _active.Any(x => x.Id == id && x.IsActive);

        public void Toggle(string id)
        {
            if (IsActive(id)) Deactivate(id);
            else Activate(id);
        }

        private void RebuildCache()
        {
            _blockedTags.Clear();
            _forcedTags.Clear();
            _forcedDefNames.Clear();
            _allowedRaidTags.Clear();
            _allowedRaidPawnKinds.Clear();
            _blockRecruitment = false;
            _blockQuestPawnRewards = false;
            _forceNaturalDisasterSequence = false;
            _menaceBias = 0f;

            foreach (var r in _active.Where(x => x.IsActive))
            {
                foreach (var t in r.BlockedIncidentTags) _blockedTags.Add(t);
                foreach (var t in r.ForcedIncidentTags) _forcedTags.Add(t);
                foreach (var d in r.ForcedIncidentDefNames) _forcedDefNames.Add(d);
                foreach (var t in r.AllowedRaidTags) _allowedRaidTags.Add(t);
                foreach (var p in r.AllowedRaidPawnKinds) _allowedRaidPawnKinds.Add(p);
                if (r.BlockRecruitment) _blockRecruitment = true;
                if (r.BlockQuestRewardsThatAddPawns) _blockQuestPawnRewards = true;
                if (r.ForceNaturalDisasterSequence) _forceNaturalDisasterSequence = true;
                _menaceBias += r.MenaceBias;
            }

            _dirty = false;
        }

        private void EnsureCache()
        {
            if (_dirty) RebuildCache();
        }

        // ========== API usada pelos cérebros e pelo catálogo ==========

        public bool IsIncidentBlocked(IncidentDef def)
        {
            if (def == null) return true;
            EnsureCache();

            // Bloqueio por DefName
            if (_blockedTags.Contains(def.defName)) return true;

            // Bloqueio por tags do def (RimWorld usa def.tags e category)
            if (def.tags != null)
            {
                foreach (var tag in def.tags)
                    if (_blockedTags.Contains(tag)) return true;
            }

            if (def.category != null && _blockedTags.Contains(def.category.defName))
                return true;

            // Regras especiais de recrutamento
            if (_blockRecruitment && IsRecruitmentIncident(def))
                return true;

            return false;
        }

        public bool IsIncidentForced(IncidentDef def)
        {
            if (def == null) return false;
            EnsureCache();

            if (_forcedDefNames.Contains(def.defName)) return true;
            if (def.tags != null)
            {
                foreach (var tag in def.tags)
                    if (_forcedTags.Contains(tag)) return true;
            }
            if (def.category != null && _forcedTags.Contains(def.category.defName))
                return true;

            return false;
        }

        public bool HasForcedIncidents => (EnsureCache(), _forcedDefNames.Count > 0 || _forcedTags.Count > 0).Item2;

        public IEnumerable<string> GetForcedDefNames()
        {
            EnsureCache();
            return _forcedDefNames;
        }

        public IEnumerable<string> GetForcedTags()
        {
            EnsureCache();
            return _forcedTags;
        }

        /// <summary>
        /// Retorna true se raids devem ser filtradas para apenas Undead (ou outras tags permitidas).
        /// </summary>
        public bool RestrictsRaids
        {
            get
            {
                EnsureCache();
                return _allowedRaidTags.Count > 0 || _allowedRaidPawnKinds.Count > 0;
            }
        }

        public bool IsRaidPawnKindAllowed(PawnKindDef kind)
        {
            if (kind == null) return false;
            EnsureCache();
            if (_allowedRaidPawnKinds.Count == 0 && _allowedRaidTags.Count == 0)
                return true; // sem restrição de raid

            if (_allowedRaidPawnKinds.Contains(kind.defName))
                return true;

            // Heurística por tags / race / label (compatível com mods de undead)
            if (kind.race?.defName != null && _allowedRaidTags.Contains(kind.race.defName))
                return true;

            if (kind.defName != null)
            {
                foreach (var tag in _allowedRaidTags)
                    if (kind.defName.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
            }

            // label
            if (kind.label != null)
            {
                foreach (var tag in _allowedRaidTags)
                    if (kind.label.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
            }

            return false;
        }

        public bool BlocksRecruitment
        {
            get { EnsureCache(); return _blockRecruitment; }
        }

        public bool BlocksQuestPawnRewards
        {
            get { EnsureCache(); return _blockQuestPawnRewards; }
        }

        public bool ForcesNaturalDisasterSequence
        {
            get { EnsureCache(); return _forceNaturalDisasterSequence; }
        }

        public float MenaceBias
        {
            get { EnsureCache(); return _menaceBias; }
        }

        public bool IsNaturalDisaster(IncidentDef def)
        {
            if (def == null) return false;
            EnsureCache();

            if (def.tags != null)
            {
                foreach (var t in def.tags)
                    if (_forcedTags.Contains(t) || IsDisasterTag(t)) return true;
            }
            if (def.category != null && IsDisasterTag(def.category.defName)) return true;
            if (IsDisasterTag(def.defName)) return true;
            return false;
        }

        private static bool IsDisasterTag(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.ToLowerInvariant();
            return s.Contains("disaster") || s.Contains("weather") || s.Contains("earthquake")
                || s.Contains("volcano") || s.Contains("tornado") || s.Contains("flood")
                || s.Contains("wildfire") || s.Contains("meteor") || s.Contains("eclipse")
                || s.Contains("coldsnap") || s.Contains("heatwave") || s.Contains("toxic")
                || s.Contains("flashstorm") || s.Contains("storm") || s.Contains("blizzard");
        }

        private static bool IsRecruitmentIncident(IncidentDef def)
        {
            if (def == null) return false;
            string n = def.defName?.ToLowerInvariant() ?? "";
            if (n.Contains("wanderer") || n.Contains("refugee") || n.Contains("join")
                || n.Contains("recruit") || n.Contains("prisoner") || n.Contains("slave")
                || n.Contains("quest") && n.Contains("pawn"))
                return true;

            if (def.tags != null)
            {
                foreach (var t in def.tags)
                {
                    string tl = t.ToLowerInvariant();
                    if (tl.Contains("recruit") || tl.Contains("join") || tl.Contains("wanderer")
                        || tl.Contains("refugee") || tl.Contains("pawnreward"))
                        return true;
                }
            }
            return false;
        }

        private void ApplyImmediateEffects(StoryRestriction r)
        {
            // Efeito imediato de bias de menace
            var gc = Current.Game?.GetComponent<XenoDirectorGameComponent>();
            if (gc != null && Math.Abs(r.MenaceBias) > 0.01f)
            {
                if (r.MenaceBias > 0) gc.RaiseMenace(r.MenaceBias * 0.5f, "restriction " + r.Id);
                else gc.LowerMenace(-r.MenaceBias * 0.5f);
            }
        }

        private static StoryRestriction Clone(StoryRestriction src)
        {
            return new StoryRestriction
            {
                Id = src.Id,
                Label = src.Label,
                Description = src.Description,
                IsActive = src.IsActive,
                ActivatedTick = src.ActivatedTick,
                BlockedIncidentTags = new List<string>(src.BlockedIncidentTags),
                ForcedIncidentTags = new List<string>(src.ForcedIncidentTags),
                ForcedIncidentDefNames = new List<string>(src.ForcedIncidentDefNames),
                AllowedRaidPawnKinds = new List<string>(src.AllowedRaidPawnKinds),
                AllowedRaidTags = new List<string>(src.AllowedRaidTags),
                BlockRecruitment = src.BlockRecruitment,
                BlockQuestRewardsThatAddPawns = src.BlockQuestRewardsThatAddPawns,
                ForceNaturalDisasterSequence = src.ForceNaturalDisasterSequence,
                MenaceBias = src.MenaceBias,
                Priority = src.Priority
            };
        }
    }
}
