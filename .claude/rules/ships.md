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

- `ShipDocking<TPort>` (Core) says which ships lie in which port and which are sailing to one; `ShipsView` updates it after the ships have advanced. Orders go through it (`OrderToPort`, `OrderToPoint`), never straight to `Ship.SetDestination`.
- A ship enters a port only when it was ordered to that city and has arrived: an order to the water beside a city does not dock. A ship in port leaves on its next order, except one to its own city. A city the ship cannot reach is not entered: it stops as near as it can, at sea. A port is entered from `Ship.AnchorageAt`.
- A ship in port is not on the map: its view is inactive, its entry in `ShipWorldPositions` is NaN (which `CityPicker` skips), and it is deselected when it arrives.
- Selecting a ship from the city panel's "ships in port" tab keeps the city selected: the only case of two selections. `WorldMapInteraction` clears it as soon as another city, or none, is selected, and the panel follows a selected AI ship that leaves by itself (`ShipDocking.Undocked`).
