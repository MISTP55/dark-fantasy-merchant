# AI Merchants — Design

Date: 2026-10-12
Status: awaiting review

## Purpose

Put the first other merchants on the map. Five ships sail on their own, buy
where a good is in surplus and sell where it is short, so that the world's
goods move without the player and the player has competitors for the best
routes. They are also the first ships that are not the player's, which makes
the code say who owns a ship.

## Scope

In scope:

- Five AI ships, each starting in the port of a random city at every launch.
- The existing ship sprites, tinted blue.
- AI ships can be hovered and selected, on the map and in a city's panel, and
  show the same panel and route as the player's. They take no order.
- A trade decision that seeks profit between two cities.
- A "Navires" tab in the debug panel (F1) that lists every ship, the player's
  and the AI's.

Out of scope:

- Gold for the AI: it is unlimited (decided). AI ships pay no wages.
- AI merchants as characters: names, reputations, fleets, bankruptcy.
- AI ships hiring sailors, or anything that changes their crew.
- Notifications about AI ships.
- Saving and loading.
- Any interaction between ships (blocking a port, piracy).

## Decisions

Taken with the user:

- **Unlimited gold.** An AI merchant has no treasury. What it would have paid
  and received is still summed, as a profit shown by the debug tool only.
- **Listed in port.** An AI ship in port is in the list of its city's panel
  and can be selected there. The market then stays read-only.
- **A stopover of one day.** A ship sells on arrival, stays a day, then
  decides where to go, buys and leaves.
- **Unreachable cities.** A ship never chooses a city whose port it cannot
  enter (its nearest water is in another navigable region). On a map whose
  cities are all connected by water this excludes nothing.

Taken by the design:

- AI ships are named "Marchand indépendant 1" to "Marchand indépendant 5".
- They sail with 14 sailors out of 28: full speed.
- Fast forward only awaits a ship of the player: the arrival of a selected
  AI ship does not end it.

## Ownership of ships

`ShipsView` keeps one list of all ships, as today, and knows which are the
player's.

- `IsPlayerShip(Ship)`, and `PlayerShips` (read-only list).
- `SpawnAiShip(ShipDefinition, Vector2 position, ShipCrew, string displayName, Color tint)`
  returns the new `Ship`. It validates the definition as `Start` does for the
  player's.
- A display name is stored per ship; `DisplayNameOf` returns it. The player
  ship keeps the name of its definition.
- `ShipView.Initialize` takes the tint, which replaces its normal color. The
  hovered and selected colors are unchanged.

What only concerns the player filters on it:

| Where | Change |
|---|---|
| `WorldMapInteraction.OnCommanded` | Ignores a selected ship that is not the player's. |
| `WorldMapInteraction` fast forward | `awaitedShip` is set only for a ship of the player. The ship selected before is still selected again afterwards, whoever owns it. |
| `PlayerExpenses.CountSailors` | Counts `PlayerShips`. |
| `ShipArrivalNotifier` | Ignores the arrivals of AI ships. |
| `CityInfoPanelController` | Gives the market section a hold only for a ship of the player, so the hold column and the [−] / [+] buttons do not show for an AI ship. |

Nothing else changes for selection: an AI ship is hovered, selected, shown in
the ship panel with its route drawn, deselected when it enters a port, and
selectable from the city panel, exactly like the player's.

## Trade decision (Core)

### Trading without a treasury

`Trade` gets two overloads without a `Treasury`:

- `Buy(market, hold, good, barrels)`: as many barrels as the city has and the
  hold has room for.
- `Sell(market, hold, good, barrels)`: as many as are aboard.

They price the barrels one by one like the existing ones and return a
`TradeResult` with the gold it came to. The existing overloads keep their
behaviour.

### `TradeRoutePlanner`

Plain C#, no Unity type but what `Core` already uses. It works on ports by
index.

```
TradeRoutePlanner(IReadOnlyList<CityMarket> markets, double[,] travelDays, double stopoverDays)

bool TryPlanTrade(int from, int capacity, int[,] incoming, out TradePlan plan)
bool TryPlanRelocation(int from, int capacity, int[,] incoming, out int destination)
```

- `travelDays[i, j]` is the days a ship takes from port `i` to port `j`;
  infinity when it cannot sail there.
- `incoming[port, good]` is the barrels other AI ships are carrying to that
  port. They count as already delivered when the selling prices are read, so
  that five ships do not aim at the same shortage.
- `TradePlan`: the destination, the barrels to buy of each good, the expected
  profit.

**Loading for one destination.** Barrel by barrel: for each good, the margin of
the next barrel is `destination.SellPriceOf(good, incoming + loaded)` minus
`source.BuyPriceOf(good, loaded)`. The good with the highest margin is loaded,
while that margin is above zero, the source has a whole barrel left and the
hold has room. A cargo can therefore mix goods. The real prices and their 5 %
margins are used, so a plan's profit is what the trade gives if nothing
changes meanwhile.

**Choosing the destination.** The reachable destination with the highest
profit per day, where the days are `travelDays[from, to] + stopoverDays`.
`TryPlanTrade` returns false when no destination gives a profit.

Buying where the stock is high and selling where it is low is not a rule of
its own: prices follow stocks, so it is what the profit selects.

**Relocation.** When there is nothing to trade from here, `TryPlanRelocation`
looks for the reachable port from which the best trade starts: for every
other port `s`, the best plan from `s` with an empty hold, scored as its
profit over `travelDays[from, s] + stopoverDays` plus the days of that plan.
The ship sails there empty. It returns false when no port has a profitable
trade. Ports another AI ship is already sailing to empty are left out (added after the code review), so that idle ships do not all go to the same one.

### Travel times

`SeaRoutes` (Core, static): the length in world units of the route between
two points, measured through `MapProjection`, and whether the route ends on
the second point. With a pathfinder it is the length of `TryFindPath`'s
waypoints; without one, a straight line, always reachable.

## Driving the merchants (Game)

### `AiMerchantsDefinition`

`Assets/Data/AI/AiMerchants.asset`:

| Field | Default |
|---|---|
| `shipCount` | 5 |
| `shipDefinition` | the merchant ship |
| `tint` | a light blue (about `#7FB2FF`) |
| `crew` | 14 |
| `stopoverDays` | 1 |
| `shipNameFormat` | "Marchand indépendant {0}" |

### `AiMerchants`

A `MonoBehaviour` with references to the definition, `ShipsView`,
`WorldEconomy`, `WorldMapView` and `WorldClock`. All are required; it logs an
error and does nothing without one. It starts working in `Update`, once the
ships view and the economy are ready.

1. **Travel times.** It measures the route between every pair of cities, from
   anchorage to anchorage, a few pairs per frame within a time budget (about
   4 ms), so that the launch has no hitch. A length becomes days through the
   ship definition's speed, the crew's speed factor and the calendar's seconds
   per day. A city that reaches no other is reported once with a warning: it
   is most likely a mistake of the mask.
2. **Start.** When the table is complete, it draws a city for each ship among
   those that reach at least one other, without repeating a city while there
   are enough, with `UnityEngine.Random` (not seeded: another draw at every
   launch). It spawns the ship there and orders it to that port, which it
   enters at once. With no usable city it logs a warning and spawns nothing.
3. **Arrival** (`ShipsView.ShipArrived`, AI ships only). In a port: the ship
   sells its whole hold, whatever the prices have become, and its stopover
   starts. At sea, which a reachable destination should never give: it is
   ordered to the nearest port it can reach.
4. **Departure.** The stopover is counted down with the clock's `DeltaTime`.
   When it ends, the planner is asked for a trade: the ship buys the plan and
   is ordered to the destination through `Docking.OrderToPort`. Otherwise for
   a relocation: it is ordered there empty. Otherwise it waits another
   stopover and asks again.

`incoming` is rebuilt for each decision from the holds of the AI ships under
way and their `Docking.DestinationPortOf`.

It exposes what the debug tool reads: for an AI ship, its cumulated profit
(gold received minus gold paid).

At the start of a game every city holds the same days of every good, so no
route pays: the ships wait in port for the first days, until producers fill up
and the others run short. This is expected.

### Scene setup

`WorldMapSetup` creates the `AiMerchants.asset` when it is missing and the
`AI Merchants` object, also in an existing scene that has none, with its
references. It adds the `shipsView` and `aiMerchants` references of the debug
panel to an existing scene.

## Debug panel

The cities panel becomes the debug panel, with two tabs: "Villes" and
"Navires". F1 opens it on the tab it was last on.

- `DebugCitiesPanelController` is renamed `DebugPanelController`, and
  `DebugCitiesPanel.uxml` / `.uss` are renamed `DebugPanel.uxml` / `.uss`,
  each with its `.meta`, so the scene keeps its references.
- The header holds the title "Débogage", the two tab buttons and the close
  button. The cities tab is the present table, legend included, unchanged.
- `DebugShipsTable` builds the ships tab: one row per ship of `ShipsView`,
  the player's first.

| Column | Content |
|---|---|
| Navire | Display name. |
| Propriétaire | "Joueur" or "IA". |
| État | "Au port", "En mer" (under way) or "À l'arrêt" (idle at sea). |
| Lieu | The city of its port, otherwise its normalized position ("0,412 ; 0,733"). |
| Destination | The city it will enter, otherwise the end of its route as a position, otherwise "—". |
| Équipage | "14 / 28". |
| Vitesse | The crew's speed factor, "100 %". |
| Cale | "60 / 200". |
| One per good | Barrels aboard. |
| Profit | Cumulated profit of an AI ship ("1 250 or"); "—" for the player's. |

The rows are rewritten every frame while the ships tab is shown, and not
otherwise. Ships created after the table was built get their row then.

## Error handling

- A missing reference or an unusable definition logs one error and disables
  the component, like the others.
- `AiMerchants` never throws out of a decision for a market or a hold that
  changed between the plan and the purchase: the trade overloads cut down to
  what is possible.
- A city without a market is left out of the planner's ports.

## Testing

EditMode, in `Assets/Tests/EditMode`:

- `TradeTests`: the overloads without a treasury (limits of stock, room and
  cargo; gold returned; events raised once).
- `TradeRoutePlannerTests`: surplus to shortage is chosen; loading stops when
  the margin is gone, at the stock and at the capacity; goods are mixed;
  a nearer destination wins at equal profit; unreachable ports are ignored;
  incoming cargo lowers a destination; relocation picks the source of the best
  trade; no route returns false.
- `SeaRoutesTests`: length with and without a pathfinder; a port in another
  region is not reached.
- `DebugShipsTableTests`: the formats of the cells and the rows built.
- Existing tests touched by ownership are updated
  (`PlayerExpensesTests`, `ShipArrivalNotifierTests`, the ship panel's).

Checked in the Editor, by playing: the tint, the selection of an AI ship on
the map and in a city panel, the refusal of orders, the trading over a few
weeks in fast forward with the debug panel open.

## Documentation

`CLAUDE.md` gets an "AI merchants" section, and its project state, scene
setup, Ships, Economy and Debug tools sections are updated.
