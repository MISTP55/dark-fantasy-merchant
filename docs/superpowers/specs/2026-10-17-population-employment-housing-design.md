# Population, Employment and Housing — Design

Date: 2026-10-17
Status: reviewed, awaiting the user's approval

## Purpose

Replace how the population of a city changes by the model of Patrician III, as
read from its machine code (`patrician3-re\resultats\Population des villes.md`,
and the hiring and layoffs of `Production et consommation des villes.md`).
Patrician III has no growth rate: beggars come to a city by themselves, become
inhabitants only when a workshop hires them and a house has room for them, and
laid-off workers wait in a reserve of their workshop. The population therefore
follows the jobs and the houses of a city.

This spec takes only that link between population, employment and housing. The
player gets a lever on it: building workshops (jobs) and houses (room) in a
city. Social classes and satisfaction, which in Patrician III decide how many
inhabitants climb to the wealthy classes and when the poor leave, are later
specs.

## Scope

In scope:

- The population of a city in two parts: housed inhabitants and beggars.
- Beggars that come and go by themselves, by Patrician III's formulas.
- The workshops of the city hiring among the beggars, within the room left in
  the houses, and laying off into a reserve of their workshop.
- Houses: the city's, enough for its population at the start, and the player's.
- Workshops of the player: built for a good the city produces, several per good,
  30 jobs each, filled from the beggars. They produce nothing yet.
- Building a workshop or a house from the city panel, paid in gold, timber and
  metal, the materials taken from the hold of the player's ship in port, then
  bought from the city's market.
- The city panel's new section, the debug tool's new columns.
- Removing the growth of the population by food.

Out of scope:

- Social classes (rich, wealthy, poor) and their satisfaction; Patrician III's
  satisfaction is taken as fixed at its starting value, 50, where a formula
  needs it.
- The departure of inhabitants: in Patrician III the poor leave when they are
  unhappy or have no house; neither can happen here (houses are never lost),
  so housed inhabitants never leave, except through the reserve's leak (below).
- The city building houses or public buildings by itself (Patrician III does).
- Production of the player's workshops, their wages, their upkeep; the rent of
  the player's houses; selling or demolishing buildings.
- A construction time: building is immediate.
- Patrician III's population ceiling, which only applies to towns founded during
  a game, and its one-off waves of beggars.
- Saving and loading (there is none yet).

## Decisions

Taken with the user:

- **Food no longer changes the population.** It grows only through hiring.
  `FoodGrowth`, its four settings and the debug column of the monthly rate go.
- **The player builds workshops and houses in a city, from its panel.** Several
  workshops per good, for any good the city produces (efficiently or not); a
  workshop costs 10 000 gold, 10 bushels of timber and 30 barrels of metal, and
  has 30 jobs; a house costs 5 000 gold, 10 bushels of timber and 10 barrels of
  metal, and houses 100 inhabitants.
- **Building is immediate.**
- **Materials** come from the hold of the player's ship in port first, then the
  rest is bought from the city's market at its price. When the hold and the
  market together cannot supply them, building is impossible.
- **Every city starts with enough houses for its population.**
- **Workers come from the beggars** through Patrician III's beggar system,
  4 beggars becoming 4 housed inhabitants per worker hired, and only while
  houses have room. This also applies to the city's own workshops, whose workers
  are no longer free.
- **Patrician III's reserve** for laid-off workers.
- **The panel's layout B** (below): a line per possible building, with what the
  player owns.
- **Room is shared**: the player's houses are room for the whole city, as a
  merchant's house is let to anyone in Patrician III. A house the player builds
  may first go to the city's workshops, which hire before the player's.
- **Building does not need a ship**: without a ship of the player in port, every
  material is bought from the city's market, from wherever the panel is opened.

My calls, presented to the user and not objected to:

- Consumption follows the whole population, beggars included, as now.
- The player's workshops are filled after the city's workshops, as Patrician III
  fills its vacancies after the day's production.
- A building is refused beforehand when gold or materials are lacking: nothing is
  taken.
- The build buttons are disabled when building is impossible; every building
  posts a notification.
- In the debug tool, the monthly rate is replaced by the beggars and the houses.

My calls, after the review of this spec:

- **No ceiling of 30 workers on hiring.** Patrician III serves the three hiring
  sources only up to 30 workers per workshop (60 for grain) and queues the rest
  in a list whose processing was not read (`0x4D4DA0`). Without that queue, the
  ceiling would stop the city's workshops above 30 from ever hiring; the queue
  presumably hires them in the end. The city's workshops therefore hire up to
  their maximum, as now.
- **The total cost is shown on the button**: "Construire (12 340 or)", the
  building's gold plus what the materials the hold lacks would cost on the
  market at that moment; it follows the market like the prices.
- **A city with no active workshop** starts with no worker, no housed
  inhabitant, no house and no beggar. Its beggars' target is 8, but none comes
  (⌈√0 ÷ 5⌉ = 0): it stays empty. The warning
  of `WorldEconomy` stays, reworded.

## Model

### Population

A city's population is two whole numbers:

- **Housed inhabitants**: always 4 per worker of the city, counting the workers
  of the city's workshops, those in their reserves and those of the player's
  workshops. They are not a separate count: hiring and leaving change workers,
  and the housed inhabitants follow (4 is a setting, `inhabitantsPerWorker`).
- **Beggars**: a count of the city. They need no house.

The **population** is their sum; it is what consumes (consumption per 1 000
inhabitants, as now) and what the beggar formulas read. It is whole: the
fractional `CityPopulation.Count` and its daily compounding go.

### Houses

- A house has room for 100 housed inhabitants (a setting of the house).
- A city has its own houses and the player's houses in it; **room** is
  100 × their number. **Free room** is room less the housed inhabitants.
- At the start a city has as many houses as its housed inhabitants need,
  rounded up: ⌈housed ÷ 100⌉. The player owns none.
- Houses are never lost, and housed inhabitants never exceed room: hiring checks
  the free room.

### Start of a game

Patrician III's start, as now, with the population split:

- The workers are spread over the city's workshops as now
  (`WorkforceRule.StartingWorkersOf`); W is their sum in the city.
- Housed inhabitants = 4 × W.
- Beggars = 4 × W ÷ 17, rounded down (Patrician III's `0x546050`).
- The city's houses = ⌈4 × W ÷ 100⌉.

The starting population therefore follows the workers actually placed rather
than the budget (a city whose workshops round up to 252 workers starts with
1 008 housed inhabitants and 59 beggars). `inhabitantsPerWorker` becomes 4 and a
new setting gives the starting beggars per worker (4 ÷ 17).

### A day of a city

In this order (`CityMarket.StartDay`):

1. **Consumption** of every good by the population, as now.
2. **City workshops**, good by good in the order of the catalogue, as now: the
   workshop's target for the day is `WorkforceRule.NextWorkers`, unchanged.
   - Above its present workers, it **hires** the difference (see Hiring); it may
     get fewer.
   - Under them, it **lays off** the difference into its reserve (see Reserve).
   - It produces with the workers it then has, as now.
3. **Player's workshops**: their vacancies are filled from the beggars (see
   Vacancies).
4. **Beggars** come or go (see Beggars).

The market raises `Changed` once, at the end, as now.

### Hiring by a city workshop

Patrician III's order (`0x510C30`), for a workshop that wants h more workers:

1. Its own **reserve** first: up to h workers come back from it. Housed
   inhabitants do not change.
2. Then the **beggars**: up to ⌊beggars ÷ 4⌋ workers and up to ⌊free room ÷ 4⌋
   workers. Each worker hired takes 4 beggars, who become 4 housed inhabitants.
   The room check is ours (Patrician III hires without it and lets the poor
   without a house leave afterwards): the user's rule is that without room there
   is no hiring.
3. Then the **reserves of the other city workshops**, in the order of the
   catalogue: workers move from one workshop to another; housed inhabitants do
   not change.

What is still missing is not hired that day; the workshop tries again the next
day with its new target. (Patrician III queues it; we do not.)

### Reserve

Every city workshop has a **reserve** of laid-off workers, as in Patrician III
(`0x510D00`). They are still housed inhabitants and still consume.

- A layoff moves the workers from the workshop to its reserve, with **no
  ceiling**. As now, a workshop above T3 keeps one worker (`workersAboveT3`).
- **Leak**: every layoff adds 2 % of the reserve (after the layoff) to a
  fractional counter of the workshop; its whole part k is taken from the counter
  and is how many reserve workers leave for the beggars, each giving back
  4 housed inhabitants as 4 beggars. k is cut so that workers + reserve + k
  stays at most 30, and nothing leaves when workers + reserve is 30 or more.
  This is Patrician III's rule as read: only workshops under 30 leak. As in its
  code, when workers leave the counter loses k and then again the number that
  leaves, so it can fall under 0; a negative whole part counts as 0. Both
  figures (0.02 and 30) are settings.
- The reserve is drawn on by its own workshop first, then by the others (Hiring,
  step 3).

### Vacancies of the player's workshops

Patrician III's second hiring (`0x51CF30`), after the city's workshops:

- Only when the city has at least 5 beggars and more than 3 free room.
- Up to ⌊min(beggars − 1, free room) ÷ 4⌋ workers are hired that day, given to
  the player's workshops of the city in the order they were built, each up to its
  30 jobs. Each takes 4 beggars, who become 4 housed inhabitants.
- The player's workshops never lay off and have no reserve.

### Beggars

Patrician III's `0x51C430`, with P the population at that step and S the beggars'
satisfaction, fixed at 50:

- **Target** = √(P × S ÷ 2) ÷ 3 + 8, rounded down: about 1.67 × √P + 8, 62 for a
  starting city.
- Under the target, ⌈√P ÷ 5⌉ beggars **come** (7 for a starting city), never
  above the target.
- Above it, ⌈√P ÷ 10⌉ **go**, never under the target.

S, the divisors 5 and 10, the 3 and the 8 are settings (Patrician III's
figures). Beggars are the only source of new inhabitants.

### Building

A building type is a definition: its gold, its materials (a list of good and
units) and what it gives (100 room for a house, 30 jobs for a workshop).

- **Workshop**: for a good the city produces, efficiently or not, and active.
  Any number per good and per city.
- **House**: any number per city.

Paying, all or nothing:

1. The materials are taken from the hold of the **ship selected in the city
   panel's list**, when it is a ship of the player in port there (the same ship
   the market trades with), up to what it carries of each.
2. What is left of each material is **bought from the city's market**, at the
   market's buy price unit by unit as a trade prices it (the integral of the
   curve), without passing through a hold: the units go into the building.
3. The building's gold plus the cost of that purchase is withdrawn from the
   treasury (`TryWithdraw`: a treasury in debt or short of gold builds nothing).

Building is possible only when the hold and the market's whole units together
cover every material and the treasury covers the total; otherwise nothing is
taken. The building exists at once: a house adds its room, a workshop its jobs,
filled from the next day.

A notification is posted: "Atelier construit à Lübeck : céréales." or "Logement
construit à Lübeck." (no article, nothing that agrees or elides, as the other
notifications).

### Consequences (expected, to be checked by playing)

- A starting city has about 62 beggars as its target and up to 99 free room
  (the rounding of its houses): its own workshops, which start under their
  maximum, take the beggars and the room within days, then **the city stops
  growing**. Its workers then only move between its workshops through the
  reserves.
- A player's workshop needs 30 workers, 120 beggars and 120 room: about 1.2 houses
  per workshop, and at 7 beggars a day, about 17 days to fill in a starting city
  whose own workshops want nothing more.
- A city grows only where the player builds both jobs and houses. Growth is
  faster in a bigger city (√P).
- Population never falls, except by the reserve's leak in small workshops and
  by beggars leaving when they are above their target.

## Data

- `EconomyDefinition`: `inhabitantsPerWorker` becomes 4; new settings for the
  starting beggars per worker (4 ÷ 17), the beggars (S 50, divisors 5 and 10,
  3 and 8), the vacancies (5 beggars, 3 room), the reserve (0.02, 30). The food
  settings (`stableFoodDays`, `abundantFoodDays`, `maxMonthlyGrowth`,
  `maxMonthlyDecline`) go. Every new setting is checked by `TryValidateSettings`
  like the others.
- The two building types and their costs are content: in an asset (a new
  definition in `Assets/Data/Economy`, or settings of `EconomyDefinition`; left
  to the builder), with the user's figures. A material that is not an active
  good makes the building unavailable, with a warning.
- `GoodCategory` stays on the goods (satisfaction will use it); nothing reads it
  in this spec.

## Interface

### City panel (layout B)

Under the population and before the market, as in the mockup the user chose:

- The population line: "Habitants : 1 008 · Mendiants : 59" (housed
  inhabitants, beggars), rewritten on the market's `Changed`.
- A section "Vos bâtiments" with:
  - a line for the houses: "Logements : 2 (ville : 1 008 / 1 300 places)", the
    player's houses, then the housed inhabitants over the room of the city
    (city's and player's houses together), and a button "Construire (5 000 or)"
    with the total cost;
  - a line per good the city produces, in the order of the active goods:
    "Céréales : 2 ateliers (45 / 60 ouvriers)", or "Bière : 0 atelier" with no
    figures, and a button "Construire (…)" with the total cost;
  - one line with the two costs: "Logement : 5 000 or, 10 bois, 10 métal ·
    Atelier : 10 000 or, 10 bois, 30 métal" (written from the definitions).
- A button is disabled while its building is impossible, and re-evaluated on the
  market's, the selected ship's hold's and the treasury's `Changed`, like the
  market's prices.

Numbers are written the French way, as elsewhere.

### Debug tool, cities tab

- "Croiss. / mois" is replaced by "Mendiants" (beggars, and the target in
  brackets: "59 (62)") and "Logements" (housed inhabitants over room). "Croiss.
  totale" stays, on the whole population.
- The workers column of a good shows the reserve when there is one:
  "45/60 +10". The player's workshops are not in this tab.
- The total row sums beggars, housed inhabitants and room.

## Tests

Core logic in EditMode tests, as the project does: the start (housed, beggars,
houses), the order of a day, each hiring source and its limits (room, beggars ÷ 4),
the reserve and its leak, the vacancies and their conditions, the beggars'
target, coming and going, and building (hold then market, all or nothing, gold,
inactive material). The existing tests of food growth go with it; those of the
markets that relied on a fixed population keep it by having no hiring.

## Documentation

`CLAUDE.md`: the Population section is rewritten, the Economy section's start of
a game and workshops are updated (workers are no longer free), and the city panel
and debug tool descriptions follow.

## Left to the builder

- Where the state lives in `Core` (in `CityMarket` or in new classes beside it)
  and the class names.
- Where the building definitions live (above).
- How the panel's section is built in UXML/USS, within the conventions.
