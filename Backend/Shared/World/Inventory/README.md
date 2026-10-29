# Inventory runtime

`ItemCatalogDocument` is the on-disk DTO for `Backend/Shared/GameData/items_catalog.json`.
It contains `Version`, `Items[]` and `Loot` (counts, group weights, independent supply rolls).
The shipped `gdd-v0.3-proposal` profile is **provisional**, including Shotgun-only gun enablement.
`CreateBaseline()` is an explicit fixture/demo profile; a production adapter must not silently
substitute it when loading a malformed or missing file.

Server loader: System.Text.Json with `IncludeFields=true` and `JsonStringEnumConverter`.
Unity loader: Newtonsoft JSON (string enums are not supported reliably by JsonUtility).
Build the catalog with `document.Build()` and sampler with `new LootTable(catalog, document.Loot)`.
The runtime has no Unity or JSON-library dependency.

All inventory commands run on the authoritative simulation thread. Bind `byte actor` to the
validated session in the adapter, never accept actor identity from the request payload. Pass the
server's time and position. Requests use one monotonic nonzero `uint RequestId` sequence across
pickup/drop/select/use for each actor per match. Duplicate and older requests are rejected with
constant-size per-actor high-water state; retransmissions must re-read current state rather than
expect a cached success response. Replace the whole service on a new MatchId.

`IItemWorldPort` owns floor items and obtains `ushort` IDs from the enclosing world registry;
zero is invalid. `MemoryItemWorld(Func<ushort>)` is a supplied implementation, not an ID allocator.
The enclosing registry must not wrap IDs or reuse a live ID. Death-drop rolls back already spawned
floor entries if allocation is exhausted, leaving inventory intact for an explicit retry.

`IItemGameplayPort` must be implemented by authoritative health/effect services. Its synchronous
callbacks must not throw or re-enter inventory mutation. Inventory requests healing/effects/rescue;
it never writes HP itself. Combat calls `TryConsumeLifePassive` **before** committing death and
honors the returned rescue request (50HP and 2s damage immunity in this profile). Do not call
`DeathDrop` for a rescued player. Once-per-life passive use is independent of packet retries.

Provisional ELIXIR policy: immediate cleanse/immunity takes effect at button press, so the item
is consumed at that point. Cancelling its subsequent heal with movement does not refund the item.
This differs from delayed healing items (cancel retains item) and prevents unlimited free cleanses.
Record this as an open product decision; it is not a newly approved final GDD rule. Stun cannot
cancel ELIXIR; Root and Reverse do not block normal item use. Ordinary damage does not cancel use.

`ItemStack.Ammo` is reserve ammunition belonging to its gun, not a globally shared ammo resource.
One floor AMMO unit adds one magazine. Loaded magazines/reload/fire validation and durability
depletion remain WPN-02 responsibilities; they are not claimed implemented by this inventory layer.

Tests: from repository root, `dotnet run --project Backend/Tests/InventoryChecks/InventoryChecks.csproj`.
The suite checks source/disabled loot rules across 100 seeds, data validation and defensive copies,
two actors contesting loot, bounded dedupe and wrong match, carry/swap/ammo/armor, timer cancellation,
ELIXIR/Root behavior, passive two-hit consumption and rollback/retry of death-drop capacity failures.
