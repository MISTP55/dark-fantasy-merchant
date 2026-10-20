---
paths:
  - "Assets/Scripts/*/AiMerchants/**"
  - "Assets/Tests/EditMode/AiMerchants/**"
  - "Assets/Data/AI/**"
---

# AI merchants

- Their gold is unlimited: they have no treasury and pay no wages, and only their cumulated profit is counted, for the debug tool.
- `AiMerchants` only wires the Core classes to the scene. It measures the travel times between the cities over several frames (`PortTravelTable`, one for all ships since they share a definition and a crew) before it spawns the ships: the AI ships appear a moment after the game starts. A city that reaches no other is warned about and ignored: most likely a mistake of the mask.
- `AiMerchantFleet<TPort>` Core, listens to `ShipDocking`. `SupplyPlanner` and `ShipmentQuantity` decide what to carry where.
- What a ship loads for a port is recorded as under way to it until it enters that port (`UnderWayTo`). Every ship's records count in every decision, the deciding ship's own included, so that it does not buy again what it carries and two ships do not supply the same lack.
- At the start every city holds the same days of every good, so no trade pays: the AI ships wait in port for the first days. This is expected.
