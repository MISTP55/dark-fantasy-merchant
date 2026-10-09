# Economy, Phase 1 — Design

Date: 2026-10-08
Status: awaiting review

## Purpose

Give the player something to trade. Cities hold stocks of ten goods, consume
them according to their population and produce a few of them. The player buys
goods in a city where one of their ships lies in port, carries them in the
ship's hold, and sells them in another city. Prices follow the stocks, so
carrying a good from a city that produces it to one that lacks it makes gold.

This is the first loop of the game that earns gold: until now the treasury
only went down, through the weekly wages.

## Scope

In scope:

- Ten goods, counted in barrels ("tonneaux"), defined in assets.
- A hold on every ship, limited in barrels (200 for the merchant ship).
- A population on every city, and a market per city: one stock per good,
  consumed and produced every day.
- Prices derived from the stocks, with a spread between buying and selling.
- Buying and selling in the city panel, for a ship in port.
- The hold shown in the ship panel.

Out of scope:

- Saving and loading.
- Any effect of a shortage on a city (unrest, population loss, events).
- A population that changes.
- Other merchants, and trade between cities without the player.
- Notifications of trade, a trade history, profit tracking.
- Goods with a weight or a size other than one barrel, perishable goods.
- A storage limit in cities: there is none.

## Decisions

Agreed with the user before writing this spec:

| Topic | Decision |
|---|---|
| City inventory | Unlimited storage, finite stock: a city sells only what it holds. |
| Prices | A base price per good, scaled by the city's stock against a target stock. |
| Transaction | Priced barrel by barrel, so a large purchase costs more per barrel. A city sells a little above and buys a little below the same price. |
| Trading UI | Inside the city panel: the market is always shown, the buy and sell buttons appear when a ship in port is selected in the list. |
| Goods | Blé, Poisson, Bois, Bière, Sel, Laine, Cuir, Fer, Vin, Épices. |
| Surplus | Production alone never raises a stock above a ceiling; what the player delivers always adds. |

## Economic model

### Goods

A good has a name, a base price in gold per barrel, and a consumption in
barrels per day per 1 000 inhabitants.

| Good | Base price | Consumption |
|---|---|---|
| Blé | 10 | 2.0 |
| Poisson | 12 | 1.5 |
| Bois | 14 | 1.0 |
| Bière | 15 | 1.5 |
| Sel | 18 | 0.5 |
| Laine | 30 | 0.5 |
| Cuir | 40 | 0.3 |
| Fer | 50 | 0.4 |
| Vin | 60 | 0.6 |
| Épices | 150 | 0.1 |

The order of this table is the order of the catalogue and of every list of
goods on screen.

### Cities

Every city has a population, fixed for now, and for every good one of three
production levels: efficient, inefficient or none.

| City | Size, access | Population | Efficient | Inefficient |
|---|---|---|---|---|
| Elforth | Village, coastal | 1 500 | Poisson, Sel | Bois, Laine |
| Cerbias | Village, coastal | 1 200 | Sel, Épices | Poisson, Vin |
| Hitrun | Village, coastal | 1 800 | Bois, Cuir | Poisson, Fer |
| Sparia | Town, coastal | 7 000 | Laine, Vin | Poisson, Bière |
| Bactfied | Town, coastal | 6 000 | Bois, Fer | Blé, Sel, Cuir |
| Liveria | Capital, river | 18 000 | Blé, Bière, Cuir | Bois, Laine, Vin |
| Hazer Empire | Capital, coastal | 24 000 | Fer, Vin, Épices | Blé, Poisson, Bière |

Every good is produced efficiently by at least one city.

### The daily step

When a day starts, for every city and every good, in this order:

1. **Production.** `production = dailyConsumption × rate`, with a rate of 1.5
   for an efficient good, 1.1 for an inefficient one and 0 otherwise. The
   stock rises by the production, but not above the production ceiling:
   60 days of consumption for an efficient good, 36 for an inefficient one. A
   stock already above its ceiling (the player delivered there) is left as it
   is by this step.
2. **Consumption.** `dailyConsumption = population / 1000 × consumption of the
   good`. The stock falls by it, not below zero. An empty stock has no other
   effect.

A step that crosses several days (fast forward) runs once per day, since
`GameClock.DayStarted` is raised once per day.

Stocks are fractional (`double`): a village consumes less than a barrel of
some goods per day. What is shown and what can be bought is the whole part.

### Starting stocks

A good the city produces starts at its production ceiling. A good it does not
produce starts at the target stock (30 days of consumption). The game opens
balanced and shortages appear during the first month.

### Prices

The target stock of a good in a city is 30 days of its consumption there.
With `r = stock / target`, the price is `base price × m(r)`:

| `r` | `m(r)` |
|---|---|
| 0 | 2.5 |
| 1 | 1.0 |
| 2 and above | 0.5 |

linear between these points. A city with no consumption of a good (population
of 0) uses a multiplier of 1.

An efficient producer left alone therefore settles at half the base price, an
inefficient one a little under it (`r = 1.2`, `m = 0.9`), and a city that does
not produce the good climbs to 2.5 times the base price as it runs out.

### Transactions

A transaction is priced one barrel at a time, in whole coins:

- **Buying** a barrel from a city whose stock is `s`:
  `ceil(price(s − 1) × 1.05)`, at least 1. The stock becomes `s − 1`.
- **Selling** a barrel to a city whose stock is `s`:
  `floor(price(s) × 0.95)`. The stock becomes `s + 1`.

Both use the price at the lower of the two stocks, so buying a barrel and
selling it back at once always loses gold.

Buying a barrel needs at least one whole barrel in the city's stock, a free
barrel in the hold and the gold (`Treasury.TryWithdraw`). Selling needs the
barrel in the hold. A transaction of several barrels trades them one by one
and stops at the first that cannot be traded; it reports how many were traded
and for how much gold. Selling deposits the gold in the treasury
(`Treasury.Deposit`, which exists and had no caller yet).

The prices shown in the panel are those of the next barrel.

### Known consequence of these numbers

Production is a share of the city's own consumption, so a village exports
little (Elforth's fish surplus is about one barrel a day) and the world as a
whole produces less of most goods than it consumes. Cities that do not
produce a good will sit near the maximum price most of the time, and the
player cannot supply everyone. This is accepted for this phase: every number
above is a field of an asset and is meant to be tuned by playing.

## Architecture

### Core

Goods are indices into the catalogue in `Core` (`0` to `goodCount − 1`), so
that it does not depend on the ScriptableObjects.

- `CargoHold` — the hold of one ship: `Capacity`, the barrels of each good,
  `Used`, `Free`, `Add(good, barrels)`, `Remove(good, barrels)`, and `Changed`.
  `Ship.Cargo` holds it, as `Ship.Crew` holds the crew.
- `MarketPricing` — the price curve and the spread: the multiplier points and
  the buy and sell margins, and the functions `BuyPrice` and `SellPrice` for a
  base price, a stock and a target.
- `MarketGood` — the static figures of one good in one city: base price,
  daily consumption, production rate, production ceiling in days.
- `CityMarket` — the stocks of one city. Built from its `MarketGood`s, the
  target days and a `MarketPricing`. `StartDay()` runs the daily step;
  `StockOf`, `AvailableOf` (whole barrels), `BuyPriceOf`, `SellPriceOf`;
  `Take(good)` and `Put(good)` move one barrel; `Changed` is raised once per
  daily step and once per transaction.
- `Trade` — `Buy(market, hold, treasury, good, barrels)` and
  `Sell(market, hold, treasury, good, barrels)`, returning a `TradeResult`
  (barrels traded, gold). They raise the market's and the hold's `Changed`
  once per call, not once per barrel.

### Game

- `GoodDefinition` (`Assets/Data/Goods/*.asset`) — display name, base price,
  consumption per 1 000 inhabitants per day.
- `EconomyDefinition` (`Assets/Data/Economy/Economy.asset`) — the ordered
  list of goods (the catalogue) and the settings: target days (30), the rate
  and ceiling of efficient (1.5, 60 days) and inefficient (1.1, 36 days)
  production, the price multipliers (2.5, 1, 0.5), the buy and sell margins
  (5 %).
- `CityDefinition` gains `population`, `efficientGoods` and
  `inefficientGoods` (lists of `GoodDefinition`). A good in both lists counts
  as efficient, with a warning; a good that is not in the catalogue is
  ignored, with a warning.
- `ShipDefinition` gains `cargoCapacity` (200 for the merchant ship).
- `WorldEconomy` (MonoBehaviour) — owns one `CityMarket` per city of the map,
  created in `Start` (the map view lists its cities in its `Awake`), and runs
  their daily step on `GameClock.DayStarted`. `MarketOf(city)` gives a market,
  or null before `Start`. It needs the world clock, the map view and the
  economy definition.

### UI

**City panel** (`CityInfoPanel.uxml`, `CityInfoPanelController`):

- "Population : 6 000" under the access tag.
- A "Marché" section under the ships in port: one row per good, with the
  city's stock, the price to buy and the price to sell. The buy price reads
  "—" when the city has no whole barrel.
- When a ship in port is selected in the list: the header shows
  "Cale : 60 / 200 tonneaux", and every row gains the barrels of that good in
  the hold and two buttons, [−] to sell and [+] to buy. A click trades 1
  barrel, Shift+click 10, Ctrl+click 100, cut down to what is possible. A
  button that can trade nothing is disabled.
- The rows are built once and rewritten on the market's `Changed`, the hold's
  `Changed` and a change of the selected ship. The treasury also drives the
  [+] buttons (`Treasury.Changed`).
- The market rows are a class of their own (`CityMarketSection`) that the
  controller creates and feeds, so the controller does not double in size.
- Without a `WorldEconomy` reference the section is hidden and the panel
  works as before.

**Ship panel** (`ShipInfoPanel.uxml`, `ShipInfoPanelController`):

- "Cale : 60 / 200 tonneaux" under the crew, then one line per good aboard
  ("Vin : 20"), nothing for an empty hold. Rewritten on the hold's `Changed`.

All of it in French, numbers written with `TreasuryHudController.FormatGold`'s
thousands separator.

### Setup

`WorldMapSetup.Build` creates the ten good assets, `Economy.asset` and the
`World Economy` object when missing, and adds the `World Economy` object and
the empty `worldEconomy` and `playerTreasury` references of
`CityInfoPanelController` and `economy` of `ShipInfoPanelController` to an
existing scene, like the other objects it retrofits.

A city references its goods by asset, so the seven existing city assets are
not edited by hand: the setup tool gives the population and productions of
the table above to every city of the map that has a population of 0, new or
existing, and leaves the others untouched. `ShipDefinition.cargoCapacity`
defaults to 200, which the existing `MerchantShip.asset` takes on load.

## Error handling

- A trade that cannot happen changes nothing and returns zero barrels; the UI
  prevents it by disabling the button.
- A negative or zero quantity, or a good index outside the catalogue, throws
  `ArgumentOutOfRangeException` in `Core`.
- A city with a population of 0 has empty markets with base prices and logs a
  warning once at startup.
- An `EconomyDefinition` with no good, or with the same good twice, logs an
  error and the economy is not started.

## Testing

EditMode tests for `Core`:

- `CargoHold`: capacity, add and remove, refusal beyond the capacity or the
  quantity held, `Changed`.
- `MarketPricing`: the multiplier at 0, 0.5, 1, 1.5, 2 and beyond; rounding of
  the buy and sell prices; a round trip loses gold at every stock.
- `CityMarket`: consumption stops at zero; production stops at the ceiling and
  leaves a stock above it alone; an efficient good settles at its ceiling, a
  good that is not produced runs out; starting stocks; whole barrels available
  from a fractional stock.
- `Trade`: a purchase moves barrels and gold; it stops at the stock, at the
  hold and at the gold; a sale deposits; the price rises through a purchase of
  several barrels; `Changed` is raised once per call.

The panel is checked by hand in the Editor: a ship in port buys in one city,
sails, and sells in another for a profit; the buttons disable at the limits;
the market moves when a day starts.
