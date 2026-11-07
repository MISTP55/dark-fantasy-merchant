# Removing the social classes — design

## Intent

A city's population today has three housed classes (the rich, the wealthy and the poor) and the beggars. The poor rise into the other classes and fall back, each class has its own houses, its own consumption of every good and its own satisfaction. The user does not like this system. They will design another one later, in another form, but for now they want the game, and the management of the cities' populations, simpler: the classes go, completely *(the user's intent)*.

What is left is two groups:

- the **beggars**, unchanged;
- the **workers' families**, "Ouvriers" for the player: today's poor, renamed. They are hired from the beggars, live in one kind of house, and go back to the beggars or leave the city when they have no room, as the poor do today. They have **no satisfaction**: the user first chose to keep the poor's, then removed it as well.

## Goals

- Remove the rich and the wealthy, everything that moves inhabitants between classes, and every per-class value, from Core, Game, Editor, the data assets, the UI and the debug tools.
- Keep the poor's behaviour for the housed population, unchanged except where this document says otherwise.
- Keep the economy's volumes roughly as they are: a city consumes about as much of each good as it does today.

## Non-goals

- No new population mechanic, nothing in place of the satisfaction.
- No rebalancing of other values (prices, thresholds, workshops, AI merchants) beyond what is written here.

## Anti-goals

- **Nothing kept "for later"**: no `SocialClass` enum with one value, no `PerClass<T>` of one, no class parameter that only ever takes one value. The future system will be built its own way *(the user's decision)*. `SocialClass` and `PerClass<T>` are deleted.
- The economy must not be thrown off balance: a change that makes demand for the goods the rich and wealthy consumed collapse is a failure, even if it compiles and tests pass.

## The population

- `CityPopulation` holds the **housed** inhabitants (one count), their **room** and the **beggars**. Its existing class-less members (`Housed`, `Room`, `Beggars`, `Inhabitants`, `StartingInhabitants`, `GrowthSinceStart`) stay; the per-class ones become single ones (free room, add room, leave), and the satisfaction ones go. *(Builder's call on exact names; my suggestion is to keep "housed" in code, since "worker" already means a job in `CityWorkforce` and `LabourRule`, and to write "Ouvriers" only in what the player reads.)*
- **Start of a game**: every starting worker's family is housed (`inhabitantsPerWorker` each); there is no longer a 10 / 30 / 60 split. The beggars come as today (`beggarsPerWorker`). The starting room is a whole number of houses, rounded up (`RoomAtStart` with one room per house).
- **Hiring** turns beggars into housed, as it turns them into poor today.
- **Sending families back to the beggars** (`SendToBeggars`): with one group, there are no weights by class nor by satisfaction. Families are taken from the housed, never leaving fewer than one housed inhabitant; a worker whose family cannot be taken whole keeps his job, as today *(the user's decision: "never below one"; the exact arithmetic is the builder's, as long as the housed stay whole families)*.
- `Move` (between classes) disappears.

## The satisfaction: removed

The satisfaction goes too, entirely *(the user's decision, taken after the classes)*:

- no satisfaction in `CityPopulation`, no satisfaction formula (the goods' terms, the target, the daily step, the bounds, the starting value), no satisfaction weight on the goods;
- a city's workshops **hire from the beggars without condition** on a satisfaction: only the beggars and the free room limit them, as they already do *(consequence of the removal)*;
- the hundredth of the housed who left each day while unhappy no longer leaves;
- the stocks of the goods no longer act on the population at all: they are still consumed, and still act on the prices, the workshops and the trade, but a shortage no longer makes anyone leave nor stops the hiring. This is expected *(consequence of the removal; the future system will bring an effect back)*.

What stays of today's `SocialRule`: the housed **beyond their room** leave the city each day, a quarter of the excess plus one, never the last one, taking their jobs with them (reserves first, then the city's workers above 30, then the player's), as today. Its settings in `EconomyDefinition` are the only ones kept (the protected workers, 30); every other setting of the section "Satisfaction and classes" goes. *(Builder's call where this rule lives: a reduced `SocialRule` under a better name, or `LabourRule`. A city without the rule keeps its housed still, as a city without `SocialRule` does today.)*

`LabourRule.BeggarSatisfaction` (the constant 50 that sets the beggars' target) is not the classes' satisfaction: it stays as it is *(my call)*; the builder may rename it if its name misleads.

## The day of a city

`City.StartDay` keeps its order, minus the classes:

1. consumption of every good, at the housed's rate;
2. the city's workshops, hiring from the beggars with no satisfaction condition (otherwise unchanged);
3. ~~the satisfaction~~ — removed;
4. the player's workshops (unchanged);
5. ~~the rich and the wealthy take poor or give them back~~ — removed; then the housed beyond their room leave the city with their jobs;
6. the beggars (unchanged);
7. at most one house a day, built by the city when the housed are above **95 %** of their room (the poor's threshold of today, `cityPoorHouseOccupancy`) *(accepted by the user)*; with a grid, only with the roads that connect it, as today.

## The goods

- Each good has **one weekly consumption per thousand**, for the housed. Its value is the average of today's three, weighted by the starting split: **0.1 × rich + 0.3 × wealthy + 0.6 × poor** *(the user's decision)*, computed once from the current assets and written into each good's asset (e.g. Beer: 0.1 × 22.75 + 0.3 × 45.5 + 0.6 × 22.75 = 29.575). The three fields of `GoodDefinition` become one; the values are not computed at run time.
- The goods have **no satisfaction weight** any more: the three fields of `GoodDefinition` go.
- `MarketGood` follows (one rate, no weight).

## Houses and buildings

- `RichHouse.asset` and `WealthyHouse.asset` are deleted (with their `.meta`). `PoorHouse.asset` becomes the one house: displayed "Maison d'ouvriers", 140 places, 5 000 gold, its materials and footprint (2 × 1) unchanged *(accepted by the user)*. *(Builder's call whether the asset file is renamed, e.g. `House.asset`, moving its `.meta` with it.)*
- `HouseDefinition` loses its class. `BuildingsDefinition` holds **exactly one house** (instead of one per class); it gives every city its starting room and the city's own houses their materials, as today.
- `BuildingKind.House` has no class; `CityGrid.HousesOf` / `ConnectedHousesOf` count every house; `CityHousing` and `CityHousingRule` have one rule, without class; the player's houses (`PlayerBuilder.TryBuildHouse`, `Construction`) have no class.
- The build bar offers one house.
- The city view draws every house in one colour, the poor house's of today (`#9E946D`) *(my call)*.

## City layouts

- `CityLayoutDefinition.StartingHouse` loses its class.
- The layout generation (`StartingHouses`, `StartingLayout`, `CityLayoutEditing.TryGenerate`) places only houses of the one kind, as many as the starting room needs.
- The City Layout window has one house tool instead of three, and one room line ("rooms placed / needed (houses needed)").
- **The existing layouts are trimmed, not regenerated** *(the user's decision)*. In every city's layout, keep as many starting houses as the city's starting population needs (its starting room / 140), and remove the others, **the rich houses first, then the wealthy ones** *(the user's decision)*. In practice: the poor houses are all kept, then the wealthy, then the rich, until the count is reached. All houses are 2 × 1, so a kept rich or wealthy house stands where it is, as a house of the one kind.
  - Most layouts have 3 rich, 5 wealthy and 5 poor houses (Oslo: 3, 4, 4). The count kept is each city's, by the new rule: the whole housed population (starting workers × `inhabitantsPerWorker`), rounded up to whole houses of 140. With today's cities (about 1 000 to 1 064 housed) that is 8 houses (7 in Oslo): the 5 poor and 3 wealthy ones (Oslo: 4 poor and 3 wealthy). The trim script computes it this way itself; it does not call today's `StartingRoomOf`, which rounds class by class and would give 9.
  - A city that would need more houses than its layout has keeps them all; the builder reports it to the user rather than adding houses *(my call)*.
  - The roads that led to a removed house are left as they are *(my call)*: they stay city roads, harmless.
  - The trim reads each house's class, so it is done while the class still exists in the layouts' data (before the field goes), by a one-off script run in the Editor, as the project's rules ask for data assets; the script is not kept.
- Every layout must stay usable (`TryCreateGrid`, `CityViewContentTests`), and each city's starting houses must be connected to the town hall, as today.

## What the player reads

- The city panel's population line: "Ouvriers : 660 · Mendiants : 64" (instead of "Riches : … · Aisés : … · Pauvres : … · Mendiants : …").
- Every other text naming a class ("pauvres", "riches", "aisés") is rewritten for "ouvriers" or removed.

## Debug tools

- The cities table and the population table: one column for the housed ("Ouvriers": count and room, written as the poor's column is today, without its satisfaction) instead of three, with its total.
- `CityIndicator`: the rich, wealthy and poor counts and rooms become one count and one room, read "Ouvriers" and "Place des ouvriers" *(my call on the wording)*; the three satisfactions go.
- The city curves: the satisfaction chart goes; the population chart's class series become one. `DebugChartColors` keeps one colour for them.

## Rules and documentation

- `.claude/rules/city.md` is rewritten for one housed group without satisfaction (population, the day's order, the catalogue's one house). `city-view.md`, `economy.md` and `debug-tools.md` are updated where they name classes. `CLAUDE.md` needs no change unless it names classes.
- The tests that cover classes are rewritten for one group, with the behaviour above (`CityPopulationTests`, `CitySocialTests`, `SocialRuleTests`, `CityHousingTests`, `BuildingsDefinitionTests`, the layout and grid tests, and every test helper that builds a `PerClass`). Tests of what disappears (rising, falling, class targets, the satisfaction and its effect on hiring and leaving) are deleted.

## Done when

- No `SocialClass`, `PerClass`, rich, wealthy or class satisfaction remains in the code, the assets, the UXML, the rules or the docs written by this work (the older specs and plans in `docs/superpowers/` stay as they are).
- The EditMode tests pass (`run-tests`), `Check Scenes` reports nothing, every city layout is usable, and the console is clean after entering Play mode and running a few weeks at fast forward.

## Left to the builder

- Exact names in code, the order of the work, and how the one-off scripts (goods' averages, layout trim, deletion of the house assets) are run, within the project's rules.
- The exact arithmetic of `SendToBeggars` with one group, within the rule above.
