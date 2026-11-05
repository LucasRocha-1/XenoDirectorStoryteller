using System.Collections.Generic;
using Verse;
using RimWorld;

namespace XenoDirector
{
    /// <summary>
    /// Arco narrativo multi-etapa. Sobrevive no GameComponent e avança
    /// independentemente do storyteller selecionado (quando EnableNarrativeArcs=true),
    /// mas ganha poder total com XenoDirector como storyteller.
    /// </summary>
    public class StoryArc : IExposable
    {
        public string ArcId;
        public string ArcType;          // "RescueChain", "Pandemic", "DroughtMigration", "Custom"
        public string Title;
        public int CreatedTick;
        public int CurrentStep;
        public int TotalSteps;
        public bool Completed;
        public bool Failed;

        // Contexto serializável
        public string AnchorPawnLoadId;     // pawn resgatado / alvo
        public string AnchorFactionId;
        public string AnchorXenotypeDefName;
        public int AnchorTile = -1;         // tile do mundo para eventos globais
        public string DiseaseDefName;
        public List<string> InvolvedFactionIds = new List<string>();
        public List<string> PendingEventTags = new List<string>();
        public Dictionary<string, string> Flags = new Dictionary<string, string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref ArcId, "ArcId");
            Scribe_Values.Look(ref ArcType, "ArcType");
            Scribe_Values.Look(ref Title, "Title");
            Scribe_Values.Look(ref CreatedTick, "CreatedTick");
            Scribe_Values.Look(ref CurrentStep, "CurrentStep");
            Scribe_Values.Look(ref TotalSteps, "TotalSteps");
            Scribe_Values.Look(ref Completed, "Completed");
            Scribe_Values.Look(ref Failed, "Failed");
            Scribe_Values.Look(ref AnchorPawnLoadId, "AnchorPawnLoadId");
            Scribe_Values.Look(ref AnchorFactionId, "AnchorFactionId");
            Scribe_Values.Look(ref AnchorXenotypeDefName, "AnchorXenotypeDefName");
            Scribe_Values.Look(ref AnchorTile, "AnchorTile");
            Scribe_Values.Look(ref DiseaseDefName, "DiseaseDefName");
            Scribe_Collections.Look(ref InvolvedFactionIds, "InvolvedFactionIds", LookMode.Value);
            Scribe_Collections.Look(ref PendingEventTags, "PendingEventTags", LookMode.Value);
            Scribe_Collections.Look(ref Flags, "Flags", LookMode.Value, LookMode.Value);

            if (InvolvedFactionIds == null) InvolvedFactionIds = new List<string>();
            if (PendingEventTags == null) PendingEventTags = new List<string>();
            if (Flags == null) Flags = new Dictionary<string, string>();
        }

        public void Advance(string reason = null)
        {
            CurrentStep++;
            if (CurrentStep >= TotalSteps)
                Completed = true;
        }
    }

    public enum ArcEventKind
    {
        XenotypeHunters,
        AssassinationAttempt,
        ReturnEscort,
        AmbushOnReturn,
        FactionRaidLinked,
        QuestMutation,
        GlobalDrought,
        FactionMigration,
        RegionalWar,
        MysteryDiseaseOutbreak,
        IdeologyCureAttempt,
        Custom
    }
}
