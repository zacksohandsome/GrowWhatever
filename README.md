# GrowWhatever

A small, single-player fantasy farming game built with Unity. Start with carrots and wheat, mill flour, research plants that grow iron, and use Moonflowers to speed up neighboring plots.

## Play

Open `Assets/Scenes/Farm.unity` in Unity **6000.3.27f2** and press Play. The project uses the packages locked in `Packages/packages-lock.json`.

The local Windows build is generated into `Builds/GrowWhatever-Windows/`. Run `GrowWhatever.exe` from that folder and keep its `_Data`, `MonoBleedingEdge` and other adjacent player files together. Build output is excluded from Git; a source clone does not include an executable.

- Choose a seed in **Seed shop**, then click an empty plot to plant. Buying and planting are separate: planting consumes one owned seed.
- Click a mature plant to harvest. **Plant all** fills empty plots in row order; **Harvest all** only collects mature plots whose bulk setting is ON.
- Sell seeds back at their purchase price, or sell produce for coins. Keep the materials shown in **Genetic Lab** for research.
- **Workshop** accepts one active job and three waiting jobs. Ordering pays materials immediately; completed goods enter inventory automatically.
- Research Iron sprout, then Moonflower. Buy either permanent upgrade, actually use a Moonflower's neighbor boost, and manually harvest Moonflower essence to complete the first farm goal.
- Moonflowers boost orthogonally adjacent non-Moonflowers by 25%. Their effect remains after maturity. Switch a Moonflower plot's bulk setting OFF to keep it while harvesting other plots.
- **Save** writes the local farm. **New farm** opens a confirmation before replacing it. **Free seed** becomes available only when no usable or pending resources remain and the cheapest unlocked seed is unaffordable.

The first farm goal is a progression milestone, not an ending: you can continue playing afterwards. There is no automatic planting, harvesting or replanting.

## Saves and offline progress

There is one local JSON save, `farm-save-v1.json`, under Unity's persistent-data directory for company/product `GrowWhatever`. Saves occur after state-changing commands, processing completion, focus/pause transitions, every 30 seconds, and on normal exit.

Existing crops and queued processing advance while away using the same rules as online play. Mature plants wait for harvest; the finite queue eventually empties. No additional work is created offline. Negative system-clock differences count as zero; clock manipulation is not prevented.

A damaged, unsupported or inconsistent save is preserved and gameplay is blocked until you explicitly choose to start a new farm. Save-write failures are displayed in the interface; you can retry Save.

## Project structure and validation

Gameplay rules live in engine-independent C# under `Assets/Scripts/Core`. Runtime components bind those rules to saved scene/Prefab assets and ScriptableObject settings. See [architecture](docs/design/architecture.md), [game specification](docs/design/game-design.md), [balance](docs/design/balance.md) and [asset sources](docs/design/asset-sources.md).

The current version passes 20 EditMode tests. A normal-resource, real-time Windows QA playthrough reached the first farm goal in **27 minutes 32 seconds**; offline restart was tested separately. See [validation and limitations](docs/design/verification.md).

Run the `GrowWhatever.EditModeTests` assembly in Unity's Test Runner for rules, progression and persistence tests. `Tools/Development` contains authoring and verification scripts for the connected Editor. QA automation is compiled only for Editor/Development builds and is excluded from the regular Windows build.

This is a single-scene desktop first version with four growables, two recipes, two research projects and two single-level upgrades. The interface currently uses English. There is no combat, daily-task system, automatic farming, cloud save, Steam integration or commercial release commitment.

Before Git work, follow [project safety checks](Tools/Security/README.md). Third-party assets retain their own terms; see [asset sources](docs/design/asset-sources.md).
