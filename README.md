# XenoDirector Storyteller

Storyteller + otimização + **orquestração narrativa profunda** para RimWorld.

Inspirado na IA dual do Xenomorfo de *Alien: Isolation* (Director + micro-cérebros).

## O que funciona SEM ser o storyteller selecionado

| Sistema | Setting | Efeito |
|---------|---------|--------|
| **Optimization layer** | Enable optimization layer | Scheduler cooperativo, hibernação de sistemas frios |
| **Advanced performance** | Enable advanced performance (**opt-in**) | Throttle de pawns distantes, soft GC, world-pawn pressure |
| **Restrictions** | Restrictions always active | Single / Zombie Apocalypse / Nature's Wrath via Harmony |
| **Narrative arcs** | Enable narrative arcs | Cadeias pós-resgate, etc. |
| **Quest mutation** | Enable quest mutation | Varia quests vanilla |
| **Global story events** | Enable global story events | Secas regionais, pandemias, guerras por migração |

Com **XenoDirector** selecionado, além disso entram Director + micro-cérebros + menace gauge completa.

---

## Performance avançada (opt-in)

Desligada por padrão. O jogador ativa em **Options → Mod settings**:

- **Dynamic entity throttling** — pula ticks de animais/NPCs distantes (nunca colonos do jogador nem hostis perto da câmera)
- **World pawn soft cap monitoring**
- **Periodic soft GC hint** (~3 dias de jogo)

Estilo “otimizador ativo” (ideia similar a mods de Minecraft), adaptado ao pipeline single-thread do RimWorld.

---

## Orquestração narrativa (acesso interno)

`NarrativeWorldAccess` + `StoryArcManager` ligam-se a:

- `IncidentWorker` / raids / letters  
- `QuestManager` (mutação de quests)  
- `Faction` goodwill / settlements  
- `WorldGrid` / tiles (seca → migração → tensão)  
- Biotech **xenotypes** (caçadores após resgate)  
- Ideology **memes** (respostas à pandemia: genética, árvores sagradas, void/dor…)

### Exemplo: resgate → cadeia

1. Recrutamento de pawn de outra facção → `StartRescueChain`  
2. Caçadores de xenotype (Biotech)  
3. Tentativa de assassinato  
4. Oferta de escolta de retorno  
5. Emboscada  
6. Raid ligada à facção de origem  

### Exemplo: seca global

1. Condição de seca/calor na região  
2. Planos de migração com tile candidato (sem mover settlements automaticamente)
3. Hostilidade entre facções deslocadas  

### Exemplo: pandemia

1. Outbreak de doença misteriosa nos colonos  
2. Cada facção reage segundo ideologia (genes / Anima-Gauranlen-Polux / void)  

DevMode: botões no settings para disparar seca e pandemia de teste.

Arcos de resgate consultam Ideology e Biotech: ideologias primárias e minoritárias
de facções visíveis são verificadas em busca de preceitos de aversão a xenótipos,
incluindo termos negativos comuns usados por mods. Preceitos são inspecionados de
forma limitada por reflexão; uma preferência positiva por xenótipo, sozinha, não
é tratada como ódio. O arco verifica o mundo a cada etapa e gera um plano
temporário: perseguidores ideológicos, facções rivais à facção original do pawn,
interferências e a oferta de retorno podem variar conforme as facções e ideologias
atuais. As opções e os planos não são serializados. Quests de escolta são geradas
sob demanda a partir de `QuestScriptDef` carregadas cujo conteúdo indica
escolta/resgate/retorno/transporte; nenhuma quest-modelo é mantida em cache.
Preceitos modded só podem ser reconhecidos quando seus nomes/tipos e dados expõem
explicitamente xenótipo e aversão.

O contrato `INarrativeContentFactory` separa a criação de eventos e missões
temporários. O factory de resgate monta eventos próprios com motivação, facção,
texto e efeito escolhidos no momento, usando ataques do jogo como mecanismo; a
missão de retorno é criada separadamente e pode ser combinada com novas peças
narrativas sem salvar modelos ou listas de possibilidades no arco.

---

## Estrutura Source

```
Core/           Mod, GameComponent, StorytellerComp
Brains/         Director + Micro (storyteller)
Restrictions/   RestrictionManager (sempre aplicável)
Narrative/Arcs/ StoryArc + StoryArcManager
Access/         NarrativeWorldAccess (ponte para APIs do jogo)
Optimization/   Scheduler, Opt brains, AdvancedPerformanceController
HarmonyPatches/ Restrictions, Narrative sync, Advanced tick throttle
```

---

## Compilação

Ajuste caminhos RimWorld/Harmony no `.csproj`, `dotnet build -c Release`, copie DLL para `Assemblies/`.

Harmony deve carregar antes deste mod.

## Testes isolados

Execute `dotnet run --project Tests\RestrictionPatchTests\RestrictionPatchTests.csproj -c Release`.
Os testes usam doubles das APIs do RimWorld e verificam a filtragem de PawnKinds, a enumeração lazy e a restauração das opções após falhas; não substituem o teste dentro do jogo.

---

O Director não cheata: aponta, filtra e sincroniza. Restrições e arcos obedecem às settings mesmo com Cassandra no comando. Performance agressiva só com consentimento explícito do jogador.
