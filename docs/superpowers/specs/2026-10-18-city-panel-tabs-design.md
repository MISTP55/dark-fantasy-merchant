# City Panel in Two Parts, with Tabs — Design

Date: 2026-10-18
Status: approved by the user

## Purpose

The city panel shows too much on one screen. It is one column (the city, its
productions, the ships in port, the player's buildings, the market) that takes
the whole height of the screen, and two of its sections, the buildings and the
market, scroll inside it. The player opens it mostly to look at the market.

The panel is split in two: the city on the left, always visible, and three tabs
on the right, "Marché", "Bâtiments" and "Navires au port". The market tab gets
its own choice of the player's ship that trades, so that trading no longer
starts by clicking a ship in a list.

## Decided with the user

- **Left part**: the city's name, size, access (sea or river port), population,
  description, and its efficient and inefficient productions. Same texts as
  today.
- **Right part**: three tabs, in this order: "Marché", "Bâtiments", "Navires au
  port".
- **Market tab**: a line above the table with a drop-down list of the player's
  ships in the city's port, which picks the ship that buys and sells, and that
  ship's hold beside it ("Cale : 120 / 200 tonneaux", the text of
  `ShipInfoPanelController.FormatCargo`). The table, its columns, its icons and
  its trading (click on a price, 1 unit, Shift 10, Ctrl 100, prices that are
  disabled buttons when a trade is impossible) are unchanged. This is option A
  of the mockups (`.superpowers/brainstorm/…/layout.html`).
- **The ship is chosen at once**: when a ship of the player lies in the port, it
  is the one in the list as soon as the panel opens. Selecting a ship before
  trading is no longer needed.
- **No ship of the player in port**: the list reads "Aucun navire" and is
  disabled, the hold text is empty, and the market is only read (no hold
  column, prices disabled), as today without a ship.
- **Buildings tab**: the content of today's "Vos bâtiments" section, unchanged.
  The materials of a building come first from the hold of the ship chosen in the
  market tab, then from the market, as today with the selected ship.
- **Ships in port tab**: every ship in the port, the player's and the AI
  merchants', as today's list ("Aucun navire au port" when there is none).
  Clicking a row selects the ship as today: its panel opens, a right click
  orders it out, clicking it again deselects it, the selected row is
  highlighted.
- **Size**: the panel has a fixed size, well under the height of the screen;
  only the content of a tab scrolls when it does not fit.
- **Opening**: the panel opens on the market tab, every time it opens (from no
  city selected to a city).
- **Going from one city to another**: when another city is selected while the
  panel is open (a click on the map around the panel), the tab that was open
  stays open, so that the player can compare cities. Only closing the panel
  (no city selected) brings it back to the market tab.

## Calls made by Claude (to be checked by the user)

- **The market's ship and the map's selected ship are two things.** Choosing a
  ship in the drop-down list does not select it on the map and opens no ship
  panel. Clicking one of the player's ships in the "Navires au port" tab, on the
  other hand, also makes it the market's ship. Clicking an AI ship changes
  nothing in the market.
- **Which ship is chosen at first**: the first ship of the player among the
  ships in the port, in the order `ShipDocking.GetShipsIn` gives them. The list
  names the ships with `ShipsView.DisplayNameOf`.
- **Ships that come and go while the panel is open**: when the chosen ship
  leaves the port (ordered out or removed), the first other ship of the player
  in port is chosen, or none. When a ship of the player enters the port while
  none is chosen, it is chosen. A ship entering while one is chosen is only
  added to the list.
- **A new city resets the choice**: the panel shows the first ship of the
  player in the new city's port, it keeps no memory per city.
- **Tab title with a count**: "Navires au port (3)", the number of ships in
  port, kept up to date, so that the player sees there are ships without opening
  the tab. The other tabs have no count.
- **Size**: about 900 × 620 units at the panel settings' 1920 × 1080 reference
  (scale with screen size), the left part about 260 wide. Centered as today: at
  that width it leaves the ship panel, at the bottom left, uncovered. The
  market's twenty rows of 32 units do not all fit; they scroll under their
  header, which stays in place.
- **Header across the whole panel**: as in mockup A, the city's name and the
  close button stay in a header band above both parts, with the separating line
  under it; the left part starts with the size and access.
- **The old section titles go**: "Marché" and "Vos bâtiments" are the tabs'
  names; the market's title line with the hold label becomes the ship line.

## Goals

- The player sees the city and the market together on opening, without
  scrolling the panel itself.
- Trading needs no click before the first price.
- Nothing that works today is lost: every text, button and event of the panel
  keeps its behaviour, only its place changes.

## Non-goals

- No new information in the panel, no change to the market's columns or to the
  buildings' rows.
- No second ship for the player: the list is for later, the player still has
  one ship.
- No change to the ship panel, the HUDs or the debug panel.
- No remembered tab between openings (the open tab is kept only from one city
  to the next while the panel stays open).

## Anti-goals

- The keyboard pans the map: no control of the panel may take keyboard focus in
  a way that stops the map from panning (today's buttons are not focusable for
  that reason). The drop-down list must not keep the keyboard after a choice.
- A click on the panel, the tabs or the open drop-down list must never reach the
  map (`CityInfoPanelController.IsPointerOverUi` must see them; a UI Toolkit
  drop-down's menu is drawn outside the panel's element, check it is picked).
- The market must not trade with a ship that is not in the city's port or is
  not the player's.

## Constraints

- UI Toolkit (UXML + USS), as all the runtime UI; texts in French; the colors,
  frame and close button of `InfoPanel.uss` and `CityInfoPanel.uss`.
- `CityMarketSection` and `CityBuildingsSection` already take the hold to show
  or use (`Show(market, hold)`): the change is which hold the controller gives
  them, not how they work.
- The ship selection rules of `WorldMapInteraction` (a ship in port is selected
  together with its city, cleared when another city or none is selected) stay
  as they are.
- `WorldMapSetup` must still build a working scene: the UXML is an asset it
  references, nothing new to wire is expected.
- `CLAUDE.md` describes the panel in several places (World map: its size and
  what shrinks; Ports: the ship list; Economy: the market and the selected ship;
  Population: the buildings section): update them. `docs/economy.md` is not
  changed: this is UI work only, nothing changes in the economy (the user's
  decision).
- EditMode tests: the rule that picks the market's ship (first player ship in
  port, what happens when it leaves or another enters) is plain logic and should
  be tested without a scene, as the conventions ask.

## Left to the builder

- Whether the tabs are UI Toolkit's `TabView` or buttons that show one
  container at a time, as long as they match the panel's style.
- Whether the drop-down is a `DropdownField` or a button with a menu of its own,
  within the anti-goals above.
- Where the market-ship rule lives (a small `Core` class, or a static method of
  the controller that tests can reach, like `ProducedGoodsText`).
- Whether the left part scrolls when a long description does not fit.
- The exact sizes and spacing, checked in the Editor at 16:9.
