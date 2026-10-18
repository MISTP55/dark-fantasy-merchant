# City Panel in Two Parts, with Tabs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The city panel becomes a fixed-size panel with the city on the left and three tabs on the right ("Marché", "Bâtiments", "Navires au port"); the market tab chooses the player's ship that trades in a drop-down list, without selecting it first.

**Architecture:** A stateless `TradingShipChoice` (Core, generic over the ship like `ShipDocking<TPort>` is over the port) says which player ships in a port can trade and which one stays chosen. `CityInfoPanelController` keeps the chosen ship and the open tab, and gives the chosen ship's hold to `CityMarketSection` and `CityBuildingsSection`, which do not change. The UXML gets a header, a left column and a right column of tab buttons over three pages; each page wraps the element its section shows and hides, so the tabs and the sections never fight over the same `display`.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (UXML + USS, `DropdownField`), Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-18-city-panel-tabs-design.md`

## Global Constraints

- Code, comments, logs, element names and classes in English; every text the player reads in French, exactly: "Marché", "Bâtiments", "Navires au port (3)" (always with the count, "(0)" included), "Navire :", "Aucun navire", "Aucun navire au port", "Production efficace", "Production inefficace", "Cale : 120 / 200 tonneaux" (`ShipInfoPanelController.FormatCargo`, unchanged). Numbers through `TreasuryHudController.FormatNumber`.
- Panel: about 900 × 620 units at the 1920 × 1080 reference, left part about 260 wide, centered as today; only a tab's content scrolls.
- The panel opens on "Marché" when a city is selected from no city; selecting another city while it is open keeps the open tab.
- The trading ship is reset (first player ship in the new port) whenever the shown city changes.
- No control of the panel takes keyboard focus: every `Button` and the `DropdownField` get `focusable = false`, as the existing buttons do.
- `docs/economy.md` is not changed (the user's decision). `CLAUDE.md` is.
- `Core` contains no `MonoBehaviour` and does not reference `Game`.
- Never hand-write `.meta` files; never edit the generated `.sln` / `.csproj`; exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` and after blocks, XML `<summary>` on public types and members, `ArgumentNullException` on null arguments.
- Commit directly on `main`. Commit messages in French, "Panneau de ville : …", ending with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Review Focus

1. An AI ship listed before the player's in the port: the player's is the one chosen. Test in Task 1 (`Choose_WithoutAChoice_TakesTheFirstPlayerShip_AfterOtherShips`).
2. The chosen ship leaves the port while the panel is open: the next player ship is chosen, or none. Test in Task 1 (`Choose_WhenTheChosenShipLeft_TakesTheFirstOtherPlayerShip`, `Choose_WhenNoPlayerShipIsLeft_ChoosesNone`).
3. An AI ship is selected in the ships tab: it never becomes the trading ship. Test in Task 1 (`Choose_NeverKeepsAShipThatIsNotThePlayers`).
4. A click on the open drop-down menu, or on a tab, must not reach the map (no city deselected, no order given). Checked by hand in Task 3, Step 7.
5. Going straight from one city to another with "Bâtiments" open: the tab stays, the ship list is the new port's. Checked by hand in Task 3, Step 7.

## Commands

The Unity Editor is normally **open** on this project. Use the `unity-mcp` tools: `Unity_RunCommand` to refresh the asset database and to run the EditMode tests, `Unity_GetConsoleLogs` to read compile errors.

Refresh and compile after every file change, before the tests:

```csharp
UnityEditor.AssetDatabase.Refresh();
```

Run EditMode tests through `Unity_RunCommand`, after checking `EditorApplication.isCompiling` and `EditorUtility.scriptCompilationFailed`:

```csharp
var api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
    new UnityEditor.TestTools.TestRunner.Api.Filter
    {
        testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
        groupNames = null, // or new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.TradingShipChoiceTests" }
    }));
```

The run writes `C:\Users\Spellman\AppData\LocalLow\DefaultCompany\Dark Fantasy Merchant\TestResults.xml`: read its `<test-run>` line and the `<test-case ... result="Failed">` entries.

When the Editor is closed, use the batch-mode commands of `CLAUDE.md` ("Commands").

---

### Task 1: The rule of the trading ship

**Files:**
- Create: `Assets/Scripts/Core/TradingShipChoice.cs`
- Test: `Assets/Tests/EditMode/TradingShipChoiceTests.cs`

**Interfaces:**
- Produces:
  - `public static class TradingShipChoice` (namespace `DarkFantasyMerchant.Core`)
  - `public static void PlayerShipsAmong<TShip>(IReadOnlyList<TShip> shipsInPort, Predicate<TShip> isPlayerShip, List<TShip> results) where TShip : class` — clears `results`, then adds the player's ships of `shipsInPort` in their order.
  - `public static TShip Choose<TShip>(IReadOnlyList<TShip> shipsInPort, Predicate<TShip> isPlayerShip, TShip current) where TShip : class` — `current` when it is in `shipsInPort` (by reference) and the player's; otherwise the first player ship of `shipsInPort`; otherwise null.
  - Both throw `ArgumentNullException` for a null list, predicate or results.

- [ ] **Step 1: Write the failing tests** in `TradingShipChoiceTests` (namespace `DarkFantasyMerchant.Tests.EditMode`). Ships are plain `object`s; `isPlayerShip` is `s => playerShips.Contains(s)` over a `List<object>`.
  - `PlayerShipsAmong_KeepsOnlyThePlayersShips_InTheirOrder`: port `[ai1, p1, ai2, p2]` → `[p1, p2]`.
  - `PlayerShipsAmong_ClearsTheResultsFirst`: results holding `ai1` beforehand, port `[p1]` → `[p1]`.
  - `Choose_WithoutAChoice_TakesTheFirstPlayerShip_AfterOtherShips`: port `[ai1, p1, p2]`, current null → `p1`.
  - `Choose_KeepsTheCurrentShip_WhileItIsInPort`: port `[p1, p2]`, current `p2` → `p2`.
  - `Choose_WhenTheChosenShipLeft_TakesTheFirstOtherPlayerShip`: port `[ai1, p2]`, current `p1` → `p2`.
  - `Choose_WhenNoPlayerShipIsLeft_ChoosesNone`: port `[ai1]`, current `p1` → null.
  - `Choose_InAnEmptyPort_ChoosesNone`: port `[]`, current null → null.
  - `Choose_NeverKeepsAShipThatIsNotThePlayers`: port `[ai1, p1]`, current `ai1` → `p1`.
  - `Choose_ThrowsOnANullList` and `PlayerShipsAmong_ThrowsOnNullResults`: `Assert.Throws<ArgumentNullException>`.

- [ ] **Step 2: Refresh and run the fixture.** Expected: compile error, `TradingShipChoice` does not exist.

- [ ] **Step 3: Implement `TradingShipChoice`** with the two methods above. Its `<summary>`: the player ships of a port that can trade from the city panel, and the one that trades: the one already chosen while it is in port, otherwise the first.

- [ ] **Step 4: Refresh and run the fixture.** Expected: all 10 pass. Run the whole EditMode suite: no other failure.

- [ ] **Step 5: Commit** `TradingShipChoice.cs`, its test and their `.meta` files: "Panneau de ville : règle du navire qui commerce".

---

### Task 2: The panel in two parts, with tabs

The behaviour does not change yet: the market still trades with the ship selected in the ships list, which moves into the "Navires au port" tab.

**Files:**
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uxml`
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uss`
- Modify: `Assets/Scripts/Game/CityInfoPanelController.cs`
- Test: `Assets/Tests/EditMode/CityInfoPanelControllerTests.cs`

**Interfaces:**
- Produces (in `CityInfoPanelController`):
  - `public static string ShipsTabText(int shipCount)` — "Navires au port (3)".
  - `private enum Tab { Market, Buildings, Ships }`, `private Tab openTab`, `private void ShowTab(Tab tab)`.
  - UXML names, used by Task 3: `ship-line` (an empty row at the top of `market-section`, before the header row, holding `market-hold` for now), `market-hold` (unchanged label), `tab-ships` (the ships tab's button).

- [ ] **Step 1: Write the failing test** in `CityInfoPanelControllerTests`:
  - `ShipsTabText_CountsTheShipsInPort`: `ShipsTabText(3)` is `"Navires au port (3)"`, `ShipsTabText(0)` is `"Navires au port (0)"`.

- [ ] **Step 2: Refresh and run it.** Expected: compile error, `ShipsTabText` does not exist.

- [ ] **Step 3: Restructure `CityInfoPanel.uxml`.** Inside `city-panel` (class `info-panel city-panel`):
  - the existing `info-panel__header` (name, close button), unchanged;
  - `city-panel__body` (row) with:
    - `city-panel__city` (left column, `ScrollView` named `city-scroll` so that a long description scrolls): `city-size`, `city-access`, `city-population`, `city-description`, the two production titles and labels — the same elements and names as today;
    - `city-panel__tabs-side` (right column): a row `city-panel__tab-bar` of three `Button`s `tab-market` ("Marché"), `tab-buildings` ("Bâtiments"), `tab-ships` (text written by the controller), class `city-panel__tab`; then three pages, class `city-panel__page`: `market-page` holding `market-section`, `buildings-page` holding `buildings-section`, `ships-page` holding `no-ship-label` and `ship-list` inside a `ScrollView`.
  - `market-section`: the "Marché" title line goes; `ship-line` (row, class `market__ship-line`) holds `market-hold`; then the header row and `market__scroll` as today.
  - `buildings-section`: the "Vos bâtiments" title goes; scroll and costs label as today.
  - The "Navires au port" section title goes (the tab names it).

- [ ] **Step 4: Restyle `CityInfoPanel.uss`.**
  - `.city-panel`: `width: 900px; height: 620px;`, no more `flex-shrink: 1` nor `margin: 16px 0`. Delete the shrink rules (`.city-panel > *`, `.city-panel > .buildings, .city-panel > .market`, `.buildings > *, .market > *`) and their comment; replace `.market > .market__scroll` (max/min height) and `.buildings > .buildings__scroll` (min height) by `flex-grow: 1; flex-shrink: 1; min-height: 0;` — the page's height is what limits them now.
  - `.city-panel__body`: `flex-direction: row; flex-grow: 1; min-height: 0;` with the header's border line under the header (`border-bottom` of `info-panel__header` in this panel only, through `.city-panel > .info-panel__header`).
  - `.city-panel__city`: `width: 260px; flex-shrink: 0; padding-right: 12px; border-right-width: 1px; border-right-color: rgb(110, 84, 52);`
  - `.city-panel__tabs-side`: `flex-grow: 1; min-width: 0; padding-left: 12px;`
  - `.city-panel__tab`: the colors of `.city-panel__ship` (background `rgba(60, 44, 30, 0.9)`, border `rgb(110, 84, 52)`, text `rgb(222, 210, 188)`), font 15px; `.city-panel__tab--open`: bold, text `rgb(240, 226, 196)`, background `rgb(110, 84, 52)`.
  - `.city-panel__page`: `flex-grow: 1; min-height: 0; margin-top: 8px;` `.market`, `.buildings`: `flex-grow: 1; min-height: 0;`.
  - `.market__ship-line`: `flex-direction: row; align-items: center; margin-bottom: 6px;` (`.market__hold` keeps its style). `.market__title` goes.
  - `.city-panel__section` stays for the two production titles; the first one inside the left column has no top margin need beyond what it has.

- [ ] **Step 5: Wire the tabs in `CityInfoPanelController`.**
  - Query the three tab buttons and three pages in `OnEnable` and add them to the missing-element check.
  - Each tab button: `focusable = false`, `clicked += () => ShowTab(…)` (store the delegates to remove them in `OnDisable`, or use named handlers as `OnCloseClicked`).
  - `ShowTab(Tab tab)`: sets `openTab`, shows its page and hides the two others (`style.display`), toggles `city-panel__tab--open` on the buttons.
  - `ShowSelected(city)`: when the panel was hidden (`cityPanel.style.display` resolved to `None`, or a `shownCity` field that is null), call `ShowTab(Tab.Market)` before showing; otherwise keep `openTab`.
  - `ShowShipsIn(city)`: also writes `tabShips.text = ShipsTabText(listedShips.Count)`.
  - Update the class `<summary>`: the panel shows the city on the left and its market, the player's buildings and the ships in port in tabs.

- [ ] **Step 6: Refresh, run the whole EditMode suite.** Expected: `ShipsTabText_CountsTheShipsInPort` passes, no other failure, no error in the console.

- [ ] **Step 7: Look at it in Play mode** (`Assets/Scenes/WorldMap.unity`): select Bruges, check each tab shows its content, the market's rows scroll under their header inside the panel, the panel does not reach the top or bottom of the Game view at 16:9, the ship panel at the bottom left is not covered when the player ship is selected in the ships tab, and trading still works after selecting the player ship there. Capture the Game view (`ScreenCapture.CaptureScreenshot("Logs/city-panel-tabs.png")` through `Unity_RunCommand`, then read the PNG) and adjust sizes if something overflows.

- [ ] **Step 8: Commit** the UXML, USS, controller and test: "Panneau de ville : deux parties et onglets".

---

### Task 3: The market's ship in a drop-down list

**Files:**
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uxml`
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uss`
- Modify: `Assets/Scripts/Game/CityInfoPanelController.cs`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: `TradingShipChoice.PlayerShipsAmong`, `TradingShipChoice.Choose` (Task 1); `ship-line`, `market-hold`, `ShowShipsIn` (Task 2).
- Produces (in `CityInfoPanelController`): `public const string NoTradingShip = "Aucun navire";`, `private Ship tradingShip`, `private readonly List<Ship> tradingShips`, `private void ChooseTradingShip(Ship current)`.

- [ ] **Step 1: Add the drop-down** to `ship-line`, before `market-hold`: a `Label` "Navire :" (class `market__ship-label`) and a `DropdownField` named `trading-ship` (class `market__ship-choice`, about 200 wide, styled like the price buttons: background `rgba(60, 44, 30, 0.9)`, border `rgb(110, 84, 52)`, text `rgb(222, 210, 188)`), and `market-hold` pushed to the right (`margin-left: auto` or `flex-grow` on a spacer).

- [ ] **Step 2: Keep the trading ship in the controller.**
  - Query `trading-ship` in `OnEnable` (missing-element check), `focusable = false`, register `RegisterValueChangedCallback` → `OnTradingShipPicked`, unregister in `OnDisable`.
  - `ChooseTradingShip(Ship current)`: `TradingShipChoice.PlayerShipsAmong(listedShips, shipsView.IsPlayerShip, tradingShips)`, then `tradingShip = TradingShipChoice.Choose(listedShips, shipsView.IsPlayerShip, current)`; fill the drop-down with `SetValueWithoutNotify` and `choices` = the ships' `DisplayNameOf` (or `[NoTradingShip]`, `SetEnabled(false)` when `tradingShips` is empty, enabled otherwise), its index that of `tradingShip`. Without `shipsView`, `tradingShip` is null and the list reads "Aucun navire".
  - `ShowShipsIn(city)` ends with `ChooseTradingShip(…)`: `null` when the shown city changed (`ShowSelected`), `tradingShip` otherwise (`OnDockingChanged`), so a ship that left is replaced and one that entered is chosen only when none was.
  - `OnTradingShipPicked`: `tradingShip = tradingShips[dropdown.index]` (ignore an index out of range), then `ShowMarketOf(selected city)`. It does not touch `shipsView.Selection`.
  - `OnShipSelected(ship)`: when `ship` is the player's and in `listedShips`, `ChooseTradingShip(ship)` before re-showing the market; any other selection leaves `tradingShip` as it is. Keep `HighlightSelectedShip()`.
  - Replace `HoldOfSelectedShipInPort()` by `HoldOfTradingShip()`: `tradingShip?.Cargo`; `ShowMarketOf` gives it to both sections. Update the comments that speak of the selected ship (the hold "is the trading ship's").

- [ ] **Step 3: Refresh, run the whole EditMode suite.** Expected: all pass, no console error.

- [ ] **Step 4: Check the open menu is part of the panel.** In Play mode, open the drop-down over the map and click an item that lies outside `city-panel`'s rectangle: the city must stay selected and no order is given. If the map takes the click, make `IsPointerOverUi` also return true while the menu is open (the `DropdownField`'s menu is a `GenericDropdownMenu` added to the panel's root: `root.panel.Pick` should find it; fix only if it does not).

- [ ] **Step 5: Check the keyboard.** After picking a ship, the arrow keys / WASD still pan the map and do not move a focus ring in the panel.

- [ ] **Step 6: Update `CLAUDE.md`.**
  - World map: the panel is fixed, about 900 × 620 at the reference resolution, city on the left, three tabs on the right, only a tab's content scrolls; it opens on the market tab and keeps the open tab from one city to the next. Delete the sentences on shrinking to the screen and `.city-panel > *`.
  - Ports: the ships in port are listed in the "Navires au port" tab, with their count in its title; clicking a player ship there also makes it the market's ship.
  - Economy (market paragraph): the hold that trades is the one of the ship chosen in the market tab's drop-down list (`TradingShipChoice`: the chosen one while it is in port, otherwise the first player ship in port, reset on a new city; "Aucun navire" and a read-only market when there is none); choosing there does not select the ship on the map. The "When a ship of the player in port is selected in the panel's list" sentence goes.
  - Population ("Vos bâtiments" paragraph): it is the "Bâtiments" tab; materials come from the hold of the market's ship.

- [ ] **Step 7: Look at it in Play mode** and capture the Game view as in Task 2:
  - Bruges at the start: the market tab is open, the player ship is in the list, the hold column shows and a price click buys, with no click before.
  - Send the ship to another city, select that city when it has docked: the list shows it at once.
  - With "Bâtiments" open, click another city on the map: "Bâtiments" stays open, the list and the ships tab are the new port's. Close the panel, reopen: "Marché".
  - A city with no player ship: "Aucun navire", disabled, prices plain text.
  - Ships tab: clicking an AI ship opens its panel and leaves the market's ship unchanged; clicking the player's ship keeps it chosen.

- [ ] **Step 8: Commit** the UXML, USS, controller and `CLAUDE.md`: "Panneau de ville : choix du navire dans l'onglet Marché".
