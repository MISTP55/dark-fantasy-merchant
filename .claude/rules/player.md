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
- `PlayerExpenses` drives `WeeklyBilling` (Core) with `GameClock.DayStarted`; a new recurring expense is a new amount of `WeeklyBill`.
