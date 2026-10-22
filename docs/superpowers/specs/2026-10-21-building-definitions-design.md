# Building definitions — design

## Intent

The player's buildings are all described today by one asset, `Assets/Data/Economy/Buildings.asset` (`BuildingsDefinition`): one generic workshop (one cost, 30 jobs, the same for every good) and one house per social class, whose names come from the class ("Maison de pauvres"). The "Bâtiments" tab of the city panel shows a row per house class and a row per good the city produces, each with a "Construire" button, and one line under the buttons that lists the cost of every building.

This work gives each building its own definition asset, so that each one can have its own attributes. A workshop gets a name of its own ("Bûcheron" for timber, not "Bois"), its own cost and its own number of workers. Houses get the same treatment. This is the base on which buildings with more specific attributes will be built later.

## Goals

- Every building the player can build is a ScriptableObject asset of its own.
- Every building has, at least:
  - a **name**, which the player reads in the "Bâtiments" tab and in the notifications;
  - a **gold cost**;
  - a **list of materials** (good + units), as today.
- A **workshop** definition adds:
  - the **good** it produces, exactly one;
  - its **maximum workers**: the same concept as today's `workshopJobs`, now per workshop type.
- A **house** definition adds:
  - its **social class**;
  - its **places** (today's `richHouseRoom` / `wealthyHouseRoom` / `poorHouseRoom`).
- The cost of a building is shown when the pointer is over its "Construire" button, in place of today's line that lists every cost.

## Non-goals

- The cities' own workshops. They stay as they are: one block per good, whose maximum of workers is set on the good (`GoodDefinition`). Nothing in this work touches them.
- Workshops producing several goods. A workshop produces exactly one good; this was considered and dropped.
- Any new attribute beyond those listed above, or any new gameplay: production of the player's workshops, hiring rules, the merchants' output bonus all work as today.
- Any change to the "Bâtiments" tab beyond the names, the per-building cost and the per-workshop workers.

## Anti-goals

- Changing how the simulation behaves. With the default content, a game plays exactly as before: same costs, same jobs (30), same places, same city houses.
- Tying the player's workshops to the definition asset in Core. Core keeps knowing a player's workshop by its good and its jobs (`CityMarket.AddPlayerWorkshop(good, jobs)`); the definition stays in Game.

## Constraints

- Project conventions (root `CLAUDE.md`): definitions are ScriptableObjects holding static data only; player-facing text in French; lists the player reads sorted with `NameOrder`; materials go through the "switched-off materials" rule (`BuildingsDefinition.MaterialsOf`: inactive materials are left out, never make a building unavailable); `WorldMapSetup.Build` must create every new data asset and reference; assets are moved with their `.meta`; no mention of the game this one is inspired by.
- The house of a class also sets the houses a city builds of its own (`CityHousingRule`: the materials of the player's house of the class, without the gold) and the room every city starts with. That stays: so there must be exactly one house definition per class.
- Runtime UI is UI Toolkit. Its `tooltip` property is only shown in the Editor, not at runtime: the hover must be built by the game.

## The design

### Definitions

- An abstract base `BuildingDefinition : ScriptableObject` holds what every building has: `displayName`, `gold`, `materials` (the existing `MaterialAmount` list).
- `WorkshopDefinition : BuildingDefinition` adds `good` (`GoodDefinition`) and `jobs` (at least 1).
- `HouseDefinition : BuildingDefinition` adds `socialClass` (`SocialClass`) and `room` (at least 1).
- `BuildingsDefinition` stays as the catalogue that `WorldEconomy` references: it holds a list of `HouseDefinition` and a list of `WorkshopDefinition`, and keeps the helpers that work on a building (`TryCreateCost`, `MaterialsOf`, the cost text), now taking a `BuildingDefinition`. It answers "the house of a class" and "the workshop of a good" (null when the good has none).

### Validation

`BuildingsDefinition.TryValidate` makes the buildings unusable (the cities have no market, as today) when:
- a class has no house, or more than one;
- a good has more than one workshop;
- a listed definition is missing (null), a workshop has no good, a name is empty, jobs or room are under 1.

As today, a building whose cost cannot be worked out (a negative gold) is only unavailable to the player: its row stays, its button disabled.

A workshop whose good is not an active good of the economy is ignored (no row anywhere), the same way an inactive material is left out. It still counts for the duplicate check: two workshops of the same good are an error whether the good is active or not.

### The "Bâtiments" tab

- **Houses**: one row per house definition, sorted by its name, reading `"<name> : 2 (ville : 1 008 / 1 300 places)"`. Default names are today's singular names: "Maison de riches", "Maison d'aisés", "Maison de pauvres".
- **Workshops**: one row per workshop definition whose good the city produces (as today: `market.MaxWorkersOf(good) > 0`), sorted by the workshop's name, reading `"Bûcheron : 2 ateliers (45 / 60 ouvriers)"` / `"Bûcheron : 0 atelier"`. The jobs shown are the sum of the jobs of the player's workshops of that good, each built with its definition's jobs. A good the city produces but that has no workshop definition has no row.
- **Buttons** keep their current text ("Construire (12 340 or)", the price now, the materials in the hold counted first).
- **Cost on hover**: when the pointer is over a "Construire" button, enabled or disabled, a label shows that building's full cost: `"10 000 or, 10 bois, 30 métal"` (today's per-building cost text: gold, then the active materials in alphabetical order, lowercase), or "indisponible" for a building whose cost cannot be worked out. It disappears when the pointer leaves.
- The costs line (`buildings-costs` in `CityInfoPanel.uxml`) and `BuildingsDefinition.CostsText` are removed.

### Notifications

The notifications use the building's name. Because a free name has no known gender, the sentence avoids the agreement: `"Construction achevée à Lübeck : Bûcheron."` for both houses and workshops (today: "Maison de pauvres construite à Lübeck." / "Atelier construit à Lübeck : céréales.").

### Runtime

- `WorldEconomy` exposes the cost of each house (by class, as now) and the cost of the workshop of each good (indexed by the good's index among the active goods; null when the good has no workshop or its cost cannot be worked out), in place of the single `WorkshopCost`.
- `CityBuildingsSection` receives the definitions (or what it needs from them: names, costs, jobs, rooms) instead of `goodNames` / one workshop cost / one `workshopJobs` / one costs text.
- Core is unchanged: `Construction.TryBuildWorkshop(…, good, jobs, …)` and `TryBuildHouse(…, housedClass, room, …)` already take what they need.

### Content and scene setup

- The assets go in `Assets/Data/Buildings/`: `Houses/` and `Workshops/`. `Buildings.asset` (the catalogue) moves there with its `.meta`, so `WorldEconomy`'s reference holds.
- `WorldMapSetup.Build` creates each missing definition with today's values, and adds a definition to the catalogue when it creates it:
  - houses: rich 25 000 gold, 25 timber, 30 metal, 40 places; wealthy 10 000 gold, 15 timber, 20 metal, 70 places; poor 5 000 gold, 10 timber, 10 metal, 140 places;
  - one workshop per good of the sample goods, each 10 000 gold, 10 timber, 30 metal, 30 jobs, with these names:

    | Good | Workshop |
    |---|---|
    | Bière | Brasserie |
    | Bois | Bûcheron |
    | Brai | Goudronnerie |
    | Céréales | Ferme |
    | Chanvre | Chanvrière |
    | Cuir | Tannerie |
    | Épices | Jardin d'épices |
    | Étoffe | Tisserand |
    | Fonte brute | Fonderie |
    | Huile | Pressoir à huile |
    | Laine | Bergerie |
    | Métal | Forge |
    | Miel | Rucher |
    | Peaux | Trappeur |
    | Poisson | Pêcherie |
    | Poterie | Atelier de potier |
    | Sel | Saline |
    | Tuiles | Tuilerie |
    | Viande | Élevage |
    | Vin | Vignoble |

- The existing `Buildings.asset` loses its old fields. The builder migrates it once, in this work, by running the setup (which creates the definitions with the values above, equal to today's) and commits the resulting assets.

### Tests and documentation

- `BuildingsDefinitionTests` and `EconomyContentTests` follow the new shape: validation cases above, the workshop of a good, costs per definition, content values. Text helpers (rows, hover text, notification) get tests like today's.
- `.claude/rules/population.md` ("Buildings") is updated: the definitions, the catalogue, one house per class, at most one workshop per good, where they live. Its `paths` follow the catalogue to `Assets/Data/Buildings/`.

## Left to the builder

- Exact class, field and method names; whether `CityBuildingsSection` takes the definitions or plain values.
- How the hover label is built and positioned (an element in the panel that follows the button, with `pickingMode = Ignore`), and its USS, matching the panel's styles. A disabled button receives no pointer events, so the hover is likely registered on its row or a wrapper.
- Whether `SocialClassNames.HouseOf` / `HousesOf` are still needed after the change; remove what nothing uses.
- The custom inspector, if any (none required).
