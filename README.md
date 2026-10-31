# XenoDirector Storyteller – Skeleton

Storyteller mod para RimWorld inspirado na arquitetura de **dois cérebros** do Xenomorfo de *Alien: Isolation*:

| Cérebro | Papel | Equivalente Isolation |
|---------|-------|-----------------------|
| **DirectorBrain** (Macro) | Omnisciente, controla pacing / menace gauge, decide quando spawnar micro-cérebros temporários, orquestra propostas e **reage a restrições** | Director AI |
| **MicroBrain** (N instâncias) | Decisões locais por especialidade (Combat, Narrative, Economy, Social, Anomaly, CrossMod…) | Alien behaviour tree + sensors |

## Sistema de Restrições (novo)

O jogador pode ativar restrições narrativas. O storyteller **reage imediatamente**:

| Restrição | Efeito |
|-----------|--------|
| **Single** | Desabilita todas as formas de ganhar novos personagens (wanderers, refugees, quest pawn rewards, recruit incidents). Social micro-brain para de propor joins. |
| **Zombie Apocalypse** | Força eventos de Anomaly (DeathPall, ShamblerSwarm, etc.). Todas as raids futuras são filtradas para apenas PawnKinds classificados como Undead/Shambler/Zombie de **qualquer mod** ativo. Spawna micro-brain Anomaly temporário. |
| **Nature's Wrath** | Coleta desastres naturais de **todos** os mods carregados e dispara em sequência cíclica. Spawna micro-brain CrossMod temporário. |

### Como funciona internamente

1. `StoryRestriction` — dados declarativos (tags bloqueadas/forçadas, AllowedRaidTags, flags).
2. `RestrictionManager` — cache de regras ativas, API `IsIncidentBlocked` / `IsIncidentForced` / `IsRaidPawnKindAllowed` / `ForcesNaturalDisasterSequence`.
3. Director + MicroBrains consultam o manager em `ProposeIncident`.
4. `StorytellerComp_XenoDirector` aplica o filtro final antes de `yield return`.
5. Harmony patches:
   - `IncidentWorker.CanFireNow` → bloqueia o que passou pelo storyteller vanilla.
   - `PawnGroupMaker.GeneratePawns` → filtra options para só Undead.
   - `QuestPart_SpawnPawn` + `IncidentWorker_WandererJoin` → Single.

Ativação: **Mod Settings** (com save aberto) ou via código `RestrictionManager.Activate("Single")`.

## Objetivos do esqueleto

1. **Detecção de reação do jogador** — `PlayerReactionProfile` + classificação de estilo.
2. **Número escalável de cérebros** — permanente (settings) + temporário (runtime).
3. **Otimização para saves longos + muitos mods** — history bounded, tick throttling, catálogo lazy.
4. **Coordenação multi-mod** — `ModIncidentCatalog` indexa IncidentDefs de todos os mods.
5. **Restrições reativas** — o storyteller muda comportamento de acordo com as regras ativas.

## Estrutura de pastas

```
XenoDirectorStoryteller/
├── About/ + Defs/ + Languages/
├── Source/XenoDirector/
│   ├── Core/                  # Mod, GameComponent, StorytellerComp
│   ├── Brains/                # IBrain, DirectorBrain, MicroBrain, BrainFactory
│   ├── Restrictions/          # StoryRestriction, RestrictionManager  ← NOVO
│   ├── PlayerAnalysis/        # Perfil + observador de reações
│   ├── ModCoordination/       # Catálogo cross-mod
│   ├── Optimization/          # Guards de performance
│   ├── HarmonyPatches/        # Observação + RestrictionPatches
│   └── Utils/
└── README.md
```

## Como adicionar uma nova restrição

```csharp
// Em RestrictionManager.EnsureRegistry() ou via Def futuro:
Register(new StoryRestriction {
    Id = "MechOnly",
    Label = "Mechanoid Hell",
    AllowedRaidTags = { "Mechanoid" },
    ForcedIncidentTags = { "Mechanoid" },
    MenaceBias = 0.2f,
    Priority = 75
});
```

Depois basta `rm.Activate("MechOnly")`.

## Compilação

1. Ajuste os caminhos `RimWorldDir` e `HarmonyDir` no `.csproj`.
2. `dotnet build -c Release`
3. Copie o DLL para `Assemblies/` dentro da pasta do mod.
4. Harmony deve carregar antes.

---

Skeleton criado para servir de base sólida. O Director nunca “cheata” – ele apenas aponta e filtra. Os micro-cérebros agem com base no que sentem + nas dicas + nas **restrições ativas**. O jogador define as regras da história; o storyteller obedece e adapta.
