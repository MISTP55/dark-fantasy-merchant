# Workshop Production — Design

Date: 2026-10-14
Status: awaiting review

## Purpose

Replace how cities produce goods by the model of Patrician III, as read from its
machine code (`patrician3-re\resultats\Production et consommation des villes.md`).
A city that produces a good has a workshop for it, with workers up to a maximum
that is the same in every city. Every day the city hires or lays off workers by
where the stock of the good stands against its four price thresholds, and stops
producing above the last one, so the stock of a producer settles between T2 and
T3 whatever its production and consumption. A new game starts as Patrician III
does: every city gets the same budget of workers, spread over its workshops, and
its population follows from its workers.

This is the first part of Patrician III's production and consumption; the others
(social classes, inputs of workshops, seasonal and famine modifiers, hidden
supply) are later specs.

## Scope

In scope:

- A workshop per good a city produces: a number of workers, a maximum of workers
  and a daily output per worker, both settings of the good.
- The daily regulation of the workers by the stock and the thresholds, and the
  production cancelled above T3.
- The day of a city in Patrician III's order: consumption, then production.
- T2 worked out from the workshop's real capacity.
- The start of a game: a workforce budget common to all cities, spread over a
  city's workshops; the population derived from it.
- New production figures in the good assets, from a capacity margin of 150 %.
- The workers of every workshop in the debug tool.

Out of scope:

- Social classes, beggars hired as workers, and any limit on hiring: hiring is
  immediate (decided; to be changed later).
- Inputs of workshops (production chains) and new goods.
- Winter and famine modifiers; hidden supply.
- The year-dependent part of Patrician III's budget (250 + 2.5 per year after
  1300): the budget is a plain setting.
- Per-type weights for spreading the budget: Patrician III's weight is 30 for
  every type matching our six goods, so the budget is spread evenly.
- Showing workers in the city panel of the game.

## Decisions

Taken with the user:

- **Workshops and regulation first**, before social classes.
- **Fixed capacity per good**, the same in every city, as in Patrician III.
- **Abstract workers**: a count per workshop, not taken from the population.
- **Immediate hiring**: the regulation's target is reached the same day.
- **Approach A**: a stateless `WorkforceRule` gives the next workers; `CityMarket`
  holds the workers.
- **Patrician III's start**: the same workforce budget for every city (250), the
  population derived from it (72 ÷ 17 ≈ 4.24 inhabitants per worker: 4, plus a
  seventeenth of beggars).
- **Capacity margin of 150 %**: the output per worker of each good is chosen so
  that the world can produce 1.5 times what it consumes at the start, every
  workshop at its maximum. The margin only sets asset values; nothing in the code
  refers to it.

## Model

### Workshops

- A good has two production settings: its **maximum of workers** and its **daily
  output per worker**, in barrels, for an efficient production.
- A city that produces a good inefficiently gets **0.75** of that output (Patrician
  III's base efficiencies, 768 against 1024), a setting of the economy. A city
  that does not produce a good has no workshop for it: no worker, no capacity.
- The **capacity** of a workshop is its maximum of workers times its output per
  worker (efficiency included): it does not depend on the size of the city.

### A day of a city

For every good, in this order:

1. The inhabitants consume, down to nothing.
2. For a good the city produces, the stock left is compared to the thresholds
   (`ThresholdsOf`):
   - **Above T3**: no production this day, and the workers fall to the setting
     for above T3 (1).
   - **Otherwise**: the workers rise by 16 at or under T0, by 8 above T0, by 4
     above T1, by 1 above T2 (up to T3 included), never above the maximum; then
     the workshop produces workers × output per worker.
3. The population then changes with the food left, as now.

The five figures (16, 8, 4, 1 and 1) are settings. The comparisons are Patrician
III's: a stock equal to a threshold counts as under it.

### Thresholds

`ThresholdsOf` gives the threshold rule the workshop's capacity as the daily
production: T2 = T1 + 10 days of capacity. A good the city does not produce keeps
T2 = T1.

### Start of a game

- Every city gets the same **workforce budget** (250, a setting of the economy).
- It is spread evenly over the goods the city produces: each workshop starts with
  the budget divided by their number, rounded up, then cut to its maximum. A city
  that produces nothing has no workers.
- The city's **population** at the start is the budget times the **inhabitants
  per worker** (72 ÷ 17), whatever the city produces: about 1 059 in every city.
- Stocks start, as now, at 7 days of consumption (T0).

### Consequences

- Every city starts at the same size; they part with the food they produce and
  the trade that reaches them.
- A city that produces many goods starts under the maximum of its workshops; its
  stocks start at T0, so it hires 8 workers a day up to it.
- A producer left alone settles between T2 and T3: it drops to one worker above
  T3, its stock falls under T3 by consumption, and it hires again.
- Capacity is 1.5 times the starting consumption: populations can grow about
  eight months at the highest rate before the world lacks capacity.

## Data

Output per worker, for an efficient production, from a world of 7 cities of
1 058.8 inhabitants (250 × 72 ÷ 17) and a margin of 1.5; the producers are those
of the current map (efficient + 0.75 × inefficient):

| Good | Weekly per 1 000 | World per day | Producers | Max workers | Output per worker |
|---|---|---|---|---|---|
| Fish | 10 | 10.588 | 1 + 4 × 0.75 = 4 | 65 | 0.0611 |
| Beer | 50 | 52.941 | 2 + 2 × 0.75 = 3.5 | 68 | 0.3337 |
| Grain | 40 | 42.353 | 2 + 2 × 0.75 = 3.5 | 68 | 0.2669 |
| Iron | 25 | 26.471 | 2 + 2 × 0.75 = 3.5 | 78 | 0.1455 |
| Meat | 8 | 8.471 | 1 + 4 × 0.75 = 4 | 60 | 0.0529 |
| Vegetables | 30 | 31.765 | 2 + 3 × 0.75 = 4.25 | 60 | 0.1869 |

Output per worker = 1.5 × world per day ÷ producers ÷ max workers. The maxima are
Patrician III's (fish 65, beer 68, grain 68, metal 78 for iron, meat 60); vegetables
have no equivalent and take 60. The values are a starting point, to be balanced by
playing.

## Code

### Core

- `MarketGood`: `productionRate` and `ceilingDays` give way to `maxWorkers` (int)
  and `dailyOutputPerWorker` (barrels, efficiency included). `IsProduced` is true
  when both are above 0; `DailyCapacity` is their product.
- `WorkforceRule` (new): the four hiring steps, the workers above T3 and the
  starting budget, validated in its constructor (none negative);
  `int NextWorkers(int workers, int maxWorkers, double stock, StockThresholds
  thresholds)` and `int StartingWorkersOf(int maxWorkers, int producedGoods)`
  (the budget split and cut).
- `CityMarket`: takes a `WorkforceRule`; holds the workers of every good
  (`WorkersOf(good)`), set at the start by the rule; `StartDay` consumes, then
  regulates and produces as above; `ThresholdsOf` passes the capacity;
  `NominalWeeklyProductionOf` is the production of a week at the workers of the
  moment. The ceilings go.

### Game

- `GoodDefinition`: `maxWorkers` and `dailyOutputPerWorker` (efficient production).
- `CityDefinition`: `population` goes.
- `EconomyDefinition`: the two production rates and the two ceilings go; in their
  place the efficiency of an inefficient production (0.75), the four hiring steps,
  the workers above T3, the workforce budget and the inhabitants per worker.
  `CreateWorkforceRule` builds the rule; `CreateMarket` gives each good its maximum
  and its output (times the efficiency for an inefficient city, none for a city
  that does not produce it) and the population of the budget.
  `TryValidatePrices` becomes `TryValidateSettings` and also checks the workforce
  settings, in the Inspector warning and in `WorldEconomy`'s error.
- `WorldEconomy`: no longer warns of a city without inhabitants (they all have the
  budget's); warns of a city that produces nothing.
- `CityInfoPanelController`: a city without a market shows "Population : —"
  instead of a starting population.
- `DebugCitiesTable`: a fifth column per good, "Ouv.", the workers over the maximum
  ("45/60"), or "—" for a good the city does not produce; the total row sums the
  workers and the maxima.
- The six good assets get their two production settings (table above);
  `Economy.asset` its new settings; the seven city assets lose their population.

### Editor

- `WorldMapSetup`: sample goods carry their maximum of workers and output per
  worker; sample cities no longer carry a population; a city of the map is given
  the sample productions when it produces nothing, instead of when it has no
  population.

### Unchanged

Prices (only T2 moves, by the capacity), `Trade`, the AI merchants, food growth.

### Documentation

`CLAUDE.md`: the *Economy* and *Population* sections describe workshops,
regulation and the start of a game in place of production rates, ceilings and the
110 % balance; the *Debug tools* section the workers column.

## Testing

EditMode tests, in `Assets/Tests/EditMode`:

- `WorkforceRuleTests`: each step (at T0, above T0, above T1, above T2, at T3), the
  maximum, the fall above T3, a maximum under the fall; the starting split (even,
  rounded up, cut to the maximum, none without produced goods); rejection of
  negative settings.
- `CityMarketTests`: consumption before production; production at the workers of
  the day; no production above T3; workers that start from the budget; thresholds
  from the capacity; a producer left alone stays between T2 and T3 over many days;
  the week log with the new order. Tests of the ceilings are replaced.
- `EconomyDefinitionTests`: the new defaults; inefficient output at 0.75; the
  population from the budget; validation of the workforce settings.
- `DebugCitiesTableTests`: the workers column and its total.
- `EconomyContentTests`: every good has a maximum and an output above 0.
- The existing tests that build `MarketGood`s or cities with a population are
  adapted; expected figures are worked out again from the new model.

Then the whole EditMode suite, and a game run in the Editor to look at producers'
stocks, workers and the AI merchants.
