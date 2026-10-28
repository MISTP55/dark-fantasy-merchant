---
paths:
  - "Assets/Scripts/*/Player/**"
  - "Assets/Tests/EditMode/Player/**"
  - "Assets/Data/Player/**"
  - "Assets/Data/Economy/Expenses.asset"
  - "Assets/UI/WorldMap/TreasuryHud.*"
  - "Assets/UI/WorldMap/NotificationsHud.*"
---

# Treasury, notifications and expenses

- `Treasury` (Core) can be negative (debt). `Withdraw` always takes the amount, for what is paid whatever the gold left (wages); `TryWithdraw` only when `CanAfford`, for what a lack of gold must block (buying, building). `Changed` is raised only on an actual change.
- The list's `top` in `NotificationsHud.uss` follows the height of the treasury label: change them together.
- Notification texts have no article and nothing that agrees with the ship's name or elides before the city's; keep new ones that way.
- `Ship.Arrived` is raised when a ship reaches the end of its route, not when an order replaces it. `ShipArrivals<TPort>` also counts a ship that enters a port without sailing, and `ShipsView` calls its `Report` after `Docking.Update`, so that the port is known.
- `ShipArrivalNotifier` also posts, for the player's ships, the entry into a city's queue (`ShipsView.ShipQueued`, from `ShipArrivals.Queued`); entering the grid is not announced, docking at the berth is the arrival.
- `PlayerExpenses` drives `WeeklyBilling` (Core) with `GameClock.DayStarted`; a new recurring expense is a new amount of `WeeklyBill`. The trading posts' rent (`Warehouse.WeeklyRentOf`) is one: `PlayerExpenses` passes it as a function read on the billing day, and counts none while `WorldEconomy` is not ready. Like the wages it is taken whatever the gold. The player's workshops are two more, read the same way: their running cost (`WorkshopDefinition.WeeklyRunningCost`, carried by `WorkshopOffer`, owed for every workshop built, with workers or not) and their wages (`ExpensesDefinition.WorkshopWageShare` of the base value of what their workers of the billing day make in a week, bonus included, inputs not counted: `CityMarket.PlayerWeeklyWagesOf`), summed over every city by `PlayerWorkshopCosts` and rounded once (rules in `PlayerWorkshopCostsTests`).
- The home city is `PlayerStartDefinition.HomeCity`; `PlayerStartDefinition.HomeCityOn` is the only way to read it (the first active city when it is empty or inactive): `ShipsView` starts the player ship at its anchorage, and at launch `HomeCityStart` docks it at a berth (`ShipDocking.PlaceInPort`), shows the city close up on the map camera and opens the city view on it. Before the view opens, `HomeCityStart` gives the player the home city's trading post (`WorldEconomy.TryGiveTradingPost`, a warning when it cannot), so the view shows it from the start. `HomeCityStart` runs at execution order 100, so that its `Start` comes after the economy's, which makes the harbours, the camera's, whose close-up is ignored before then, and that of `AiMerchants`, whose ships enter their ports only at the first update, after the player's has taken its berth.
