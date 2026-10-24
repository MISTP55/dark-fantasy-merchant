---
paths:
  - "Assets/Scripts/*/Population/**"
  - "Assets/Tests/EditMode/Population/**"
  - "Assets/Scripts/Core/Economy/CityMarket.cs"
  - "Assets/Data/Buildings/**"
---

# Population and buildings

## Population

- `CityPopulation` (Core): the rich, the wealthy and the poor (`SocialClass`, values per class in `PerClass<T>`), each with its room and its satisfaction, and the beggars, who have no place; held by `CityMarket.Population`. Hired families become poor; only the poor rise into the other classes. Food acts only through the satisfaction. Beggars are the only source of new inhabitants, and they consume nothing.
- `SocialRule` (Core) holds the satisfaction and class formulas, in truncated integer arithmetic. A market without one (the tests' default) keeps its classes and satisfactions still; it needs a `LabourRule` to have one.
- The order of a day in `CityMarket.StartDay`, per city:
  1. consumption of every good, each class at its rate, before any workshop hires: a hire turns beggars, who consume nothing, into poor;
  2. the city's workshops, in the catalogue's order: hiring (from the beggars only while the poor's satisfaction is above 0), layoffs into a reserve, production (`WorkforceRule`, `LabourRule`), which takes its inputs from the stocks as they stand (a good made earlier in the catalogue's order is already there);
  3. the satisfaction of the three classes (`SocialRule`);
  4. the player's workshops, which hire from the beggars after the city, never lay off and produce nothing yet (`PlayerDailyOutputOf` is known, `MerchantOutputRule`; their production will need a trading office, the "comptoir");
  5. the rich and the wealthy take poor or give them back, then the poor without room or unhappy leave the city with their jobs (reserves first, then the city's workers above 30, then the player's);
  6. the beggars, who come or go towards `BeggarTarget`;
  7. a house for each crowded class (`CityHousingRule`, one per class, at most one house per class a day). With a grid, a house the strategy finds no place for is not built (nothing is taken).
- A family sent back to the beggars is taken from the three classes, the unhappy ones giving more (`SendToBeggars`); a worker whose family cannot be taken stays in his reserve.
- The rules are in `CityMarketLabourTests` / `CityMarketHousingTests` / `CityMarketSocialTests` / `SocialRuleTests`.

## Buildings

- Each building the player can build is an asset of its own in `Assets/Data/Buildings/` (`Houses/`, `Workshops/`): a `BuildingDefinition` (name, gold, materials), either a `WorkshopDefinition` (its one good, its jobs) or a `HouseDefinition` (its class, its room). `TryCreateCost` turns its cost into Core's `BuildingCost`, `CostText` writes it for the player.
- `BuildingsDefinition` (`Buildings.asset`) is the catalogue that `WorldEconomy` references. It must hold exactly one house per class: that house also gives every city its starting room (`HouseRooms`), and a city's own house of a class takes its materials, without the gold. A good has one workshop at most, active or not; the workshop of an inactive good is ignored. A catalogue that breaks this leaves the cities without a market. The scene setup completes the catalogue by what it holds, not by asset paths (`WorldMapSetup.CompleteBuildings`): it drops the entries of deleted definitions, gives a class or a good that lacks one the definition it creates, and a new catalogue every one that exists; one taken out of the catalogue by hand stays out.
- The catalogue turns its definitions into `HouseOffer` / `WorkshopOffer` (name, cost, cost text, room or jobs, a workshop's good by its index among the active goods), which `WorldEconomy` exposes. A cost that cannot be created only makes its building unavailable: its offer's cost is null and it cannot be quoted or built.
- Core knows a player's workshop only by its good and its jobs (`CityMarket.AddPlayerWorkshop`), never by its definition.
- `Construction` (Core) quotes and builds the player's buildings.
- The player builds through `PlayerBuilder` (scene component of the `Simulation` group, reachable through `SimulationServices`) and nowhere else: it quotes and builds a house or a workshop at a place of the city's grid (`TryBuildHouse`/`TryBuildWorkshop`), paying first from the hold of the first player's ship docked in the city (sorted with `NameOrder`), posts the notification and raises `MaterialBought` for each material bought (the debug recorder records it as a construction). There is no building without a place on the grid.
