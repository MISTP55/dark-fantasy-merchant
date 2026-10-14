# Patrician III Pricing — Design

Date: 2026-10-14
Status: awaiting review

## Purpose

Replace how a city prices its goods by the model of Patrician III, the game's
main inspiration, as read from its machine code
(`patrician3-re\resultats\Calcul des prix.md`). A good has one base price; a
city multiplies it by a factor that follows its stock against four thresholds
measured in days of its demand and of its production. The player pays on one
curve and is paid on another: they meet at the base price around a normal
stock and part towards the extremes, so trading in a city in shortage or in
glut costs more. The factors are settings, to be balanced by playing.

## Scope

In scope:

- Four stock thresholds T0 < T1 < T2 < T3 per good and per city, from its
  daily consumption and daily production, with floors in barrels.
- Two price curves of five factors each, one for what the player buys and one
  for what the player sells.
- The price of a barrel as the base price times the average factor over the
  stock the barrel crosses, so that a trade of N barrels costs the integral of
  the factor, as in Patrician III.
- The removal of the per-good minimum and maximum prices and of the two 5 %
  margins.

Out of scope:

- The game option of Patrician III that sets the factor at an empty stock when
  the city buys (2.2, 2.0 or 1.8): it is a plain setting at 2.0 (decided).
- The surcharge of 15 % on a good a city is a large supplier of (decided).
- Any change to production, consumption, population or the AI merchants'
  rules. Production in Patrician III also follows the thresholds; here it
  keeps its ceilings.
- New base prices: the current ones are kept (decided).
- Showing the thresholds or the factors in the debug tools.

## Decisions

Taken with the user:

- **Faithful model, tunable factors.** The mechanism is Patrician III's; its
  numbers are the defaults of settings in `EconomyDefinition`.
- **Thresholds as in Patrician III**, production capacity and floors included,
  each figure a setting.
- **No secondary rule** (game option, surcharged good) in this step.
- **Base prices kept**: beer 50, fish 500, grain 80, iron 300, meat 600,
  vegetables 100.
- **Approach A**: small `Core` classes for thresholds and curves, composed by
  `MarketPricing`; `CityMarket`'s price API keeps its signatures.

## Model

### Thresholds

For one good in one city, with D its daily consumption (`DailyConsumptionOf`,
which follows the population) and P its daily production (the city's
production rate for the good times D, 0 when the city does not produce it):

| Threshold | Formula | Defaults |
|---|---|---|
| T0 | max(T0 days × D, T0 floor) | 7 days, 5 barrels |
| T1 | max(T0 + T1 added days × D, T1 floor) | 14 days, 10 barrels |
| T2 | max(T1 + T2 production days × P, T2 floor) | 10 days, 15 barrels |
| T3 | max(T2 + T0, T3 floor) | 20 barrels |

- The thresholds are worked out when a price is asked, not stored: they follow
  the population of the moment, which changes once a day.
- By construction T0 ≤ T1 ≤ T2 ≤ T3. The T0 floor must be above 0, and the
  floors must not decrease from T0 to T3.
- A good the city does not consume has its thresholds at the floors: its price
  still follows its stock.
- A city that does not produce a good has T2 = T1, unless the T2 floor is
  higher. Its price then falls at once from the T1 factor to the T2 factor as
  the stock crosses T1. This is faithful to Patrician III's formula, and the
  average over a barrel (below) handles the step.

In days of consumption, with the default production rates and well above the
floors: an efficient producer (rate 2) has T0 = 7, T1 = 21, T2 = 41, T3 = 48;
an inefficient one (rate 1.5) has T2 = 36, T3 = 43; a city that does not
produce the good has T2 = 21, T3 = 28.

### Curves

A curve is five factors: at an empty stock, at T0, at T1, at T2, and at T3 and
above. Between two of these points the factor is a straight line; above T3 it
stays at the last one. A segment of zero width (two equal thresholds) is a
step.

| Stock | Player buys (the city sells) | Player sells (the city buys) |
|---|---|---|
| 0 | 4.0 | 2.0 |
| T0 | 1.5 | 1.4 |
| T1 | 1.0 | 1.0 |
| T2 | 0.8 | 0.7 |
| T3 and above | 0.6 | 0.5 |

Rules on the settings, checked when the pricing is created (an
`ArgumentOutOfRangeException` otherwise):

- Every factor is above 0 and finite.
- Along a curve, from the empty stock to T3, a factor is never above the one
  before it.
- At every one of the five points, the factor at which the player buys is at
  least the one at which the player sells. Both curves being straight lines
  between the same thresholds, this holds at every stock.

### Price of a barrel

- **The player buys** one barrel from a city whose stock is s: base price ×
  the average of the buy curve over [s − 1, s], rounded up, at least 1.
- **The player sells** one barrel to a city whose stock is s: base price × the
  average of the sell curve over [s, s + 1], rounded down, at least 0.
- A stock below 0 counts as 0 (the factor at an empty stock).
- A trade of N barrels is priced barrel by barrel, as now
  (`BuyPriceOf(good, barrelsAlreadyBought)`,
  `SellPriceOf(good, barrelsAlreadySold)`). The barrels' averages laid end to
  end are the integral of the factor over the N barrels: before rounding, the
  total is Patrician III's.
- Buying a barrel and selling it back never gains gold: the buy curve is never
  under the sell curve, and the two roundings go against the player.
- The price shown in the market is the one of the next barrel, as now. A city
  sells nothing it holds less than one whole barrel of, as now.
- The small margin of the current code for floating-point rounding is kept
  (20 × 1.05 must round up to 21, not 22).

### Consequences

- At the start every city holds 7 days of every good, which is T0 wherever
  the floor does not apply: prices open at 1.5 and 1.4 times the base, the
  same everywhere, so no trade pays during the first days, as now.
- Production keeps its ceilings (60 days efficient, 36 inefficient). Left
  alone, an efficient producer rises above T3 (48 days) and ends at the lowest
  factors; an inefficient one stops at 36 days, its T2, at 0.8 and 0.7. The
  two kinds of producers now differ in price.
- The spread between the curves is wide in a shortage (4.0 against 2.0) and
  narrow around T1: the AI merchants' planner, which compares the real prices,
  needs no change, but fewer small trades will pay.

## Code

### Core

- `StockThresholds` (new, readonly struct): `T0`, `T1`, `T2`, `T3`, in
  barrels.
- `ThresholdRule` (new): the three day figures and the four floors, validated
  in its constructor; `ThresholdsFor(double dailyConsumption, double
  dailyProduction)` returns the `StockThresholds`.
- `PriceCurve` (new): the five factors, validated in its constructor (above 0,
  finite, never rising); `FactorAt(double stock, StockThresholds)` and
  `AverageFactor(double from, double to, StockThresholds)`, the exact integral
  of the straight lines divided by the width.
- `MarketPricing`: built from the buy curve and the sell curve, and checks
  that the first is never under the second. `BuyPrice(long basePrice, double
  stock, StockThresholds)` and `SellPrice(long basePrice, double stock,
  StockThresholds)` replace the versions with minimum, maximum and target.
  `Price` (the single curve) goes.
- `MarketGood`: loses `minPrice` and `maxPrice`; `basePrice` must be at
  least 1.
- `CityMarket`: takes a `ThresholdRule` in place of `targetDays`;
  `ThresholdsOf(good)` replaces `TargetOf(good)`. `BuyPriceOf` and
  `SellPriceOf` keep their signatures.

### Game

- `GoodDefinition`: loses `minPrice` and `maxPrice`; the tooltip of
  `basePrice` says it is the price at T1.
- `EconomyDefinition`: `targetStockDays`, `buyMargin` and `sellMargin` go.
  Three headers come instead: stock thresholds (the three day figures and the
  four floors), the factors at which the player buys, the factors at which the
  player sells (five each, defaults as above). `CreatePricing` builds the two
  curves; a new `CreateThresholdRule` builds the rule, which `CreateMarket`
  passes to `CityMarket` where it passed `targetStockDays`.
- `Assets/Data/Economy/Economy.asset` and the six good assets: the obsolete
  fields are removed and the new ones set to the defaults.

### Editor

- `WorldMapSetup`: the sample goods give only their base price.

### Unchanged

`Trade`, `TradeRoutePlanner`, `AiMerchantFleet`, `CityMarketSection` and the
debug tools: they price only through `BuyPriceOf` and `SellPriceOf`.

### Documentation

- `CLAUDE.md`: the *Economy* section describes the thresholds, the two curves
  and the average over a barrel in place of the target, the per-good bounds
  and the margins; the line on the ceilings ("a producer left alone ends at
  its minimum price") is corrected per the consequences above.

## Testing

EditMode tests, in `Assets/Tests/EditMode`:

- `PriceCurveTests`: the factor at each of the five points and between them;
  constant above T3; a stock below 0 counts as 0; the average over a range
  inside one segment and over one that crosses a threshold; the step when
  T2 = T1; rejection of a factor at or below 0, of a rising curve.
- `ThresholdRuleTests`: the four formulas; each floor; T2 = T1 for a good not
  produced; thresholds that follow the consumption (so the population);
  rejection of a T0 floor at 0 and of decreasing floors.
- `MarketPricingTests` (rewritten): the next barrel near T1 costs the base
  price up to the rounding; buying rounds up, selling rounds down; a sold
  barrel is never worth more than the same barrel bought; N barrels priced one
  by one sum to the integral up to the rounding; rejection of a buy curve
  under the sell curve at some point.
- The existing tests that build `MarketGood`s (31 calls in six files) or check
  exact prices (`CityMarketTests`, `EconomyDefinitionTests`,
  `MarketPricingTests`) are adapted; their expected prices are worked out
  again from the new model, not copied.

Then the whole EditMode suite, and a game run in the Editor to look at the
market panel's prices and at the AI merchants trading.
