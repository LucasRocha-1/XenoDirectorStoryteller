using System;
using HarmonyLib;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Entry point do mod. Inicializa Harmony e carrega configurações estáticas de cérebros.
    /// </summary>
    public class XenoDirectorMod : Mod
    {
        public static XenoDirectorSettings Settings;
        public static Harmony HarmonyInstance;

        // Número base permanente de micro-cérebros (pode ser aumentado via settings ou código).
        // Em runtime o sistema permite spawns temporários adicionais.
        public const int DefaultPermanentMicroBrainCount = 2;
        public const int AbsoluteMaxMicroBrains = 12; // hard ceiling para segurança de performance

        public XenoDirectorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<XenoDirectorSettings>();
            HarmonyInstance = new Harmony("lucas.xenodirector.storyteller");
            HarmonyInstance.PatchAll();
            Log.Message("[XenoDirector] Mod loaded. Permanent micro-brains base: " + Settings.PermanentMicroBrainCount);
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory() => "XenoDirector Storyteller";
    }

    /// <summary>
    /// Settings do jogador + knobs de performance, número de cérebros e atalhos de restrições.
    /// Restrições ativas são persistidas no GameComponent (por save), não aqui.
    /// </summary>
    public class XenoDirectorSettings : ModSettings
    {
        public int PermanentMicroBrainCount = XenoDirectorMod.DefaultPermanentMicroBrainCount;
        public bool EnablePlayerReactionTracking = true;
        public bool EnableCrossModCoordination = true;
        public bool AggressiveOptimization = true; // para saves longos / muitos mods
        public float MenaceDecayMultiplier = 1f;
        public int HistoryRetentionDays = 60; // bounded history

        public override void ExposeData()
        {
            Scribe_Values.Look(ref PermanentMicroBrainCount, "PermanentMicroBrainCount", XenoDirectorMod.DefaultPermanentMicroBrainCount);
            Scribe_Values.Look(ref EnablePlayerReactionTracking, "EnablePlayerReactionTracking", true);
            Scribe_Values.Look(ref EnableCrossModCoordination, "EnableCrossModCoordination", true);
            Scribe_Values.Look(ref AggressiveOptimization, "AggressiveOptimization", true);
            Scribe_Values.Look(ref MenaceDecayMultiplier, "MenaceDecayMultiplier", 1f);
            Scribe_Values.Look(ref HistoryRetentionDays, "HistoryRetentionDays", 60);

            PermanentMicroBrainCount = Math.Clamp(PermanentMicroBrainCount, 1, XenoDirectorMod.AbsoluteMaxMicroBrains);
        }

        public void DoWindowContents(UnityEngine.Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("=== Cérebros & Performance ===");
            listing.Label("Permanent micro-brains (code + settings): " + PermanentMicroBrainCount);
            PermanentMicroBrainCount = (int)listing.Slider(PermanentMicroBrainCount, 1, XenoDirectorMod.AbsoluteMaxMicroBrains);
            listing.CheckboxLabeled("Track player reactions to events", ref EnablePlayerReactionTracking);
            listing.CheckboxLabeled("Coordinate events/quests across multiple mods", ref EnableCrossModCoordination);
            listing.CheckboxLabeled("Aggressive optimization (long saves / heavy modlists)", ref AggressiveOptimization);
            listing.Label("Menace decay multiplier: " + MenaceDecayMultiplier.ToString("F2"));
            MenaceDecayMultiplier = listing.Slider(MenaceDecayMultiplier, 0.25f, 3f);
            listing.Label("History retention (days): " + HistoryRetentionDays);
            HistoryRetentionDays = (int)listing.Slider(HistoryRetentionDays, 14, 180);

            listing.GapLine();
            listing.Label("=== Restrições Narrativas (ativas no save atual) ===");
            listing.Label("Ative/desative restrições. O storyteller reage imediatamente.");

            var rm = XenoDirectorGameComponent.GetRestrictions();
            if (rm == null)
            {
                listing.Label("(Abra um save / inicie uma colônia para gerenciar restrições)");
            }
            else
            {
                rm.EnsureRegistry();
                foreach (var r in rm.AllRegistered())
                {
                    bool active = rm.IsActive(r.Id);
                    bool newVal = active;
                    listing.CheckboxLabeled($"{r.Label} — {r.Description}", ref newVal);
                    if (newVal != active)
                    {
                        if (newVal) rm.Activate(r.Id);
                        else rm.Deactivate(r.Id);
                    }
                }
            }

            listing.End();
        }
    }
}
