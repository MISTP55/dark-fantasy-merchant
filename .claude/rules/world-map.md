---
paths:
  - "Assets/Scripts/*/WorldMap/**"
  - "Assets/Tests/EditMode/WorldMap/**"
  - "Assets/Data/WorldMap/**"
  - "Assets/Data/Cities/**"
  - "Assets/UI/WorldMap/CityInfoPanel.*"
---

# World map

- Positions on the map are **normalized** (`(0,0)` bottom-left, `(1,1)` top-right) and converted to world space only through `MapProjection`. The map is centered on the world origin and sized by `WorldMapDefinition.worldWidth`, so the map image can be replaced by one of another resolution without moving anything. `Assets/Art/WorldMap/WorldMap.jpg` is a placeholder.
- `WorldMapView` owns the scene's `MapSelectionState<CityDefinition>`. Markers and the city panel both react to it and do not know about each other.
- `WorldMapInput` is the only reader of the `WorldMap` action map; it raises click, drag and zoom events and ignores pointer input that starts over the UI.
- `MapCameraModel` is where input has sent the camera (the target); `MapCameraSmoother` is what is displayed, eased towards it. Hover, picking and marker size follow the displayed view.
- Picking is `CityPicker` (nearest point within a screen-pixel radius, no colliders), for cities and ships alike.
- A city stands on a navigable cell of the mask, at the edge of the water beside its town on the map. The order of the map's active cities is the order of the AI merchants' numbers, and the player starts in the first when the home city is empty or inactive (`player.md`).
- **Active cities.** Each `MapCity` of the map is ticked active or not before Play mode, to test the economy on fewer cities. An inactive city does not exist in the game: `WorldMapDefinition.Cities` holds only the active ones, and everything in the game reads them through `WorldMapView.Cities`; `MapCities` is every city, for the Editor and the content tests. The shortages an inactive city leaves are wanted, not made up for.
- **Cities without a usable layout are inactive too.** `WorldMapView.SpawnMarkers` skips (one warning each, "CityDefinition 'X' is inactive: …") an active city that `WorldMapView.IsUsable(city, buildings, out problem)` refuses: no layout, or a layout whose `TryCreateGrid` fails (see `city-view.md`). It checks with the `buildings` catalogue the scene setup wires into the view; without one it logs one error and skips no city.
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`), inactive ones in dark grey; it and the navigation mask tool find the map through `WorldMapEditorContext`.
- `WorldMapInteraction` arbitrates between the selection of cities and that of ships (see `ships.md`), and drives fast forward (see `time.md`).

## City panel

- `CityInfoPanelController` hosts the market (`CityMarketSection`, see `economy.md`), the transfers between a ship and the trading post (`TradingPostTransferSection`) and the ships in the city, in three tabs: Marché, Transfert and Navires. The Transfert tab exists only while the shown city has the player's trading post; open when it goes (another city, a city without one), the panel falls back on Marché. Its "Navire :" drop-down lists the same docked ships of the player as the market's, chosen apart from it (kept while still there, otherwise the first). It has no building tab: the player builds only from the city view (`PlayerBuilder`, see `population.md`). Its "Voir la ville" button, in the header beside the close button, raises `ViewCityRequested` with the shown city.
- The ships tab lists every ship in the city (`GetShipsInCity`: waiting, arriving, docked, leaving), each row with its state (`ShipRowText`); the market's trading ships are chosen among the docked ones only (`GetShipsIn`). Both are rebuilt on `CityStateChanged`.
- The market's "Avec :" drop-down (`TradingPartnerLabels`) lists the player's docked ships, then "Comptoir" when the player has a trading post in the city (`WorldEconomy.TradingPostOf`). What trades is the one kept while still there, otherwise the first ship, otherwise the trading post; a new city starts from its first ship. The panel follows `WorldEconomy.TradingPostOpened` for the shown city, and raises `PlayerTraded` for the trading post's trades as for a ship's.
- Each tab's page wraps the element its section shows and hides, so the tabs and the sections never write the same `display`.
- Its sections listen to the market, the holds, the trading post and the treasury only while they are shown.
