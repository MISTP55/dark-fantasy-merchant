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
- A city's own house takes its materials from its market, with no gold and no notification, and is not counted as consumption in the week's log.
- A market without a `LabourRule` keeps its population: it hires at once, laid-off workers vanish, it never builds, and the player's workshops in it hire nobody.

## Buildings

- `BuildingsDefinition` holds the player's house and workshop. `TryCreateCost` turns a cost into Core's `BuildingCost`. A cost that cannot be created only makes its building unavailable: `WorldEconomy.HouseCost` or `WorkshopCost` is then null and its buttons stay disabled.
- `Construction` (Core) quotes and builds. The materials come first from the hold of the market tab's ship, then are bought from the market, all or nothing. A button whose quote is impossible reads "Construire" with no amount: the gold of an impossible quote means nothing.
