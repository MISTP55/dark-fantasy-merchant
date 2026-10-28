# Player's workshops — design

## Intent

The player can already build workshops in the city view: they hire beggars while connected to the road network, and `CityMarket.PlayerDailyOutputOf` knows what they would make, but they make nothing, take nothing and cost nothing. This work makes them **run**: they produce into the player's trading post of their city, take their inputs from it, and cost the player a weekly running cost and wages. The trading post becomes the hub of the player's business in a city, as its design announced *(the user's intent)*.

## Goals

### Where a workshop can be built

- The player can build a workshop **only in a city where they have a trading post** *(the user's decision)*. Elsewhere the build bar's workshop offers are **disabled**, still listed, with a tip saying that a trading post is needed *(builder's call: visible rather than hidden, as the trading post's offer once built, so that the player sees what is missing)*. `Construction`'s quote and build refuse it too, so that no path builds one without a post (the debug tools' free building included).
- The rest is unchanged: a city that produces the good, a place on the grid, the cost paid as today.

### What a workshop does each day

- It makes `workers × merchant output per worker × bonus` of its good, the bonus being the existing one of `MerchantOutputRule` for the number of the player's workshops of the good in the city *(the user's decision: keep the bonus)* — what `PlayerDailyOutputOf` already computes, workshops of a good together.
- It takes its **inputs from the player's trading post of its city, never from the market** *(the user's decision)*: the recipe of the good (`GoodDefinition.Inputs`, per unit made, inputs that are not active left out), as the city's workshops use it.
- **Short of an input, it produces in proportion to the scarcest one**, as the city's workshops do *(the user's decision)*: what it takes and what it makes are both scaled down to what the post holds.
- What it makes **goes into the same trading post**. Its capacity never stops it: above capacity, it only raises the rent.
- There is no regulation by stock or price: a running workshop always produces as much as its workers and inputs allow.
- Only workshops that have workers produce; a workshop not connected to the roads hires nobody (as today) and so makes nothing.
- *Builder's call:* the player's workshops produce in `CityMarket.StartDay` right after they hire (step 4 of the day), in the order they were built, so that what one makes can serve another the same day.
- *Builder's call:* the trading post counts whole units while the output is fractional (0.1 a worker a day by default). Each workshop, or each good's group of workshops, keeps the fraction it has made and delivers only whole units; the inputs it takes follow the same rule: an input unit is taken from the post only once the fraction owed reaches a whole unit, and the shortage is judged against what the post holds at that moment (no stock of inputs kept ahead). No fraction is lost nor created from day to day.
- What the player's workshops make or take is **not** the city's: it does not enter `MarketWeekLog`, the market's stocks, its demand or its thresholds.

### What a workshop costs each week

Two amounts, added to the weekly bill (`WeeklyBill`) with the sailors' wages and the trading posts' rent, and taken like them whatever the gold left (`Treasury.Withdraw`). The weekly notification keeps reading the total.

- **A running cost** per workshop, set on each workshop's asset (`WorkshopDefinition`) in the Editor, **200 gold a week by default** *(the user's decision)*. *Builder's call:* it is owed for every workshop built, whether it has workers or is connected.
- **Wages**, based on the **base price** of the good it makes *(the user's decision)*: a workshop with all its workers pays **80 %** of the base value of what it makes in a week. Example: one unit a week of a good worth 100 gold gives 80 gold of wages.
  - The 80 % is one value for the whole game, in `ExpensesDefinition`, next to the sailor's wage *(builder's call)*.
  - *Builder's call:* wages follow the **workers present on the billing day**, as the sailors' do: weekly wages of a city's workshops of a good = workers × merchant output per worker × bonus × days of a week × base price × 80 %. A half-filled workshop pays half. A workshop short of inputs pays its workers in full: workers are paid, not output.
  - *Builder's call:* the bonus counts in the wages, so the margin stays 20 % of the base value.
  - *Builder's call:* rounded to the nearest gold once per bill, after summing every workshop of every city.

## Non-goals

- Any interface for the player's workshops (workers, output, costs, shortages): it comes later *(the user's statement)*. The build bar's tip is unchanged apart from the trading-post requirement.
- A production level the player sets, layoffs, regulation by stock.
- Workshops of the AI merchants.
- Inputs taken from a ship's hold or the market; deliveries elsewhere than the city's trading post.
- Notifications about shortages.
- Demolishing or selling a workshop.

## Anti-goals

- A player's workshop taking anything from the city's market, or putting anything into it.
- A workshop built in a city without the player's trading post, by any path.
- Goods created or lost by rounding: over days, the units delivered are the units made, to the fraction kept.

## Constraints

- Follow the project's `CLAUDE.md` and rules: the production and wages are Core logic covered by EditMode tests (production with and without inputs, proportional shortage, fractions carried, bonus, wages and running cost in the bill, refusal without a post); Game only wires them. New scene references go through `WorldMapSetup`; the new asset field gets its default there for existing assets.
- Core knows a player's workshop by its good and jobs, not its definition: what it needs of the definition (the running cost) is handed to it, or summed in Game.
- No mention of the game this one draws inspiration from.
- The rules of the subsystems touched (`population.md`, `economy.md`, `player.md`, `city-view.md`) are updated: in particular the lines saying the player's workshops "produce nothing yet" and "take nothing".

## Left to the builder

- How `CityMarket` reaches the player's trading post's warehouse (e.g. an `IGoodStore` given to the market when the post opens, the post at the home city given at the start), and what happens in a market without one (no production).
- Where the fractions are kept, per workshop or per good.
- The tip text of a workshop offer without a trading post.
- Whether the debug tools show any of this.
