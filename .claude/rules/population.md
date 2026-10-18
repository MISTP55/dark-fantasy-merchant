---
paths:
  - "Assets/Scripts/*/Population/**"
  - "Assets/Tests/EditMode/Population/**"
  - "Assets/Scripts/Core/Economy/CityMarket.cs"
  - "Assets/Data/Economy/Buildings.asset"
---

# Population and buildings

## Population

- `CityPopulation` (Core): housed inhabitants, beggars and room, held by `CityMarket.Population`. Only labour changes the population; food does not. Beggars are the only source of new inhabitants.
- The order of a day in `CityMarket.StartDay`, per city:
  1. consumption;
  2. the city's workshops, in the catalogue's order: hiring, layoffs into a reserve, production (`WorkforceRule`, `LabourRule`);
  3. the player's workshops, which hire from the beggars after the city, never lay off and produce nothing yet (`PlayerDailyOutputOf` is known, `MerchantOutputRule`; their production will need Patrician III's office, the "comptoir");
  4. the beggars, who come or go towards `BeggarTarget`;
  5. a house the city builds of its own when crowded (`CityHousingRule`, at most one a day).
- The rules are in `patrician3-re\resultats\Population des villes.md` and `Production et consommation des villes.md`, and in `CityMarketLabourTests` / `CityMarketHousingTests`.

## Buildings

- `BuildingsDefinition` holds the player's house and workshop. `TryCreateCost` turns a cost into Core's `BuildingCost`. A cost that cannot be created only makes its building unavailable: `WorldEconomy.HouseCost` or `WorkshopCost` is then null and its buttons stay disabled.
- `Construction` (Core) quotes and builds the player's buildings.
