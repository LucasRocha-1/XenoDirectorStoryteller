using System;
using System.Collections.Generic;
using Verse;

namespace XenoDirector
{
    /// <summary>
    /// Restrição narrativa ativada pelo jogador.
    /// O Director e os micro-cérebros consultam o RestrictionManager antes de propor,
    /// filtrar ou transformar eventos/raids/quests.
    /// </summary>
    public class StoryRestriction : IExposable
    {
        public string Id;                    // "Single", "ZombieApocalypse", "NaturesWrath", etc.
        public string Label;
        public string Description;
        public bool IsActive;
        public int ActivatedTick;

        // Regras declarativas (serializáveis / configuráveis)
        public List<string> BlockedIncidentTags = new List<string>();       // tags que nunca podem disparar
        public List<string> ForcedIncidentTags = new List<string>();        // tags que devem ser priorizadas
        public List<string> ForcedIncidentDefNames = new List<string>();    // DefNames específicos forçados
        public List<string> AllowedRaidPawnKinds = new List<string>();      // se não vazio → só estes (ou tags)
        public List<string> AllowedRaidTags = new List<string>();           // "Undead", "Mechanoid", etc.
        public bool BlockRecruitment;                                       // impede ganhar novos pawns
        public bool BlockQuestRewardsThatAddPawns;
        public bool ForceNaturalDisasterSequence;
        public float MenaceBias;                                            // ajuste permanente na gauge (-1..+1)
        public int Priority;                                                // ordem de aplicação (maior = mais forte)

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "Id");
            Scribe_Values.Look(ref Label, "Label");
            Scribe_Values.Look(ref Description, "Description");
            Scribe_Values.Look(ref IsActive, "IsActive");
            Scribe_Values.Look(ref ActivatedTick, "ActivatedTick");
            Scribe_Collections.Look(ref BlockedIncidentTags, "BlockedIncidentTags", LookMode.Value);
            Scribe_Collections.Look(ref ForcedIncidentTags, "ForcedIncidentTags", LookMode.Value);
            Scribe_Collections.Look(ref ForcedIncidentDefNames, "ForcedIncidentDefNames", LookMode.Value);
            Scribe_Collections.Look(ref AllowedRaidPawnKinds, "AllowedRaidPawnKinds", LookMode.Value);
            Scribe_Collections.Look(ref AllowedRaidTags, "AllowedRaidTags", LookMode.Value);
            Scribe_Values.Look(ref BlockRecruitment, "BlockRecruitment");
            Scribe_Values.Look(ref BlockQuestRewardsThatAddPawns, "BlockQuestRewardsThatAddPawns");
            Scribe_Values.Look(ref ForceNaturalDisasterSequence, "ForceNaturalDisasterSequence");
            Scribe_Values.Look(ref MenaceBias, "MenaceBias");
            Scribe_Values.Look(ref Priority, "Priority");

            if (BlockedIncidentTags == null) BlockedIncidentTags = new List<string>();
            if (ForcedIncidentTags == null) ForcedIncidentTags = new List<string>();
            if (ForcedIncidentDefNames == null) ForcedIncidentDefNames = new List<string>();
            if (AllowedRaidPawnKinds == null) AllowedRaidPawnKinds = new List<string>();
            if (AllowedRaidTags == null) AllowedRaidTags = new List<string>();
        }

        public static StoryRestriction CreateSingle()
        {
            return new StoryRestriction
            {
                Id = "Single",
                Label = "Single",
                Description = "Você está sozinho. Nenhuma forma de ganhar outros personagens é permitida.",
                BlockRecruitment = true,
                BlockQuestRewardsThatAddPawns = true,
                BlockedIncidentTags = new List<string> { "Recruit", "Refugee", "Wanderer", "Join", "PrisonerJoin", "Slave", "QuestPawnReward" },
                Priority = 100
            };
        }

        public static StoryRestriction CreateZombieApocalypse()
        {
            return new StoryRestriction
            {
                Id = "ZombieApocalypse",
                Label = "Zombie Apocalypse",
                Description = "O mundo está morrendo. Raids futuras são apenas Undead. Eventos de Anomaly (Death Pall etc.) são forçados.",
                ForcedIncidentDefNames = new List<string> { "DeathPall", "UnnaturalDarkness", "BloodRain", "FingerspikeInfestation", "ShamblerSwarm" },
                ForcedIncidentTags = new List<string> { "Undead", "Shambler", "Anomaly", "Horror" },
                AllowedRaidTags = new List<string> { "Undead", "Shambler", "Zombie", "Ghoul", "Skeleton" },
                MenaceBias = 0.25f,
                Priority = 80
            };
        }

        public static StoryRestriction CreateNaturesWrath()
        {
            return new StoryRestriction
            {
                Id = "NaturesWrath",
                Label = "Nature's Wrath",
                Description = "A natureza se volta contra a colônia. Desastres naturais de todos os mods ativos disparam em sequência.",
                ForcedIncidentTags = new List<string> { "NaturalDisaster", "Weather", "Earthquake", "Volcano", "Tornado", "Flood", "Wildfire", "Meteor", "Eclipse", "ColdSnap", "HeatWave", "ToxicFallout", "Flashstorm" },
                ForceNaturalDisasterSequence = true,
                MenaceBias = 0.15f,
                Priority = 70
            };
        }
    }
}
