---
paths:
  - "Assets/Scripts/*/Ships/**"
  - "Assets/Tests/EditMode/Ships/**"
  - "Assets/Data/Ships/**"
  - "Assets/UI/WorldMap/ShipInfoPanel.*"
---

# Ships, crew and ports

## Ships

- `Ship` (Core) is the runtime state of one ship; `ShipDefinition` (Game) its static definition, with eight sprites indexed by `CompassDirection` (the order of the sprite sheets).
- `SetDestination` asks the ship's `NavigationPathfinder` for a route over water; without one (a map with no mask) the route is one straight leg. What is left of a step after a waypoint is spent on the next leg, so the speed is constant through turns. Steps are measured in world space, not normalized space, so the speed is the same in every direction on a non-square map.
- A ship is created on the water nearest to the position it is given: cities are on land.
- `ShipsView` owns every ship of the map, the player's and the AI's, in parallel lists by index: `RemoveShip` takes a ship out of all of them at once. It advances them with the world clock's `DeltaTime`, or `Time.deltaTime` without a clock. A ship's name is its own (`DisplayNameOf`), not its definition's. Only the player's ships (`IsPlayerShip`) take orders, are awaited by a fast forward, are paid wages and have their arrivals announced.
- `WorldMapInteraction` arbitrates between the two selection states: a ship is picked before the cities, and at most one thing is hovered and one selected (except a ship in port, below). The right button orders on press, not on release, so that an order given while the pointer moves is not lost; on a city it sends the ship to that city's port.
- The route of the selected ship (`ShipRouteView`): a solid line over `Ship.SailedWaypoints`, a dashed one over what is left. The dashes are laid out from the destination, not from the ship, so they do not slide while it sails. Widths and dashes are in screen pixels.

## Crew

- `ShipCrew` (Core) gives the share of its speed a ship sails at (`SpeedFactor`, from the occupancy). A ship without a sailor does not move but keeps its route: it stays under way, never enters the port it was ordered to, and a fast forward that awaits it ends only on input.
- Nothing changes a crew yet. The ship panel writes the crew on selection: a crew that changes will need an event to refresh it.
- `ShipsView` reads the starting crew from its optional `playerStart`; without it the ship starts with a full crew.

## Ports

- `ShipDocking<TPort>` (Core) says which ships lie in which port and which are sailing to one; `ShipsView` updates it after the ships have advanced. Orders go through it (`OrderToPort`, `OrderToPoint`), never straight to `Ship.SetDestination`.
- A ship enters a port only when it was ordered to that city and has arrived: an order to the water beside a city does not dock. A ship in port leaves on its next order, except one to its own city. A city the ship cannot reach is not entered: it stops as near as it can, at sea. A port is entered from `Ship.AnchorageAt`.
- A ship in port is not on the map: its view is inactive, its entry in `ShipWorldPositions` is NaN (which `CityPicker` skips), and it is deselected when it arrives.
- Selecting a ship from the city panel's "ships in port" tab keeps the city selected: the only case of two selections. `WorldMapInteraction` clears it as soon as another city, or none, is selected, and the panel follows a selected AI ship that leaves by itself (`ShipDocking.Undocked`).
