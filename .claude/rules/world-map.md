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
- `MapCameraModel` is where input has sent the camera (the target); `MapCameraSmoother` is what is displayed, eased towards it. Position and size share one easing factor on purpose: that keeps the zoom anchor fixed during the transition, so do not split it into separate pan and zoom settings. Hover, picking and marker size follow the displayed view.
- Picking is `CityPicker` (nearest point within a screen-pixel radius, no colliders), for cities and ships alike.
- A city stands on a navigable cell of the mask, at the edge of the water beside its town on the map. The order of the map's cities is the order of the AI merchants' numbers, and the player starts in the first.
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`); it and the navigation mask tool find the map through `WorldMapEditorContext`.
- `WorldMapInteraction` arbitrates between the selection of cities and that of ships (see `ships.md`), and drives fast forward (see `time.md`).

## City panel

- `CityInfoPanelController` hosts the market (`CityMarketSection`, see `economy.md`), the buildings (`CityBuildingsSection`, see `population.md`) and the ships in port.
- Each tab's page wraps the element its section shows and hides, so the tabs and the sections never write the same `display`. The panel's fixed height is chosen so that the market's active goods show without scrolling.
- Its sections listen to the market, the hold and the treasury only while they are shown.
