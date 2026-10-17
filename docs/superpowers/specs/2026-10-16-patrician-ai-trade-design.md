# AI Merchants Trade Like Patrician III — Design

Date: 2026-10-16
Status: awaiting review

## Purpose

The AI merchants today seek the most gold per day. They load a cargo unit by
unit, try every destination, and also work out the best trade from every
other port to decide whether to sail there empty. That costs on the order of
ports² × hold size × goods × 2 price computations per departure: about
800 000 at 10 cities, 7 million at 30, and it grows with the size of the holds.

The user wants the behaviour of Patrician III instead, as read in its code
(`patrician3-re\resultats\Marchands IA.md`, read in full on 2026-10-16):

- AI merchants **regulate supply** rather than maximise profit. They carry a
  good from a city above its thresholds to a city that will be under its T1
  on arrival, in a quantity worked out in one closed formula on the
  thresholds, and they rank goods by the gap between price factors, not by gold.
- Each ship has a **home city** and makes round trips between it and a
  destination, with stopovers on the way back where it sells only up to the
  city's T1.
- A decision costs at most ports × goods × 2 evaluations of a small formula,
  whatever the size of the holds.

The intention of the model is kept; three oddities of Patrician III are
corrected (decided with the user, see Decisions).

## Scope

In scope:

- A new trade decision for AI ships: quantities from the thresholds, goods
  ranked by weighted factor gap, destination chosen from the home city.
- The tour: home city → final destination → back home, with stopovers on the
  way back.
- Partial sales at stopovers, full unloads at the final destination and at home.
- Deliveries under way counted in the destination's projected stock.
- One AI ship per city, each with that city as its home (7 ships on the
  present map instead of 5).
- The debug tool: the "Ajouter un navire IA" button gives the new ship a home
  city; the ships tab shows each AI ship's home and final destination.

Out of scope:

- Offices (Kontor) and workshops owned by AI merchants, and the stock an
  office keeps for them. DFM's AI merchants own neither; this can come back
  when they do.
- Gold for the AI: it stays unlimited, only the profit is counted (Patrician
  III does not check the merchant's gold before buying either).
- Crew, repairs, armed ships and convoys of Patrician III's AI.
- Saving and loading.

## Decisions

Taken with the user:

- **The whole model**, home city and partial sales included.
- **Oddities corrected, intention kept:**
  - The score reads the source's factor on its real price curve (Patrician
    III adds where it should multiply between the middle of T0–T1 and T1,
    which doubles the slope).
  - The stopover is chosen where the ship is most useful (Patrician III picks
    it on a practically arbitrary byte).
  - No deposit into an office that teleports goods home (there is no office).
- **At least one AI merchant per city.** On the present map, 7 ships.
- **The debug button gives the new ship a home city.**

Taken by the design (the user can override them):

- The weights of the goods are a field of each good's asset, set to Patrician
  III's values.
- The stopover criterion: the candidate with the highest total score from the
  port where the ship is.
- Deliveries under way: what a ship loaded *for* a port counts toward that
  port until the ship enters it, for every ship's decision including its own,
  never more than it has aboard.
- A city is a stopover at most once per return trip.
- The debug button picks the home city with the fewest AI ships, at random
  among ties.
- Gold stays unlimited; the stopover stays one day; ships keep their names
  ("Marchand indépendant N") and their numbering.
- A ship that finds no destination waits at home and tries again at the end
  of every stopover.

## Units and figures

Everything is in the good's own unit (barrels, or bushels for the goods
counted in bushels), as stocks, thresholds and prices already are. A bushel
takes `barrelsPerBushel` (10) barrels of room in a hold (`GoodUnits`).

- **Smallest lot:** 5 units for a good counted in barrels, 3 for one counted
  in bushels (Patrician III's 5 barrels and 3 loads). A computed quantity
  under it is no transport.
- **Weights** (`GoodDefinition`, a new integer field, Patrician III's table):

| Weight | Goods |
|---|---|
| 16 | grain, salt |
| 11 | timber, metal, wool |
| 9 | meat, fish, beer, honey, spices, wine, cloth, skins, oil, leather, hemp, pottery |
| 8 | pitch, pig iron, tiles |

  The field defaults to 9 for a new good. A weight of 0 means the AI never
  carries that good. The weights reach `Core` with the other figures of a
  good (alongside `MarketGood`, or as a list given to the planner; the
  builder chooses).

## The decision for one good

For a good, a source city A (where the ship is) and a destination B, with
`travelDays` the days of the trip from A to B (`PortTravelTable`):

### Projected stock of B on arrival

`b = max(0, stock(B) + underWay(B) + (dailyProduction(B) − dailyConsumption(B)) × travelDays)`

- `underWay(B)`: units other AI ships loaded for B and have not delivered yet
  (see Deliveries under way).
- `dailyProduction(B)`: what B's workshop makes in a day with the workers of
  the moment (`NominalWeeklyProductionOf / daysPerWeek`); 0 for a good B does
  not produce.
- `dailyConsumption(B)`: `DailyConsumptionOf`.

### Quantity

With A's thresholds `A.T0, A.T1`, `midA = (A.T0 + A.T1) / 2`, `a = stock(A)`,
and B's thresholds `B.T0, B.T1`:

1. If `a ≤ midA`: nothing. A never gives below the middle of its T0 and T1.
2. If `b ≥ B.T1`: nothing. B is never filled beyond T1.
3. `need = B.T1 − b`. If A's surplus above its T1, `a − A.T1`, covers `need`,
   the quantity is `need`.
4. Otherwise:
   - A first gives its surplus above T1 (if any): `q1 = max(0, a − A.T1)`.
   - While B is under a target point `targetB = B.T0 + 3/8 × (B.T1 − B.T0)`,
     A gives up to that point, without going below `midA`:
     `q2 = max(0, min(targetB − (b + q1), (a − q1) − midA))`.
   - Beyond that, the rest is shared so that A, between `midA` and its T1,
     and B, between `targetB` and its T1, end at the same relative position.
     With `a' = a − q1 − q2`, `b' = b + q1 + q2`, `ra = A.T1 − midA`,
     `rb = B.T1 − targetB`, and only when `b' ≥ targetB` and `a' > midA`:
     `q3 = max(0, ((a' − midA) × rb − (b' − targetB) × ra) / (ra + rb))`.
   - The quantity is `q1 + q2 + q3`.
5. Rounded down to whole units. Under the smallest lot: nothing.

Degenerate thresholds (two equal thresholds, a zero range) must not divide by
zero: a zero range gives nothing to share at that step.

### Score

`score = (sellFactor(B, b) − buyFactor(A, a)) × weight`

- `sellFactor(B, b)`: the factor at which B pays for the good, read on the
  market's sell curve (`MarketPricing.SellCurve.FactorAt`) at the projected
  stock `b`.
- `buyFactor(A, a)`: the factor at which A sells the good, read on its buy
  curve (`MarketPricing.BuyCurve.FactorAt`) at its stock `a`.
- One point each, no integral. Neither the base price nor the quantity enters
  the score: a bushel of wool weighs the same as a barrel of salt at the same
  gap of factors. That is the point: transports are spread over all goods,
  cheap ones included.
- A score of 0 or below is no transport, even with a quantity.

`CityMarket` needs to expose these two factors (its `pricing` is private
today); the builder chooses how.

A good with a weight of 0 is never transported.

## The tour

Every AI ship has a **home city** and a **final destination**, and goes to a
**next port** (the stopover it is sailing to, or the final destination).

### Choosing the final destination (at home)

For every city C the ship can reach from home (reachable in the travel table,
not home itself):

- For each good, the score from home to C when its quantity is a transport;
  otherwise the score from C back to home when that quantity is a transport
  (what the ship would bring back); otherwise 0.
- The total over the goods is C's value.

The city with the highest value above 0 becomes the final destination. The
trip's length does not divide it: a far city is worth a near one at the same
total. With no city above 0, the ship waits another stopover at home and
tries again.

### Choosing a stopover (on the way back only)

When a ship is in a port other than its home, after its sale (see below), it
looks for a stopover on its way home. A candidate is a city C that is
reachable, is neither the port the ship is in nor home, has not already been a
stopover of this return trip (nor its final destination), and whose detour is
short:

`travelDays(port, C) + travelDays(C, home) < 1.2 × travelDays(port, home)`

Each candidate is valued as a destination is, from the port the ship is in:
the total over the goods of the score from here to C when its quantity is a
transport (no return part). The candidate with the highest value above 0
becomes the next port; on a tie, the one with the shortest detour. With none,
the next port is home.

A ship can therefore chain stopovers on its way home: each one looks for
another between itself and home. Since each detour is shorter than 1.2 times
the direct trip from where the ship is, it gets no farther from home than
that allows; since a city is a stopover at most once per return trip, the
chain ends. Without that rule, two close cities far from home could pass the
detour rule in both directions and keep a ship shuttling between them (the
choice is deterministic, unlike Patrician III's). The ship remembers the
cities of its return trip and forgets them when it enters home.

The ship never stops on its way out: from home it sails straight to its
final destination.

### In port

**On arrival:**

1. **At its final destination** (home or not): it unloads everything — every
   unit aboard is sold to the city, whatever the stock.
   - If that port is home, it then chooses a new final destination.
   - Otherwise its final destination becomes home.
2. **At a stopover on the way back**: it sells only what brings the city to
   its T1, and keeps what its home lacks. For each good:
   - `keep = max(0, home.T1 − stock(home))`, rounded up to whole units;
   - `sellable = aboard − keep`;
   - it sells `min(sellable, max(0, T1 − stock))` of the city, rounded down to
     whole units.
3. Away from home, it chooses a stopover (above).
4. Its stopover of one day starts (`stopoverDays`, unchanged).

Sales go through `Trade.Sell` without a treasury, unit by unit at the
integrated price, as today; the profit adds what they came to.

**When the stopover is over:**

1. It loads for its final destination: for each good, the quantity and score
   from this port to the final destination; goods in decreasing score, each
   limited by the room left (a bushel takes 10 barrels) and by
   `AvailableOf`, in whole units. Purchases go through `Trade.Buy` without a
   treasury; the profit subtracts what they came to.
2. If the next port is a stopover and more than a quarter of the hold is still
   free, it also loads for that stopover the same way. If a quarter or less is
   free, it drops the stopover: the next port becomes the final destination.
3. It sails to the next port (`Docking.OrderToPort`).

If the ship is at home with no final destination, it waits another stopover.

The smallest lot applies to the computed quantity; once a good is chosen, any
whole unit that fits is bought.

### Deliveries under way

What a ship loads for a port (its final destination, or its stopover) is
recorded as under way to that port, by ship and by good. A port's `underWay`
is the sum of these records over **every** AI ship, the deciding ship's own
included. It is not what the ship actually sells there: it is what must not be
brought again.

- Counting the deciding ship's own records is what keeps it from buying the
  same lack twice: on its way home, it loads for home at the final destination
  and again at every stopover, and what it already carries for home must lower
  home's projected stock. (Patrician III counts the whole cargo of ships at sea
  toward their next port only, so a ship reloading at a stopover ignores what
  it carries for home; this is corrected.)
- A ship's record for a port and a good never exceeds what it has aboard of
  that good: when it sells units at a stopover, its records are cut down to
  what is left aboard.
- A ship's records for a port are cleared when it enters that port, and all
  of them when it is removed from the fleet.

This replaces today's `incoming`, which counts every unit aboard a ship toward
its next port. The ports "claimed" by ships sailing empty go away with the
empty relocation.

### Other cases

- A ship found idle at sea is ordered back to the port it last lay in, as
  today.
- A port with no market, or a ship in such a port, behaves as today: it waits.
- The travel table is the same as today; it is only used to project the
  destination's stock, to exclude unreachable cities and to measure detours.

### What goes away

- The unit-by-unit loading on margins and the profit per day.
- The empty relocation (`TryPlanRelocation`) and the claimed ports.
- `TradePlan`'s profit and profit per day, if nothing else needs them.

## Ships and home cities

`AiMerchantsDefinition`:

- `shipCount` (5) is replaced by `shipsPerCity` (1, at least 0): the number of
  AI ships whose home is each city that reaches another by water. On the
  present map, 7 cities give 7 ships.
- The asset `Assets/Data/AI/AiMerchants.asset` is updated; `WorldMapSetup`'s
  default becomes 1.

`AiMerchants` at start:

- For every candidate city (one that reaches another, as today), it spawns
  `shipsPerCity` ships in its port, with that city as their home. The random
  draw of start ports (`StartPortDraw`) is no longer needed for the start.
- Names keep their numbering: "Marchand indépendant 1" to "7", in the order
  of the cities.

`AiMerchantFleet.Add` takes the home city with the ship (the start port is the
home).

## Debug tools

- **"Ajouter un navire IA"** spawns a ship in the port of a home city, chosen
  as the candidate city with the fewest AI ships whose home it is, at random
  among ties. The ship's home is that city. Numbering as today.
- **Ships tab** (`DebugShipsTable`): two new columns for AI ships, "Attache"
  (home city) and "Destination finale" (final destination, "—" while it has
  none). "—" for the player's ships. The existing "Destination" column keeps
  showing the port the ship is sailing to (its next port).

## Cost

A departure from home costs, at most, cities × goods × 2 evaluations of the
quantity-and-score formula for the destination, then goods evaluations to load.
A stopover choice costs cities × goods. No step depends on the size of the
hold or on the number of units. Each evaluation reads the thresholds of two
markets.

## Error handling

- Settings that would break the formula (a negative weight) are rejected
  where the other good settings are validated (`EconomyDefinition.TryValidateSettings`),
  with a message naming the good.
- A trade never throws for a market or a hold that changed between the
  decision and the purchase: the existing overloads cut down to what is
  possible.

## Testing

EditMode, in `Assets/Tests/EditMode`. The planner's tests are replaced:

- **Quantity**: nothing at or under `midA`; nothing when B projected is at T1
  or above; `need` when A's surplus covers it; the three steps of the sharing
  (surplus, up to `targetB`, equal relative position) on worked numbers;
  smallest lot; degenerate thresholds.
- **Projection**: under-way units, consumption and production over the trip,
  floor at 0.
- **Score**: factor gap on the real curves, times the weight; weight 0 and
  score ≤ 0 mean no transport.
- **Destination**: the highest total wins; the return score counts for a good
  with nothing to carry; unreachable cities and home are excluded; no
  destination when every total is 0; the trip's length does not divide.
- **Stopover**: the detour rule; the highest value wins; tie on the shortest
  detour; none → home; a city already visited on this return trip is
  excluded, and the memory is cleared at home.
- **Loading**: by decreasing score, limited by room (bushels take 10 barrels)
  and stock; the second loading only with more than a quarter free.
- **Fleet tour**: out straight to the destination; full unload there; back
  home with stopovers; partial sale to T1 keeping home's lack; full unload at
  home; a new destination; waiting at home with none; under-way deliveries
  recorded on loading, cut down by a partial sale, and cleared on arrival and
  on removal; a ship reloading for home at a stopover does not buy again what
  it already carries for home.
- **Definition and setup**: `shipsPerCity`; weights in the goods' content
  tests (`AiMerchantsContentTests`, the goods' content tests).
- **Debug**: the new columns' formats; the button's choice of home city.

Checked in the Editor, by playing in fast forward with the debug panel open
over a few weeks: ships go out and come back home, stop on the way back, and
the cities' stocks around T1 are what they supply.

## Documentation

`CLAUDE.md`: the "AI merchants" section is rewritten for the new model (home
city, tour, quantities, scores, weights, deliveries under way, one ship per
city); the Economy section's mention of the planner and the Debug tools
section's button and columns are updated. The spec of 2026-10-12 stays as it
is, as the history of the first model.
