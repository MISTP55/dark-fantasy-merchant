# Workshop inputs — design

## Intent

Today a city's workshop makes its good out of nothing: workers × output per worker, every day its stock is not above T3. Raw and building materials (timber, salt, pig iron, wool, hemp…) are consumed by nobody but the inhabitants and pile up in large surplus (the economy rule says so: "expected until production chains are added").

This work adds the production chains: a workshop transforms **inputs** into its good. A brewery needs grain and timber; a smithy needs timber and pig iron. When an input is short, the workshop produces **in proportion to what it has** — it does not stop dead. Inputs become demand of the city like the inhabitants' consumption, so a brewing city pays more for grain, starts with a stock of it and draws the AI merchants that carry it. Raw materials become goods worth carrying.

## Goals

- Every good can have a **recipe**: a list of inputs, each an input good and the units of it consumed per unit of the good produced.
- A city's workshop consumes its inputs from the city's stock as it produces. When an input lacks, the day's production is brought down to what the scarcest input allows, and only the matching inputs are consumed.
- A city's **demand** for a good is what its inhabitants consume **plus** what its workshops need of it as input. That total is what the thresholds (so the prices), the starting stocks and the AI merchants' planning read.
- The debug tools show the inputs a city's workshops need and consume.

## Non-goals

- The player's workshops. They produce nothing yet (that needs the trading office, the "comptoir"), so they need no inputs. Their recipe will be the good's, when they produce.
- Anything the player sees outside the debug tools: no input shown in the city panel or the market tab. The user decided the debug tools are enough for now.
- Rebalancing: no change to output rates, consumption rates, max workers, base prices or the cities' productions. The new shortages and surpluses are to be observed through the debug tools first.
- Workshops with several products. Each workshop makes one good, as today (leather and oil have workshops of their own).
- Seasons, harvests, events.

## Anti-goals

- A workshop that stops completely the moment one input is a hair short. Production is proportional.
- A city whose demand for an input falls during a shortage of it, so that it pays *less* for what it lacks. The input demand is what the workshops would consume with their workers of the moment, not what they actually managed to consume.
- Recipes hard-coded in C#. They are data, in the good assets.

## Constraints

- Project conventions (root `CLAUDE.md`): definitions in ScriptableObjects, runtime state in Core; simulation logic in plain C# covered by EditMode tests; player- and debug-facing text in French; lists shown sorted with `NameOrder`; good names never followed by their unit; striped lists; `WorldMapSetup.Build` creates and wires whatever is new; no mention of the game this one is inspired by, anywhere in the repository.
- In Core, a good is its index among the **active** goods. An input whose good is not active does not exist in the game: it is left out of the recipe, the same way a switched-off building material is left out of a construction (the workshop then needs only its other inputs, or nothing).
- The order of a day (`population.md`) stays: inhabitants consume every good first, then the city's workshops in the catalogue's order.
- `CityMarket.LastDay` must only copy values the day already computes.

## The recipes (default content)

Units per unit of the good produced, in each good's own unit (a bushel is 10 barrels; these are the same proportions as the reference values once converted to our units):

| Good produced (unit) | Inputs per unit produced |
|---|---|
| Bière (barrel) | Céréales 0.025 bushel, Bois 0.01 bushel |
| Métal (barrel) | Bois 0.1 bushel, Fonte brute 0.1 bushel |
| Étoffe (barrel) | Laine 0.1 bushel |
| Sel (barrel) | Bois 0.08 bushel |
| Poterie (barrel) | Bois 0.05 bushel |
| Tuiles (bushel) | Bois 0.5 bushel |
| Brai (barrel) | Bois 0.1 bushel |
| Peaux (barrel) | Métal 0.05 barrel, Chanvre 0.005 bushel |
| Poisson (bushel) | Sel 2 barrels, Chanvre 0.1 bushel |
| Viande (bushel) | Sel 2.5 barrels |
| Cuir (barrel) | Sel 0.1 barrel, Bois 0.02 bushel |
| Céréales, Bois, Miel, Laine, Fonte brute, Vin, Chanvre, Huile, Épices | none |

With today's catalogue, Sel, Fonte brute and Chanvre are not active goods, so they are left out of these recipes in the game: Poisson and Viande need nothing, Métal and Cuir only Bois, Peaux only Métal. Their recipes are still written in full, so that activating those goods brings their inputs back with no other change.

Derivation, for the record: the reference ratios are in a common small unit where a barrel is 200 and a bushel 2 000; a ratio r of input per product becomes r × size(product unit) ÷ size(input unit). The reference makes leather as a by-product of the meat workshop and puts its share of salt (÷10) and timber (÷5) on it; here leather has its own workshop, so that share is its own recipe (the user's call to keep the reference values; the split is Claude's call).

## The design

### Definition

- `GoodDefinition` gets a list of inputs: each an input `GoodDefinition` and its units per unit produced (above 0). The recipe lives on the good because the city's workshop of a good is defined there (max workers, output per worker).
- `EconomyDefinition` turns it into Core's form when it builds the `MarketGood`s: the inputs whose good is active, by their index among the active goods.
- Validation (`EconomyDefinition.TryValidateSettings`, which already leaves every city without a market on a broken setting): an input with no good, a good that is its own input, a good listed twice in one recipe, an input good absent from the catalogue, or a quantity that is not above 0 and finite, is an error. A cycle between recipes is allowed (nothing breaks: each workshop reads the stock as it stands).
- The default recipes above are written into the good assets, and `WorldMapSetup` gives them to the goods it creates.

### The day's production

In `CityMarket.StartDay`, for each produced good in the catalogue's order, after hiring/laying off:

1. `potential = workers × output per worker`.
2. For each input: `needed = potential × units per unit`. The workshop's factor is the smallest `stock ÷ needed` over its inputs, capped at 1 (1 with no input).
3. Each input's stock falls by `needed × factor`.
4. When the workshop produces (its good's stock not above T3, as today), `potential × factor` enters the stock and counts as produced.

Stocks are fractional, so a factor below 1 is exact. A workshop reads the stocks as they stand when its turn comes: a good produced earlier in the catalogue's order the same day is already there.

When its good's stock is above T3, the workshop still works its day and **consumes its inputs** (steps 1 to 3), but nothing enters the stock and nothing counts as produced: those inputs are lost. The user chose this, knowing it makes a saturated city a sink for raw materials.

Hiring is unchanged: a workshop short of inputs keeps hiring and laying off by its own good's stock against its thresholds. An idle workshop with full staff is possible; it is what makes the shortage visible.

### Demand

- `CityMarket` gets the daily **input need** of a good: over every produced good, its workers of the moment × output per worker × units of this good per unit. It is what the workshops would consume with no shortage, above T3 or not.
- `DailyConsumptionOf` keeps its meaning (the inhabitants' consumption, which `StartDay` takes first and the satisfaction checks). A new **demand** = consumption + input need is what these read instead of the consumption:
  - the thresholds (`UpdateThresholds`: T0 and T1 in days of demand; T2 keeps the production capacity);
  - the starting stock (`startingDays` × demand, with the starting workers, which the constructor sets before the stocks);
  - the AI merchants' `SupplyPlanner` (net = production − demand). The production it reads stays the nominal one (`DailyProductionOf`, workers × output), which ignores a shortage of inputs (Claude's call: the planner projects what the city would do with its workers; a projection that follows today's input stocks can come later if the AI is seen to undersupply the goods of broken chains).
- The satisfaction compares a good's stock to its T0, which now includes the input need: a brewing city finds it harder to have "enough" grain for its inhabitants. This is a side effect the user was told of and accepted with the demand. Which goods count in it (`DailyConsumptionOf > 0`) does not change.
- A shortage ("Manque", `MissingOf`) stays what the inhabitants needed and did not get.

### Records and debug tools

- `MarketWeekLog` records, per good, the inputs consumed of it, apart from the inhabitants' consumption; `CityDayReport` (`LastDay`) records the day's input need and the inputs consumed, copied from what the day computed.
- Two new `GoodIndicator`s, in French in the tools, shown wherever the per-good indicators are (the tables of "Villes", the indicator lists of "Courbes", "Composer"):
  - the input need in a week with the workers of the day (nominal), beside the nominal consumption;
  - the inputs actually consumed over the last week (recent), beside the recent consumption.
- The "Net" columns and their totals in the tables of "Villes" (`DebugCitiesTable`, `DebugGoodMarketTable`) take the inputs off too: nominal net = nominal production − (nominal consumption + nominal input need), recent net = recent production − (recent consumption + recent inputs consumed) (Claude's call: otherwise a brewing city would show a surplus of grain it lacks).
- Production reduced by a shortage needs no indicator of its own: it shows as recent production below nominal production, which the tools already have.

### Documentation

- `economy.md`: the "Raw materials" bullet becomes the inputs' rule (where recipes live, proportional production, demand = consumption + input need); `population.md`'s order of a day mentions the inputs at step 2; `debug-tools.md` the new indicators.

## Testing

EditMode tests, by the existing patterns (`CityMarket…Tests`, test helpers in `Common`):
- a workshop with all its inputs produces as before and consumes `produced × units` of each;
- a short input brings production down to its proportion and consumes the others in the same proportion; the scarcest of two inputs decides; an input at 0 stops the workshop;
- above T3, nothing is produced but the inputs are consumed, in proportion to what the scarcest allows;
- the input need follows the workers; the thresholds and the starting stock use consumption + input need;
- the satisfaction's goods and `MissingOf` are unchanged by inputs;
- an inactive input is left out; the validation's errors;
- `LastDay` and the week log record the inputs.
- A content test that the default recipes are those of the table.

## Left to the builder

The names of the new types and members, the Core shape of a recipe, where the French labels of the indicators go, the column order in the debug tables (beside their consumption counterparts).
