# Patrician III Goods — Design

Date: 2026-10-15
Status: awaiting review

## Purpose

Replace the six goods of the game by the twenty of Patrician III, with their base
prices (`patrician3-re\resultats\Calcul des prix.md`) and their consumption
(`Production et consommation des villes.md`), and bring in Patrician III's second
unit: the **bushel** ("boisseau", Patrician III's "charge"), worth 10 barrels. A good
is counted, priced and stocked in its own unit; only the hold of a ship counts in
barrels of room, where a bushel takes 10.

## Scope

In scope:

- The twenty goods of Patrician III, in its order, with their unit, base price,
  consumption, category and workshop figures.
- The bushel: a unit of a good, worth a setting of 10 barrels of room in a hold.
- Decimal base prices.
- New productions for the seven cities of the map.
- Holds, trade, the AI merchants' planner and the panels made aware of units.

Out of scope:

- The four weapons of Patrician III (not consumed by inhabitants, no base price read).
- Social classes: consumption is the average of the three classes (decided).
- Inputs of workshops and the leather by-product of cattle: spices, oil and leather
  get ordinary workshops (decided).
- New ship types or hold sizes: the merchant ship keeps 200 barrels of room.
- Balancing beyond the method below.

## Decisions

Taken with the user:

- **The twenty goods**, no weapons.
- **Consumption** per 1 000 inhabitants is the average of Patrician III's three
  classes weighted by its starting mix: 10 % rich, 30 % wealthy, 60 % poor.
- **Spices, oil and leather** get ordinary workshops (maximum 60 workers).
- **Decimal base prices**, exactly Patrician III's; the prices paid stay whole coins.
- **Food**: grain, meat, fish and beer.
- **Approach A**: the unit is a property of the good; Core holds know the room a unit
  takes; stocks, prices, thresholds, consumption and production stay in the good's unit.
- **Productions** of the cities: the thematic split below.

## The goods

In Patrician III's order, which becomes the order of the catalogue. Base price per
unit; consumption per 1 000 inhabitants per week, in the good's unit; maximum workers
of a workshop (Patrician III's, 60 where it has none); output per worker per day for
an efficient production, in the good's unit:

| Good | Asset | Unit | Base price | Weekly / 1 000 | Food | Max workers | Output / worker |
|---|---|---|---|---|---|---|---|
| Céréales | Grain | bushel | 110 | 4.725 | yes | 68 | 0.03396 |
| Viande | Meat | bushel | 957 | 1.552 | yes | 60 | 0.01643 |
| Poisson | Fish | bushel | 440 | 3.08 | yes | 65 | 0.02316 |
| Bière | Beer | barrel | 34.8 | 29.575 | yes | 68 | 0.21254 |
| Sel | Salt | barrel | 28.5 | 0.35 | | 54 | 0.01029 |
| Miel | Honey | barrel | 110 | 5.425 | | 60 | 0.08206 |
| Épices | Spices | barrel | 280 | 0.77 | | 60 | 0.02038 |
| Vin | Wine | barrel | 220 | 9.24 | | 93 | 0.15780 |
| Étoffe | Cloth | barrel | 206.8 | 8.575 | | 60 | 0.22699 |
| Peaux | Skins | barrel | 676.5 | 5.25 | | 72 | 0.11581 |
| Huile | Oil | barrel | 82.5 | 7.525 | | 60 | 0.19919 |
| Bois | Timber | bushel | 55 | 1.96 | | 48 | 0.02594 |
| Métal | Metal | barrel | 255.6 | 16.625 | | 78 | 0.19344 |
| Cuir | Leather | barrel | 224 | 6.265 | | 60 | 0.09476 |
| Laine | Wool | bushel | 880 | 0.875 | | 50 | 0.02779 |
| Brai | Pitch | barrel | 55.6 | 0 | | 26 | 0.03846 |
| Fonte brute | PigIron | bushel | 880 | 0 | | 70 | 0.01429 |
| Chanvre | Hemp | bushel | 440 | 0.089 | | 62 | 0.00228 |
| Poterie | Pottery | barrel | 171 | 5.46 | | 58 | 0.14951 |
| Tuiles | Tiles | bushel | 79.8 | 0.016 | | 27 | 0.00094 |

- Consumption: Patrician III's weekly figures per 1 000 of each class (rich, wealthy,
  poor) times 0.1, 0.3 and 0.6, summed.
- Output per worker: 1.5 times the world's starting daily consumption (7 cities of
  250 × 72 ÷ 17 inhabitants), over the producers (efficient 1, inefficient 0.75), over
  the maximum of workers — the method of the workshop design.
- Pitch and pig iron are consumed by no one until workshops have inputs: their output
  is provisional, one unit a day for a full efficient workshop. Their stock rises to
  T3 and their price stays low.
- Food goods are of category `Food`, the others `RawMaterial`.
- The current goods map to new ones: Grain becomes Céréales, Meat Viande, Fish
  Poisson, Beer Bière, Iron becomes Metal (Métal, renamed asset, same GUID);
  Vegetables is removed.

## Productions of the cities

| City | Efficient | Inefficient |
|---|---|---|
| Sparia | Poisson, Sel, Brai | Céréales, Bière |
| Elforth | Miel, Bois, Épices | Poisson, Viande |
| Bactfied | Bière, Métal, Poterie | Céréales, Bois |
| Hitrun | Laine, Viande, Tuiles | Céréales, Cuir |
| Cerbias | Vin, Huile, Chanvre | Poisson, Miel |
| Hazer Empire | Étoffe, Fonte brute, Cuir | Bière, Viande, Métal |
| Liveria | Céréales, Peaux | Bois, Bière, Poisson |

Every good is produced efficiently somewhere. Cities have 5 workshops (Hazer Empire 6),
so 50 workers each at the start (Hazer Empire 42).

## Units

- A good's unit is a barrel or a bushel. A bushel takes `EconomyDefinition`'s barrels
  per bushel (10) of room in a hold; a barrel takes 1.
- Stocks, consumption, production, workers' output, thresholds (their floors of 5, 10,
  15 and 20 included) and prices are in the good's own unit, as in Patrician III: the
  price of grain is the price of a bushel.
- A hold's capacity, `Used` and `Free` are barrels of room; what it carries of a good
  is counted in units of that good.
- A trade or a load never splits a unit: buying stops when the room left is less than
  a unit.

## Code

### Core

- `GoodUnits` (new): the barrels of room a unit of each good takes, by index
  (`RoomOf(good)`, at least 1); `GoodUnits.AllBarrels` for a catalogue of barrels
  only.
- `CargoHold`: takes a `GoodUnits` (optional; without it every unit takes one barrel);
  `BarrelsOf` becomes `UnitsOf`; `TryAdd` and `TryRemove` count units and move their
  room; `RoomFor(good)` is how many whole units of a good still fit.
- `Trade`: buys at most the units that fit (`RoomFor`); its parameters and results
  are in units.
- `TradeRoutePlanner`: takes a `GoodUnits` (optional, as above); loads unit by unit
  the good whose next unit pays the most per barrel of room, among those that still
  fit, while one pays; `TradePlan.BarrelsOf` becomes `UnitsOf`, `TotalBarrels`
  becomes `TotalUnits`, and the plan gives the room it takes (`Room`).
- `AiMerchantFleet`: counts units aboard and units incoming; uses the plan's units.
- `MarketGood`, `MarketPricing`, `CityMarket`: the base price becomes a `double`.
  Nothing else changes: they already count in the good's unit.

### Game

- `GoodDefinition`: `unit` (`GoodUnit`: `Barrel`, `Bushel`); `basePrice` a `double`.
- `EconomyDefinition`: `barrelsPerBushel` (10); `CreateGoodUnits()` and `RoomOf(GoodDefinition)`.
- `ShipsView`: an `economy` reference, whose `CreateGoodUnits()` gives the holds of
  the player's and the AI's ships their units; `WorldMapSetup` wires it.
- `AiMerchants`: gives the planner the economy's units.
- `CityMarketSection` / `CityInfoPanel.uxml`: a good's name is followed by its unit in
  the plural ("Céréales (boisseaux)", "Bière (tonneaux)"); the rows are in a scroll
  view of limited height; a buy price is disabled when not one unit fits in the hold.
- `ShipInfoPanelController`: "Cale : 60 / 200 tonneaux" stays the room; each line of
  the hold gives the quantity in its unit ("Céréales : 3 boisseaux", "Bière : 1 tonneau").
- Debug tools: the cities tab's good headers carry the unit; the ships tab shows
  units per good, and its hold column the room.
- A shared French formatting of a quantity and its unit (singular and plural:
  "1 boisseau", "3 boisseaux", "1 tonneau", "12 tonneaux").

### Content

- The twenty good assets in `Assets/Data/Goods`: the four kept (Grain, Meat, Fish,
  Beer) and the renamed Iron (Metal) get their new figures; fifteen are created by
  the Editor so that Unity makes their `.meta`; Vegetables is deleted with its `.meta`.
- `Economy.asset`: the catalogue in the order above; `barrelsPerBushel: 10`.
- The seven city assets: their productions above.
- `WorldMapSetup`: sample goods and sample cities follow the same content.

### Documentation

`CLAUDE.md`: the goods, the bushel, the productions, the holds and the panels.

## Testing

EditMode tests, in `Assets/Tests/EditMode`:

- `GoodUnitsTests`: room by good, all-barrels default, rejection of a room under 1.
- `CargoHoldTests`: mixed units (room used, `RoomFor`, refusal when a bushel does not
  fit, removal frees its room); the existing tests keep their figures with barrels only.
- `TradeTests`: buying bushels stops when the room left is under 10; selling frees room.
- `TradeRoutePlannerTests`: the best margin per barrel of room wins; a bushel that no
  longer fits is not loaded, a barrel still is.
- `MarketPricingTests` / `CityMarketTests`: a decimal base price (34.8) gives the
  expected whole prices.
- `EconomyDefinitionTests`: units from the definition; `RoomOf`; the barrels per bushel
  default.
- `EconomyContentTests`: twenty goods in order with their names, units, categories and
  base prices; every good produced efficiently somewhere; every city produces something.
- Panels: the formatting of a quantity and its unit; the market section's names and
  its buy price disabled without room for a unit.

Then the whole EditMode suite, and a game run in the Editor: a city's market and a
ship's hold, a purchase of bushels that fills the hold, and the AI merchants trading.
