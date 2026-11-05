using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace XenoDirector
{
    public class XenoDirectorMod : Mod
    {
        public static XenoDirectorSettings Settings;
        public static Harmony HarmonyInstance;

        public const int DefaultPermanentMicroBrainCount = 2;
        public const int AbsoluteMaxMicroBrains = 12;

        public XenoDirectorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<XenoDirectorSettings>();
            HarmonyInstance = new Harmony("lucas.xenodirector.storyteller");
            HarmonyInstance.PatchAll();
            Log.Message("[XenoDirector] Loaded. Restrictions+Arcs+Opt work without being selected storyteller. Advanced perf is opt-in.");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory() => "XenoDirector Storyteller";
    }

    public class XenoDirectorSettings : ModSettings
    {
        // --- Core ---
        public int PermanentMicroBrainCount = XenoDirectorMod.DefaultPermanentMicroBrainCount;
        public bool EnablePlayerReactionTracking = true;
        public bool EnableCrossModCoordination = true;
        public bool AggressiveOptimization = true;
        public bool EnableOptimizationLayer = true;
        public float MenaceDecayMultiplier = 1f;
        public int HistoryRetentionDays = 60;

        // --- Always-on systems (independent of selected storyteller) ---
        public bool EnableRestrictionsAlways = true;
        public bool EnableNarrativeArcs = true;
        public bool EnableQuestMutation = true;
        public bool EnableGlobalStoryEvents = true;

        // --- Advanced performance (OPT-IN, off by default) ---
        public bool EnableAdvancedPerformance = false;
        public bool AdvDynamicEntityThrottling = false;
        public bool AdvWorldPawnAggressiveCull = false;
        public bool AdvPeriodicGcHint = false;
        public int AdvDistantPawnTickInterval = 4;
        public int AdvWorldPawnSoftCap = 250;

        private Vector2 _scroll;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref PermanentMicroBrainCount, "PermanentMicroBrainCount", XenoDirectorMod.DefaultPermanentMicroBrainCount);
            Scribe_Values.Look(ref EnablePlayerReactionTracking, "EnablePlayerReactionTracking", true);
            Scribe_Values.Look(ref EnableCrossModCoordination, "EnableCrossModCoordination", true);
            Scribe_Values.Look(ref AggressiveOptimization, "AggressiveOptimization", true);
            Scribe_Values.Look(ref EnableOptimizationLayer, "EnableOptimizationLayer", true);
            Scribe_Values.Look(ref MenaceDecayMultiplier, "MenaceDecayMultiplier", 1f);
            Scribe_Values.Look(ref HistoryRetentionDays, "HistoryRetentionDays", 60);

            Scribe_Values.Look(ref EnableRestrictionsAlways, "EnableRestrictionsAlways", true);
            Scribe_Values.Look(ref EnableNarrativeArcs, "EnableNarrativeArcs", true);
            Scribe_Values.Look(ref EnableQuestMutation, "EnableQuestMutation", true);
            Scribe_Values.Look(ref EnableGlobalStoryEvents, "EnableGlobalStoryEvents", true);

            Scribe_Values.Look(ref EnableAdvancedPerformance, "EnableAdvancedPerformance", false);
            Scribe_Values.Look(ref AdvDynamicEntityThrottling, "AdvDynamicEntityThrottling", false);
            Scribe_Values.Look(ref AdvWorldPawnAggressiveCull, "AdvWorldPawnAggressiveCull", false);
            Scribe_Values.Look(ref AdvPeriodicGcHint, "AdvPeriodicGcHint", false);
            Scribe_Values.Look(ref AdvDistantPawnTickInterval, "AdvDistantPawnTickInterval", 4);
            Scribe_Values.Look(ref AdvWorldPawnSoftCap, "AdvWorldPawnSoftCap", 250);

            PermanentMicroBrainCount = Math.Clamp(PermanentMicroBrainCount, 1, XenoDirectorMod.AbsoluteMaxMicroBrains);
            AdvDistantPawnTickInterval = Math.Clamp(AdvDistantPawnTickInterval, 2, 30);
            AdvWorldPawnSoftCap = Math.Clamp(AdvWorldPawnSoftCap, 50, 2000);
        }

        public void DoWindowContents(Rect inRect)
        {
            var view = new Rect(0, 0, inRect.width - 24f, 1200f);
            Widgets.BeginScrollView(inRect, ref _scroll, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("=== Otimização base (qualquer storyteller) ===");
            listing.CheckboxLabeled("Enable optimization layer", ref EnableOptimizationLayer,
                "Cérebros de otimização + scheduler. Funciona com Cassandra/Phoebe/Randy.");
            listing.CheckboxLabeled("Aggressive optimization (saves longos / muitos mods)", ref AggressiveOptimization);

            listing.GapLine();
            listing.Label("=== Performance AVANÇADA (opt-in — use com cuidado) ===");
            listing.CheckboxLabeled("Enable advanced performance methods", ref EnableAdvancedPerformance,
                "Ativa técnicas mais agressivas (throttle de pawns distantes, etc.). OFF por padrão.");
            if (EnableAdvancedPerformance)
            {
                listing.CheckboxLabeled("  Dynamic entity throttling (skip ticks de animais/NPCs distantes)", ref AdvDynamicEntityThrottling,
                    "Similar a entity-distance culling ativo. Nunca afeta colonos do jogador nem hostis próximos.");
                listing.CheckboxLabeled("  World pawn pressure monitoring (soft cap)", ref AdvWorldPawnAggressiveCull);
                listing.CheckboxLabeled("  Periodic soft GC hint (~3 dias de jogo)", ref AdvPeriodicGcHint);
                listing.Label("  Distant pawn tick interval: " + AdvDistantPawnTickInterval + " (maior = mais skip)");
                AdvDistantPawnTickInterval = (int)listing.Slider(AdvDistantPawnTickInterval, 2, 30);
                listing.Label("  World pawn soft cap: " + AdvWorldPawnSoftCap);
                AdvWorldPawnSoftCap = (int)listing.Slider(AdvWorldPawnSoftCap, 50, 2000);
            }

            listing.GapLine();
            listing.Label("=== Restrições & narrativa (independente do storyteller) ===");
            listing.CheckboxLabeled("Restrictions always active (even if XenoDirector is NOT selected)", ref EnableRestrictionsAlways,
                "Single / Zombie Apocalypse / Nature's Wrath aplicam via Harmony com qualquer storyteller.");
            listing.CheckboxLabeled("Narrative arcs (rescue chains, linked events)", ref EnableNarrativeArcs);
            listing.CheckboxLabeled("Quest mutation (vary vanilla quests)", ref EnableQuestMutation);
            listing.CheckboxLabeled("Global story events (drought migration, pandemics)", ref EnableGlobalStoryEvents);

            listing.GapLine();
            listing.Label("=== Storyteller XenoDirector (só quando selecionado) ===");
            listing.Label("Permanent micro-brains: " + PermanentMicroBrainCount);
            PermanentMicroBrainCount = (int)listing.Slider(PermanentMicroBrainCount, 1, XenoDirectorMod.AbsoluteMaxMicroBrains);
            listing.CheckboxLabeled("Track player reactions", ref EnablePlayerReactionTracking);
            listing.CheckboxLabeled("Cross-mod event coordination", ref EnableCrossModCoordination);
            listing.Label("Menace decay multiplier: " + MenaceDecayMultiplier.ToString("F2"));
            MenaceDecayMultiplier = listing.Slider(MenaceDecayMultiplier, 0.25f, 3f);
            listing.Label("History retention (days): " + HistoryRetentionDays);
            HistoryRetentionDays = (int)listing.Slider(HistoryRetentionDays, 14, 180);

            listing.GapLine();
            listing.Label("=== Restrições ativas (save atual) ===");
            var rm = XenoDirectorGameComponent.GetRestrictions();
            if (rm == null)
                listing.Label("(Abra um save para gerenciar restrições)");
            else
            {
                rm.EnsureRegistry();
                foreach (var r in rm.AllRegistered())
                {
                    bool active = rm.IsActive(r.Id);
                    bool nv = active;
                    listing.CheckboxLabeled($"{r.Label} — {r.Description}", ref nv);
                    if (nv != active)
                    {
                        if (nv) rm.Activate(r.Id);
                        else rm.Deactivate(r.Id);
                    }
                }
            }

            listing.GapLine();
            listing.Label("=== Debug / diagnóstico ===");
            listing.Label(XenoDirectorGameComponent.IsXenoStorytellerActive()
                ? "Storyteller: XenoDirector ATIVO"
                : "Storyteller: outro (opt + restrições + arcos ainda podem estar ativos)");
            var arcs = XenoDirectorGameComponent.GetArcs();
            if (arcs != null)
                listing.Label($"Arcos narrativos ativos: {arcs.ActiveArcs.Count}");
            var opt = XenoDirectorGameComponent.GetOpt();
            if (opt != null)
            {
                int h = 0, b = 0, s = 0, f = 0;
                foreach (var sys in opt.Scheduler.AllSystems)
                {
                    switch (sys.Tier)
                    {
                        case WorkTier.Hibernating: h++; break;
                        case WorkTier.Background: b++; break;
                        case WorkTier.Secondary: s++; break;
                        case WorkTier.Foreground: f++; break;
                    }
                }
                listing.Label($"Opt tiers F/S/B/H: {f}/{s}/{b}/{h} | deferred={opt.Scheduler.DeferredCount}");
            }

            // Botões de teste de arco global (dev)
            if (Prefs.DevMode && Current.Game != null)
            {
                listing.GapLine();
                listing.Label("=== Dev: disparar arcos globais ===");
                if (listing.ButtonText("Start drought migration (near player)"))
                {
                    var map = Find.AnyPlayerHomeMap;
                    if (map != null)
                        XenoDirectorGameComponent.GetArcs()?.StartDroughtMigration(map.Tile, 12);
                }
                if (listing.ButtonText("Start mystery pandemic"))
                    XenoDirectorGameComponent.GetArcs()?.StartPandemic("MysteryFever");
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}
