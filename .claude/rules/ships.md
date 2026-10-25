---
paths:
  - "Assets/Scripts/*/Ships/**"
  - "Assets/Tests/EditMode/Ships/**"
  - "Assets/Data/Ships/**"
  - "Assets/UI/WorldMap/ShipInfoPanel.*"
---

# Ships, crew and ports

## Ships

- `Ship` (Core) is the runtime state of one ship; `ShipDefinition` (Game) its static definition. `SetDestination` asks the ship's `NavigationPathfinder` for a route over water; without one (a map with no mask) the route is one straight leg. `FollowRoute` takes a route worked out beforehand (the AI's cached routes) without asking the pathfinder: it must start where the ship is.
- `ShipsView` owns every ship of the map, the player's and the AI's, and advances them, the docking and the arrivals at every `GameClock.Stepped` (in real time once per frame without a clock). It raises `StepStarted` before the ships sail a step: `AiMerchants` advances its fleet there, so that a ship that leaves sails the step and a stopover is not shortened by the step it starts in. A ship's name is its own (`DisplayNameOf`), not its definition's. Only the player's ships (`IsPlayerShip`) take orders, are awaited by a fast forward, are paid wages and have their arrivals announced.
- `WorldMapInteraction` arbitrates between the two selection states: a ship is picked before the cities, and at most one thing is hovered and one selected (except a ship in port, below). An order on a city sends the ship to that city's port.

## Crew

- `ShipCrew` (Core) gives the share of its speed a ship sails at (`SpeedFactor`). A ship without a sailor does not move but keeps its route: it stays under way, never enters the port it was ordered to, and a fast forward that awaits it ends only on input.

## Ports

- `ShipDocking<TPort>` (Core) says which ships lie in which port, which are in a city's harbour and which are sailing to one; `ShipsView` updates it after the ships have advanced, with the step's game time (`Update(deltaTime)`), which advances the harbours before it lets in the ships that reached their anchorage. Orders go through it (`OrderToPort`, `OrderToPoint`), never straight to `Ship.SetDestination`. It is given each port's `Harbour` (`harbourOf`); a port without one is entered at once, as a whole docking without harbours is.
- A ship enters a port only when it was ordered to that city and has arrived: an order to the water beside a city does not dock. A city the ship cannot reach is not entered: it stops as near as it can, at sea. A port is entered from `Ship.AnchorageAt`.
- At its anchorage, a ship enters the city (`StateOf`, `CityOf`, `IsInCity`): it queues or crosses the grid to its berth, and is docked (`IsDocked`, `PortOf`, `GetShipsIn`, `Docked`/`Undocked`) only at the berth. `GetShipsInCity` lists every state. A ship in a city keeps its map position at the anchorage and does not sail: its route is sailed only once it is back at sea, so `ShipsView` must advance only the ships not `IsInCity`.
- Orders in a city: the route is worked out first from the anchorage, so an order the ship ignores changes nothing. A queued ship sails at once; an arriving or docked ship leaves (`Undocked` at once if it was docked) and sails its route when it reaches the edge; a leaving ship keeps leaving with the new route. An order to its own city changes nothing, except for a leaving ship, which turns back to its berth and drops its route (`Ship.Stop`). `Remove` takes a ship out of a city at once, whatever its state.
- `CityStateChanged` is raised after every change of `StateOf`, the harbour's own (entered from the queue, berthed, exited) included, with the city.
- A ship in port is not on the map: its view is inactive, its entry in `ShipWorldPositions` is NaN (which `CityPicker` skips), and it is deselected when it arrives.
- `ShipArrivals` reports an arrival when a ship docks at its berth, or stops at sea; a route ending at an anchorage that leads into a queue or a grid is not an arrival. It reports the tracked ships that entered a queue, and are still in it, through `Queued`.
- Selecting a ship from the city panel's "ships in port" tab keeps the city selected: the only case of two selections. `WorldMapInteraction` clears it as soon as another city, or none, is selected, and the panel follows a selected AI ship that leaves by itself (`ShipDocking.Undocked`).

## Harbour traffic

- `Harbour` (Core) is one city's traffic on its `CityGrid`: a ship is only a key in it, it never reads the ship's map position or route. A ship's place is its berth and its distance from the berth along the berth's exit path.
- A berth is reserved from the moment its ship enters the grid until it has left it by the edge, so a ship leaving (or turning round) still holds it. Turning round (`Leave`, `Return`) keeps the berth and only changes the direction.
- Ships that find no free berth queue, first come first served; a queued ship enters on `Advance` or `Remove`, one per free berth.
- The rest of a step is not carried over: a ship that docks or exits mid-step does not use the leftover distance.
