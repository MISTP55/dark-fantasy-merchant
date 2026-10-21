---
paths:
  - "Assets/Scripts/*/Population/**"
  - "Assets/Tests/EditMode/Population/**"
  - "Assets/Scripts/Core/Economy/CityMarket.cs"
  - "Assets/Data/Economy/Buildings.asset"
---

# Population and buildings

## Population

- `CityPopulation` (Core): the rich, the wealthy and the poor (`SocialClass`, values per class in `PerClass<T>`), each with its room and its satisfaction, and the beggars, who have no place; held by `CityMarket.Population`. Hired families become poor; only the poor rise into the other classes. Food acts only through the satisfaction. Beggars are the only source of new inhabitants, and they consume nothing.
- `SocialRule` (Core) holds the satisfaction and class formulas, in truncated integer arithmetic. A market without one (the tests' default) keeps its classes and satisfactions still; it needs a `LabourRule` to have one.
- The order of a day in `CityMarket.StartDay`, per city:
  1. consumption of every good, each class at its rate, before any workshop hires: a hire turns beggars, who consume nothing, into poor;
  2. the city's workshops, in the catalogue's order: hiring (from the beggars only while the poor's satisfaction is above 0), layoffs into a reserve, production (`WorkforceRule`, `LabourRule`);
  3. the satisfaction of the three classes (`SocialRule`);
  4. the player's workshops, which hire from the beggars after the city, never lay off and produce nothing yet (`PlayerDailyOutputOf` is known, `MerchantOutputRule`; their production will need a trading office, the "comptoir");
  5. the rich and the wealthy take poor or give them back, then the poor without room or unhappy leave the city with their jobs (reserves first, then the city's workers above 30, then the player's);
  6. the beggars, who come or go towards `BeggarTarget`;
  7. a house for each crowded class (`CityHousingRule`, one per class, at most one house per class a day).
- A family sent back to the beggars is taken from the three classes, the unhappy ones giving more (`SendToBeggars`); a worker whose family cannot be taken stays in his reserve.
- The rules are in `CityMarketLabourTests` / `CityMarketHousingTests` / `CityMarketSocialTests` / `SocialRuleTests`.

## Buildings

- `BuildingsDefinition` holds the player's house of each class (`HouseOf`, `HouseRooms`) and workshop; a city's own house of a class takes the materials of the player's, without the gold. `TryCreateCost` turns a cost into Core's `BuildingCost`. A cost that cannot be created only makes its building unavailable: its entry of `WorldEconomy.HouseCosts` or `WorkshopCost` is then null and its buttons stay disabled.
- The house rows of the city panel and the costs line name the houses through `SocialClassNames`, sorted with `NameOrder`.
- `Construction` (Core) quotes and builds the player's buildings.
