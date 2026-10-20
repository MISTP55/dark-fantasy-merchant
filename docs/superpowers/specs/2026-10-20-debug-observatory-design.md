# Debug Panel as an Observatory of the Simulation — Design

Date: 2026-10-20
Status: approved by the user

## Purpose

The user is about to change how several systems of the simulation work. Before
and while doing so, they need to see as much as possible of what happens in the
game, to catch the problems a change causes further down the line (a change in
one system that unsettles another). There is no specific bug to hunt: the
world is left running, often sped up, and its evolution is read afterwards.

Today the debug panel (F1) only shows the present: a cities table and a ships
table. The simulation keeps no history at all; the only memory is
`MarketWeekLog`, the production and consumption of the last 7 days. Values the
simulation computes are not shown (thresholds, prices), and what happens inside
a city's day (hires, layoffs, poor leaving, class moves, beggars, houses) is
neither counted nor shown. Neither are the AI merchants' trades: only their
cumulated profit is.

This work turns the debug panel into an observatory: a daily record of every
city, curves over time, a journal of events and a journal of trades, organised
in tabs.

## Goals

- Record, every game day, a snapshot of every city, kept over a horizon that
  defaults to 12 months and can be changed in the panel.
- Show that history as curves, browsable three ways (by city, by indicator,
  composed).
- Show the present state of the cities, split so that thresholds and prices fit.
- Show a journal of what happens in the cities' days.
- Show a journal of the trades and the flows of goods they make.
- Keep the ships tab as it is.

## Non-goals

- No change to how the simulation behaves. The record reads; it must not alter
  a single figure of the simulation. (The only things the panel changes stay
  what they are today: the speed slider and the add/remove AI ship buttons.)
- No export to a file, no saving the history with a game (there is no save
  system yet).
- Nothing in release builds: everything here exists only in the Editor and in
  development builds (`Debug.isDebugBuild`), as the debug tools today.

## Anti-goals

- A record that changes the simulation's results, or whose cost slows the game
  down noticeably at x600.
- Choosing now what will matter: the user does not yet know what their changes
  will break, so the record takes everything listed below, not a selection.

## Decided with the user

- **Horizon**: 12 months by default (the calendar has 12 months of 30 days, so
  360 days), with a setting in the panel to change it.
- **Curves**: all three layouts of the mockups
  (`2026-10-20-debug-observatory-mockups/courbes-layout.html`, next to this
  document; the other tabs are in `autres-onglets.html`; their figures are
  invented), each in its own sub-tab of the curves tab:
  1. **Par ville**: a list of the cities on the left (alphabetical); on the
     right, all the curves of the chosen city: inhabitants per class as stacked
     areas with the beggars, the satisfaction of the three classes, workers and
     reserve; then one small chart per good: the stock over coloured bands for
     its thresholds T0–T3, and its price as a dotted line.
  2. **Par indicateur**: pick an indicator (and a good when the indicator is a
     good's); one small chart per city, side by side; a checkbox puts them all
     on a common scale.
  3. **Composer**: one large chart; curves are added and removed as chips, each
     a city × (good) × indicator.
- **Hover**: everywhere, hovering a curve shows the date and the value of that
  day.
- **Live**: the curves follow the days while their sub-tab is shown.
- **Recording runs from the start of the game**, panel open or closed, so that
  there is something to see when it opens.
- **Tabs** of the panel, in this order: **Villes**, **Courbes**, **Événements**,
  **Échanges IA** (named "Échanges", see the calls below), **Navires** (mockup
  `autres-onglets.html`). The header keeps
  the speed slider and the close button, and gets the horizon setting.
- **Villes** has two sub-tabs:
  - **Population**: one row per city: inhabitants, beggars over their target,
    each class's count over its room with its satisfaction, growth since the
    start (today's city columns), plus the houses the city has built.
  - **Marchés**: a choice of good; one row per city: stock, T0, T1, T2, T3, buy
    price, sell price, production, consumption, net (nominal and last week, as
    today), workers (with reserve), and what the AI ships are carrying there
    (`UnderWayTo`). A choice "toutes les marchandises" gives back today's wide
    table, every good in columns.
  - A total row, as today.
- **Événements**: a journal, newest first, filterable by city, good and type,
  with a "Figer" button that stops it scrolling so that it can be read. Kept
  over the same horizon as the curves. Types:
  - shortage (see the calls below for what counts as one) and threshold
    crossings (the stock moves from one band to another: under T0, T0–T1,
    T1–T2, T2–T3, above T3);
  - hires and layoffs, grouped per day, city and good;
  - production stopped above T3 (and resumed);
  - poor leaving the city (with the workers they take), moves between classes,
    beggars arriving or leaving;
  - a house built by the city.
- **Échanges IA** has two sub-tabs:
  - **Journal**: every buy and sell of the AI merchants: date, ship, city,
    operation, good, units, average unit price, total, and the profit of the
    sale (column "Bénéfice", defined in the calls below).
  - **Flux par marchandise**: for a chosen good, what each city received and
    shipped through the AI merchants over the horizon.
- **Navires**: unchanged.

## Calls made by Claude (to be checked by the user)

- **The horizon setting** is a drop-down in the header: 3, 6, 12, 24, 48 months,
  or "Toute la partie". Shortening it drops the days beyond it at once;
  lengthening it does not bring back what was dropped. It applies to the
  curves, the events and the trades alike. It lasts for the session only
  (default 12 months at every Play).
- **Composer's scales**: a stock and a price have nothing in common, so each
  curve of the composed chart has its own vertical scale (from its own minimum
  to its maximum over the shown period); the real value is read on hover. The
  axis shows no numbers, only the hover does.
- **Profit of a sale** (the mockup's "Bénéfice du voyage"): the fleet keeps,
  per AI ship and good, the average price it paid for what is aboard; the
  profit of a sale is its total minus that average times the units sold. A buy
  has none ("—").
- **The player's trades** are in the trades journal too, marked "Joueur" in the
  ship column, since they move stocks as much as the AI's: the buys and sells
  of the market tab (`CityMarketSection`) and the materials the player's
  constructions buy from the market (`Construction`, operation "Construction").
  They have no profit ("—"): the player's hold keeps no purchase price. The tab
  is therefore named "Échanges" rather than "Échanges IA", and the flows count
  the player's trades with the AI's. Materials a city takes for its own houses
  are not trades: they are the "house built" event.
- **Shortage**: a city's day comes in this order: consumption, then
  production. A good the city produces is therefore rarely at 0 in the
  evening even when the inhabitants went short in the morning. A shortage is
  thus a day when the city consumed less of a good than it needed
  (`DailyConsumptionOf`), read from the day's report, not a stock at 0 in the
  evening. The record also keeps, per city and good, the units that were
  missing that day, as an indicator ("Manque") of the curves.
- **Threshold crossings** are counted whatever moved, the stock or the
  thresholds (which follow the population every evening): what matters for
  prices and hiring is the band, not why it changed. They are many: their
  type is unticked in the journal's type filter by default.
- **The panel's size**: charts need room. The panel takes a fixed share of the
  screen (about 90 % of its width and of the height under the time HUD) instead
  of growing with its table as today; only the content of a tab scrolls.
- **Charts are drawn by hand** with UI Toolkit's vector API
  (`VisualElement.generateVisualContent` and `Painter2D`): no charting package.
  One small `VisualElement` subclass draws a time series chart (lines, stacked
  areas, horizontal bands, dotted line) and handles the hover.
- **A chart redraws at most once per frame**: at x600, 20 days pass each
  second; the record marks the charts dirty, and they redraw once in the next
  frame, not once per day.
- **Long journals use a virtualised `ListView`** (tens of thousands of rows over
  a year). The striped-rows convention is kept through the `ListView`'s
  alternating row background, styled like `ListStripes`' stripe, since
  `ListStripes.Apply` cannot see rows that a virtualised list recycles.
- **Indicators of "Par indicateur" and "Composer"**, all from the daily record:
  - per city: inhabitants, each class's count, each class's room, each class's
    satisfaction, beggars, beggar target, houses built by the city;
  - per city and good: stock, T0, T1, T2, T3, buy price, sell price, nominal and
    actual production, nominal and actual consumption, missing consumption
    ("Manque"), workers, reserve, under way (AI).
  "Par indicateur" also shows, after the cities, a "Monde" chart: the sum over
  every city, for the indicators that add up (not satisfactions, not prices).
- **Days**: the curves have one point per day. A price in the record is the
  price of one unit at the stock of the end of the day (`BuyPriceOf(good)`,
  `SellPriceOf(good)`).

## How it fits the code (for the builder)

What follows is guidance; the builder decides the details.

- **The record is Core**, plain C# and covered by EditMode tests: a ring of
  daily snapshots per city sized by the horizon, a journal of events and a
  journal of trades, each trimmed to the horizon. It lives in
  `Assets/Scripts/Core/Debug` (next to `MarketWeekLog`); the panel and its
  tables in `Assets/Scripts/Game/Debug` and `Assets/UI/Debug`. The rule
  `.claude/rules/debug-tools.md` gets updated, and its "read the simulation and
  change nothing" line stays true.
- **When a day is recorded**: after every city's `StartDay` of that day. Today
  `WorldEconomy.OnDayStarted` runs them all; it can raise an event once they
  are done (for example `DayEnded(GameDate)`), which the recorder listens to.
  Relying on the order of `DayStarted`'s subscribers would be fragile.
- **What happens inside a city's day** is not observable from outside: hires
  from the reserve versus from the beggars, layoffs, the reserve's leak, class
  moves, the poor leaving, the beggars' change, houses. `CityMarket.StartDay`
  fills a small per-day report (as it fills `MarketWeekLog`), which the
  recorder reads after the day. Filling it must not change any figure. The
  report also holds, per good, what was consumed against what was needed (the
  shortages). Threshold crossings need no report: they come from comparing a
  day's snapshot with the previous one.
- **Trades**: `AiMerchantFleet` reports every `Trade.Buy` / `Trade.Sell` it makes
  (ship, port, good, units, gold, profit of a sale) through an event; the
  player's trades come from the market tab's trading (`CityMarketSection`) and
  from `Construction`'s payment of materials. The record does not watch
  `CityMarket.Take`/`Put`, which do not know who trades nor for how much.
- **Recorder component**: a MonoBehaviour of the Debug subsystem that exists
  only in debug builds, built by `WorldMapSetup` like every scene object, and
  that the panel reads. It starts recording on the first day.
- **Cost**: about 24 cities × 14 active goods × ~15 figures a day, a few
  hundred kilobytes over a year; "Toute la partie" over ten game years stays
  under a hundred megabytes. Recording a day must stay well under a
  millisecond, so as not to weigh on x600 (see `time.md`).
- **French and alphabetical order**: every list the user reads (cities, goods,
  indicators where they are names) is sorted with `NameOrder`; every text is in
  French; numbers are written as the existing debug tables write them
  (`DebugCitiesTable.FormatQuantity` and friends); dates as the calendar
  writes them (`CalendarDefinition.Format`).

## Left to the builder

- The exact layout, colours and sizes of charts and tables, within the
  mockups' spirit and the existing debug panel's style.
- How the per-day report and the trade events are shaped in Core.
- How the ring buffers are laid out in memory.
- The order of the implementation steps and their tests.
