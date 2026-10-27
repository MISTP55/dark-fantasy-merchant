# Trading post — design

## Intent

The player is a merchant, and a merchant needs somewhere to keep goods ashore. This work gives the player a **trading post** ("Comptoir" in the game): a building of their own in a city, holding an inventory of goods. A ship entering a port can leave its cargo there and the player can wait for the city's prices to rise before selling; the trading post buys and sells with the city's market like a ship does, without a ship in port. In coming work, the player's workshops will deliver what they produce into the trading post of their city, so the inventory is the hub of the player's business in a city *(the user's intent)*.

Every city keeps one fixed, reserved place for it, designed by hand in the City Layout window, so that the player's trading post always stands in a sensible spot of each city and nothing the city builds can ever take that spot.

## Goals

### The building

- A new building, the **trading post**, displayed "Comptoir": **2×3** cells, **one block high** *(the user's decision)*. It is drawn in the city view with the existing 2×3 placeholder block (a footprint the view already draws), tinted with its own colour *(builder's call: a new `tradingPostColor` on `CityGridView`, like `portColor`)*.
- Its definition is an asset of the buildings catalogue (`Assets/Data/Buildings/`), like the port and the town hall, made by the scene setup when missing: name "Comptoir", footprint 2×3, cost **20 000 gold, 50 Metal, 100 Timber** *(the user's decision; values live in the asset)*, capacity, rent (below).
- **Only the player** builds a trading post *(the user's decision)*. The cities never do, nor do the AI merchants.
- **At most one per city** *(the user's decision)*, and only on the city's reserved site.
- It needs **no road** *(the user's decision)*. Like the port, it is not part of the road network: not a root, a road touching it does not become a network road, never darkened as unconnected *(builder's call, matching the port)*. It has no room and no jobs.
- The player's **home city** has its trading post from the start of the game, standing on its reserved site *(the user's decision)*. It is given, not built: nothing is paid, no notification is posted, and it starts empty. The home city is the one `PlayerStartDefinition.HomeCityOn` returns, its fallback included *(builder's call)*.

### The reserved site

- Every city's layout (`CityLayoutDefinition`) holds one **reserved site**: an origin and an orientation (`Along` or `Across`) for the 2×3 footprint *(the user's decision: one fixed site per city)*.
- It is placed in the **City Layout window**, by a new tool after Berth, "Trading post site": ghost of the footprint, green where it can stand and red where not, **R** turns it between its two orientations, left click places it (or moves it), right click on it removes it, as the town hall tool does.
- It is **required**: a layout without a site, or whose site cannot stand, is **unusable**, exactly as one without a port (`TryCreateGrid` fails; `WorldMapView.IsUsable` false; `CityViewContentTests` checks every city) *(the user's decision)*.
- It must stand on **land**, inside the grid, **inside the walls** (every cell enclosed) *(the user's decision)*, and on no other building. `TryValidateWalls` (or the check that follows it) reports a site outside the walls.
- **Nothing but the trading post** may cover a reserved cell *(the user's decision)*: no house (the player's or the city's), no workshop, no road (traced by the player, planned by `RoadPlanner`, or generated), no wall, no town hall, no port. The reservation must go through the single placement test (`IsBuildable` / `CanPlace`, which every strategy and `RoadPlanner` use) so that no path forgets it. In the layout window, the other tools skip reserved cells as they skip the port; every edit carries the site, and resizing drops a site that falls outside or no longer stands.
- The window warns when the layout has no site, as it does for a missing port. Its ghost checks the grid, the land and the other buildings; being outside the walls only raises the window's walls warning, since the layout's grids do not keep inside the walls while they are edited *(builder's call)*.
- **In game the site is invisible** *(the user's decision)*: the reserved cells look like any free land. They are highlighted only while the player is placing the trading post (below).

### Generated layouts

The project's rule is that a change to what makes a layout usable comes with the regeneration of every city's layout, never with a patch of the existing ones nor a layout left for the designer to fix. So:

- `StartingLayout.TryBuild` / `CityLayoutEditing.TryGenerate` places the site too, **after the walls and before the houses and their roads**, near the town hall, inside the walls, on free land, so the houses and roads are laid around it *(builder's call; the exact search — e.g. the nearest place to the town hall by a fixed order — is the builder's)*. Generation fails, with a clear problem, when no site fits.
- Every city's layout is regenerated (`CityLayoutRegeneration.Run`) as part of this work. This redraws the cities' houses and roads too *(accepted by the user)*.

### Building it in the city view

- The build bar gets an offer for the trading post *(builder's call: in its own category, or with the other offers; one button "Comptoir")*, quoted through `PlayerBuilder` like the others, with its cost tip.
- When the player already has a trading post in the city, the offer is **disabled** and its tip says so *(builder's call)*.
- Choosing the offer **highlights the reserved cells** *(the user's decision)* and shows the ghost **on the site**, in the site's orientation: it does not follow the pointer and R does nothing *(builder's call: there is only one place it can go)*. A left click on a highlighted cell builds it; Cancel stops placing and removes the highlight. The ghost is green when the quote is possible, red otherwise.
- Building pays like any other construction: materials that are not active goods are left out (`BuildingDefinition.MaterialsOf`), the gold is taken from the treasury, all or nothing, and the player gets the usual notification ("Construction achevée à Lübeck : Comptoir."). It is built at once, as the other buildings are today.
- The debug tools' free building applies to it as to the others.

### The inventory

- A trading post holds goods, counted in the good's unit, like a hold. A bushel takes `barrelsPerBushel` barrels of room, as in a hold *(accepted by the user)*.
- It has a **capacity of 2 000 barrels** *(the user's decision; an asset value)*, but its storage is **unlimited**: it can always take more *(the user's decision)*.
- Above the capacity, the player pays a **rent of 1 gold per barrel per week** *(the user's decision; an asset value)*. It is a new amount of the weekly bill (`WeeklyBill`), summed over every trading post of the player, on the barrels above capacity **on the day the week is billed** *(accepted by the user)*. Like the wages, it is taken whatever the gold left (`Treasury.Withdraw`).
- The inventory is mutable runtime state in Core, never written into an asset; it is covered by EditMode tests (storage, room counted in barrels, rent, trades, transfers, construction materials).

### The city panel: Market tab

- The drop-down, now read "Avec :", lists the player's ships **docked** in the city, sorted by name, then **"Comptoir"** when the player has a trading post in the city *(the user's decision, option B of the mockups)*.
- With "Comptoir" chosen, the last column is "Comptoir" and shows the units in the trading post; next to the drop-down, its fill reads "1 850 / 2 000 tonneaux" *(accepted by the user)*. Above capacity, the fill is drawn in another colour and a tooltip gives the rent of the coming week *(accepted by the user)*.
- The prices trade as they do today with a ship, the modifier keys included (same units per click), the treasury paying and being paid. Buying into the trading post is **never limited by room**; it is still limited by the city's stock and by the gold.
- The player can trade with the trading post **with no ship in port**: the drop-down then opens on "Comptoir" *(accepted by the user)*. With a ship docked, it opens on the first ship, as today *(builder's call)*. With no ship and no trading post, the market reads as it does today without a ship.
- The debug tools' record of the player's trades (`SimulationRecorder.OnPlayerTraded`) takes the trades made with the trading post as well as those made with a ship.

### The city panel: Transfer tab

- A new tab **"Transfert"**, between "Marché" and "Navires", present only when the player has a trading post in the shown city *(the user's decision, option B)*.
- Its own drop-down, "Navire :", lists the player's ships docked in the city, sorted by name; with none, it reads "Aucun navire" and the list is empty.
- One row per good present **in the hold or in the trading post**, sorted by name (`NameOrder`), striped (`ListStripes`): good, units in the hold, an arrow **→** (hold to trading post), an arrow **←** (trading post to hold), units in the trading post *(accepted by the user)*.
- The arrows move the same units per click as the prices, modifier keys included *(accepted by the user)*. **→** is never limited by room; **←** stops when the hold is full. No gold changes hands.
- Labels are rewritten on the events that change them (the hold's, the trading post's and the docking's `Changed`/state events), not every frame.

### Construction materials

- When the player builds in a city where they have a trading post, materials are taken **first from the trading post**, then from the hold of the ship docked in the city, and the rest is bought from the market *(accepted by the user)*. This applies to every construction of the player (houses, workshops, roads); the trading post itself draws only from the hold and the market, since the city has none yet. The quote follows the same order.

## Non-goals

- The player's workshops delivering into the trading post: the next feature, not this one *(the user's statement)*.
- Trading posts for the AI merchants or the cities.
- Raising the capacity (upgrades, more storage): the capacity is an asset value, fixed for now.
- Automatic orders (keep a stock, sell above a price) and any trading-post administration.
- Moving goods between the trading posts of two cities without a ship.
- Construction delays, demolition, or selling a trading post.
- Showing the reserved site in game outside placement.
- Saving and loading: the game has no save yet.

## Anti-goals

- A reserved cell taken by anything but the trading post, by any path (the city's houses, `RoadPlanner`'s roads, the player's road trace). This would silently make a city's trading post impossible to build.
- A layout patched by hand to get its site, or left unusable: every layout is regenerated.
- Buying into the trading post blocked by its capacity: the capacity only sets the rent.
- A transfer that creates or destroys goods, or touches the treasury.

## Constraints

- Follow the project's `CLAUDE.md` and rules: Core holds the logic and state (no `MonoBehaviour`), Game the assets and thin adapters; the new scene objects, assets and references are added to `WorldMapSetup` / `CityViewSetup` so an existing scene gets them; everything the player reads is in French; lists sorted with `NameOrder` and striped with `ListStripes`; good names never followed by their unit; numbers with a no-break space for thousands; UI Toolkit only, on the shared `PanelSettings`.
- No mention of the game this one draws inspiration from anywhere in the repository.
- New rules for the subsystems touched are written in `.claude/rules/` (the trading post's place, its inventory, the reserved site).

## Left to the builder

- The names of types and members (e.g. `TradingPost`, `TradingPostDefinition`, `TradingPostSite`), the subsystem folder the inventory lives in (Economy is the natural one), and how the inventory and the hold share `Trade` (an interface over both, or overloads).
- How the reserved cells are kept in `CityGrid` and how the trading post is placed there (like `PlacePort`, a dedicated method, is suggested).
- Where the player's trading posts live at runtime and how `SimulationServices` reaches them.
- The highlight colour of the reserved cells while placing.
- The generation's search for a site, as long as it is deterministic for a seed and inside the walls.
- What the debug tools show of the trading posts, if anything.
