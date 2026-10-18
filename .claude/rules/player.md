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

## Treasury

- `Treasury` (Core) can be negative (debt). `Withdraw` always takes the amount, for what is paid whatever the gold left (wages); `TryWithdraw` only when `CanAfford`, for what a lack of gold must block (buying, building). `Changed` is raised only on an actual change.

## Notifications

- `NotificationFeed` (Core), newest first, aged by `PlayerNotifications` in real time, not with the world clock: a notification is read by the player, in fast forward too.
- The list's `top` in `NotificationsHud.uss` follows the height of the treasury label: change them together.
- Texts have no article and nothing that agrees with the ship's name or elides before the city's; keep new ones that way.
- `Ship.Arrived` is raised when a ship reaches the end of its route, not when an order replaces it. `ShipArrivals<TPort>` also counts a ship that enters a port without sailing, and `ShipsView` calls its `Report` after `Docking.Update`, so that the port is known.

## Expenses

- `WeeklyBilling` (Core) keeps no count of its own: the week is read from the clock's elapsed days, counted from the first day of the game (not within a month), so a day it is not told of shifts no week, and a step that crosses several weeks pays each. It pays with `Withdraw`, so a player without gold goes into debt.
- `WeeklyBill` holds one amount per kind of expense; a new recurring expense is a new amount added to its `Total`. The only one so far is the sailors' wages, counted when the week ends, at sea or in port.
