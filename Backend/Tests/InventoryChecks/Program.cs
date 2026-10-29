using JungleMaze.World.Inventory;
using System.Text.Json;
using System.Text.Json.Serialization;

var jsonOptions = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
if (args.Contains("--write-catalog"))
{
    var catalog = ItemCatalog.CreateBaseline();
    var document = new ItemCatalogDocument { Version = catalog.Version, Items = catalog.Items.ToArray() };
    File.WriteAllText(Path.Combine("Backend", "Shared", "GameData", "items_catalog.json"), JsonSerializer.Serialize(document, jsonOptions));
    return 0;
}
var passed = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL " + label); passed++; Console.WriteLine("PASS " + label); }
void Throws(Action action, string label) { var threw = false; try { action(); } catch (ArgumentException) { threw = true; } catch (InvalidOperationException) { threw = true; } Check(threw, label); }
var baseline = ItemCatalog.CreateBaseline();
Check(baseline.Items.Count == 35, "full catalog 18 weapons +16 equipment +ammo");
Check(baseline.Get("HAND").Kind == ItemKind.Unarmed && baseline.Get("HAND").Sources == LootSource.None, "HAND outside inventory and loot");
Check(baseline.Get("ELIXIR").UseWhileControlled && baseline.Get("ELIXIR").EffectAtStart, "Elixir immediate cleanse profile");
Check(!baseline.Get("RUBBER_GUN").Enabled && baseline.Get("SHOTGUN").Enabled, "tentative gun enabled config");
Throws(() => new ItemCatalog("v", new[] { baseline.Get("HAND"), baseline.Get("HAND"), baseline.Get("AMMO") }), "duplicate catalog rejected");
Throws(() => new ItemCatalog("v", new[] { baseline.Get("AMMO") }), "missing HAND rejected");
Throws(() => new ItemDefinition { Id = "BAD", UseSeconds = double.NaN }.Validate(), "NaN rule rejected");
Throws(() => new ItemDefinition { Id = "BAD", Reduction = 1.1f }.Validate(), "invalid armor reduction rejected");
var loot = new LootTable(baseline);
for (uint seed = 1; seed <= 100; seed++)
{
    var chest = loot.Roll(LootSource.Chest, seed); var repeated = loot.Roll(LootSource.Chest, seed);
    if (chest.Length < 3 || chest.Length > 4 || !chest.Select(x => x.ItemId).SequenceEqual(repeated.Select(x => x.ItemId))) throw new Exception("non deterministic drops");
    foreach (var item in chest) if ((baseline.Get(item.ItemId).Sources & LootSource.Chest) == 0 || !baseline.Get(item.ItemId).Enabled) throw new Exception("invalid source");
    var pot = loot.Roll(LootSource.Pot, seed);
    if (pot.Length < 1 || pot.Length > 2 || pot.Any(x => baseline.Get(x.ItemId).MagazineSize > 0)) throw new Exception("invalid pot loot");
}
Check(true, "100 seeds deterministic count/source/disabled rules");
var supply = loot.Roll(LootSource.Supply, 42);
Check(supply.Any(x => x.ItemId == "MACE") && supply.Any(x => x.ItemId == "COMPASS"), "supply guaranteed rare items");
Throws(() => new LootTable(baseline, new LootProfile { ChestGroupWeights = new int[6] }), "infeasible zero-weight source rejected");
var custom = new LootTable(baseline, new LootProfile { ChestMin = 1, ChestMax = 1 });
Check(custom.Roll(LootSource.Chest, 3).Length == 1, "loot counts data configurable");
var docRoundtrip = JsonSerializer.Deserialize<ItemCatalogDocument>(JsonSerializer.Serialize(new ItemCatalogDocument { Version = baseline.Version, Items = baseline.Items.ToArray() }, jsonOptions), jsonOptions);
Check(docRoundtrip.Build().Items.Count == 35, "JSON config roundtrip preserves catalog");
var productionDocument = JsonSerializer.Deserialize<ItemCatalogDocument>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "items_catalog.json")), jsonOptions);
Check(productionDocument.Build().Items.Count == 35, "actual packaged JSON contains all 35 IDs");
Check(new LootTable(productionDocument.Build(), productionDocument.Loot).Roll(LootSource.Chest, 4).Length >= 3, "actual packaged JSON profile validated and sampled");
var leaked = baseline.Get("KNIFE"); leaked.Enabled = false;
Check(baseline.Get("KNIFE").Enabled, "catalog returns defensive item copy");
var mutableDefinition = new ItemDefinition { Id = "TEST", Kind = ItemKind.Weapon, Sources = LootSource.Chest };
var copiedCatalog = new ItemCatalog("v", new[] { baseline.Get("HAND"), baseline.Get("AMMO"), mutableDefinition }); mutableDefinition.Id = "CORRUPT";
Check(copiedCatalog.Get("TEST").Id == "TEST", "catalog owns copied input definitions");
var mutableProfile = new LootProfile(); var copiedLoot = new LootTable(baseline, mutableProfile);
Array.Clear(mutableProfile.ChestGroupWeights); mutableProfile.Supply[0].ItemId = "CORRUPT";
Check(copiedLoot.Roll(LootSource.Chest, 1).Length >= 3 && copiedLoot.Roll(LootSource.Supply, 1).Any(x => x.ItemId == "MACE"), "loot owns profile array and rule copies");

var match = Guid.NewGuid(); ushort next = 1000;
var floor = new MemoryItemWorld(() => ++next); var game = new GameplaySpy();
var service = new InventoryService(match, baseline, game, floor);
service.Register(0); service.Register(1); service.SetActorState(0, 10, 10, true); service.SetActorState(1, 10, 10, true);
uint request = 0;
ushort Spawn(string id, int count = 1, float x = 10, float y = 10, int ammo = 0) { if (!floor.TrySpawn(new ItemStack(id, count, 0, ammo), x, y, out var wid)) throw new Exception("spawn failed"); return wid; }
ItemResult Pick(string id, int count = 1, int ammo = 0) => service.Pickup(match, 0, ++request, Spawn(id, count, ammo: ammo));
Check(service.GetSnapshot(0).ActiveWeapon == -1 && service.GetSnapshot(0).Weapons.All(x => x.IsEmpty), "initial inventory has HAND outside all slots");
var contested = Spawn("KNIFE");
Check(service.Pickup(match, 0, ++request, contested) == ItemResult.Ok, "first pickup succeeds");
Check(service.Pickup(match, 1, 1, contested) == ItemResult.Missing, "second actor cannot duplicate pickup");
Check(service.Pickup(match, 0, request, contested) == ItemResult.Duplicate, "duplicate request rejected without mutation");
Check(service.Pickup(Guid.NewGuid(), 0, request + 1, contested) == ItemResult.WrongMatch, "wrong match rejected");
Check(service.Pickup(match, 0, 0, contested) == ItemResult.Invalid, "zero request rejected");
var far = Spawn("BONE", x: 40);
Check(service.Pickup(match, 0, ++request, far) == ItemResult.OutOfRange, "authoritative pickup radius");
Check(service.Pickup(match, 0, request - 1, far) == ItemResult.Stale, "stale request bounded dedupe");
Check(Pick("BONE") == ItemResult.Ok && Pick("AXE") == ItemResult.Ok && Pick("SHOTGUN") == ItemResult.Ok, "fill four weapon slots");
Check(service.Select(match, 0, ++request, 2) == ItemResult.Ok, "select weapon");
var beforeSwap = floor.Items.Count;
Check(Pick("TREE_BRANCH") == ItemResult.Ok && service.GetSnapshot(0).Weapons[2].ItemId == "TREE_BRANCH", "full weapon bag swaps selected slot");
Check(floor.Items.Count == beforeSwap + 1, "swap drops original exactly once");
Check(service.Select(match, 0, ++request, 2) == ItemResult.Ok && service.GetSnapshot(0).ActiveWeapon == -1, "toggle weapon to unarmed");
Check(Pick("BANANA", 5) == ItemResult.Ok && service.GetSnapshot(0).Weapons[0].Count == 3, "weapon carry capped and unarmed swaps slot zero");
Check(floor.Items.Any(x => x.Stack.ItemId == "BANANA" && x.Stack.Count == 2), "partial pickup keeps remaining floor stack");
Check(Pick("AMMO", 1) == ItemResult.Ok && service.GetSnapshot(0).Weapons[3].Ammo == 4, "ammo adds one magazine to first eligible gun");
Check(Pick("AMMO", 99) == ItemResult.Ok && service.GetSnapshot(0).Weapons[3].Ammo == 20, "ammo reserve capped");
Check(Pick("AMMO") == ItemResult.Full, "full reserves do not consume ammo");
Check(Pick("WOODEN_ARMOR") == ItemResult.Ok && Pick("MAGIC_ARMOR") == ItemResult.Ok, "better armor auto equips and replaces");
Check(Pick("WOODEN_ARMOR") == ItemResult.Rejected, "inferior armor not auto picked");
Check(Pick("GOD_LEAF", 6) == ItemResult.Ok && Pick("SPIRIT_TEAR") == ItemResult.Ok && Pick("MAGIC") == ItemResult.Ok && Pick("ELIXIR", 2) == ItemResult.Ok, "fill four consumable types");
Check(Pick("COMPASS") == ItemResult.Full, "full consumables refuse different type");
Check(service.BeginUse(match, 0, ++request, 0, 1) == ItemResult.Ok && service.IsMovementLocked(0), "use starts and locks movement");
service.CancelForAction(0, ItemCancelAction.Damage);
Check(service.GetSnapshot(0).IsUsing, "damage does not cancel proposed GDD use");
service.CancelForAction(0, ItemCancelAction.Move);
Check(!service.GetSnapshot(0).IsUsing && service.GetSnapshot(0).Consumables[0].Count == 6 && game.Heals == 0, "move cancels without consuming healing item");
Check(service.BeginUse(match, 0, ++request, 0, 2) == ItemResult.Ok, "restart use");
service.Tick(2.49); Check(game.Heals == 0, "heal not early");
service.Tick(2.5); service.Tick(2.5);
Check(game.Heals == 1 && game.HealTotal == 10 && service.GetSnapshot(0).Consumables[0].Count == 5, "complete use consumes and heals once");
service.SetActorState(0, 10, 10, true, ItemControlFlags.Stun);
Check(service.BeginUse(match, 0, ++request, 0, 3) == ItemResult.Controlled, "ordinary medicine rejected during CC");
Check(service.BeginUse(match, 0, ++request, 3, 3) == ItemResult.Ok && game.Effects == 1 && game.LastEffect == ItemEffect.Cleanse, "Elixir cleanses immediately during CC");
service.CancelForAction(0, ItemCancelAction.Stun);
Check(service.GetSnapshot(0).IsUsing, "Elixir immune to stun cancel");
service.CancelForAction(0, ItemCancelAction.Move);
Check(!service.GetSnapshot(0).IsUsing && service.GetSnapshot(0).Consumables[3].Count == 1, "immediate cleanse cannot be repeatedly granted for free");
service.SetActorState(0, 10, 10, true);
Check(service.Drop(match, 0, ++request, 7) == ItemResult.Ok, "drop consumable slot");
Check(Pick("ELIXIR_OF_LIFE") == ItemResult.Ok, "loot passive");
Check(service.TryConsumeLifePassive(0, 10) && !service.TryConsumeLifePassive(0, 10) && !service.TryConsumeLifePassive(0, 11) && game.Rescues == 1, "two hits only consume passive once");
Check(game.RescueHp == 50 && game.RescueSeconds == 2, "passive requests external 50 HP and 2 seconds immortality");
Check(service.GetSnapshot(0).Consumables.All(x => x.ItemId != "ELIXIR_OF_LIFE"), "passive removed");
var snapshot = service.GetSnapshot(0); snapshot.Weapons[1] = default;
Check(!service.GetSnapshot(0).Weapons[1].IsEmpty, "snapshot cannot mutate authoritative state");
var beforeDeath = floor.Items.Count;
Check(service.DeathDrop(0, 1) == ItemResult.Ok, "death drop succeeds");
var afterDeath = floor.Items.Count;
Check(afterDeath > beforeDeath && service.DeathDrop(0, 1) == ItemResult.Duplicate && floor.Items.Count == afterDeath, "death drop exactly once");
Check(service.GetSnapshot(0).Weapons.All(x => x.IsEmpty) && service.GetSnapshot(0).Armor.IsEmpty, "death clears inventory and worn armor");
Check(service.Pickup(match, 0, ++request, far) == ItemResult.Dead, "dead player cannot pickup");
Throws(() => service.Tick(double.NaN), "invalid clock rejected");
Throws(() => service.SetActorState(1, float.NaN, 0, true), "invalid position rejected");
Check(!floor.TrySpawn(default, 0, 0, out _), "empty floor item rejected");
Check(!floor.TrySpawn(new ItemStack("KNIFE"), float.NaN, 0, out _), "nonfinite floor position rejected");
floor.TrySpawn(new ItemStack("KNIFE", 1, float.NaN), 10, 10, out var badDurability);
Check(service.Pickup(match, 1, 2, badDurability) == ItemResult.Invalid, "invalid floor durability rejected");
floor.TrySpawn(new ItemStack("SHOTGUN", 1, 0, 21), 10, 10, out var badAmmo);
Check(service.Pickup(match, 1, 3, badAmmo) == ItemResult.Invalid, "invalid floor ammo rejected");
var poisoned = Spawn("UNKNOWN");
Check(service.Pickup(match, 1, 4, poisoned) == ItemResult.Invalid, "unknown floor catalog id rejected");
var rootItem = Spawn("GOD_LEAF"); service.Pickup(match, 1, 5, rootItem); service.SetActorState(1, 10, 10, true, ItemControlFlags.Root);
Check(service.BeginUse(match, 1, 6, 0, 4) == ItemResult.Ok, "Root still allows using medicine");
service.CancelForAction(1, ItemCancelAction.Attack);
Check(!service.GetSnapshot(1).IsUsing && service.GetSnapshot(1).Consumables[0].Count == 1, "attack cancels without consuming delayed medicine");
service.SetActorState(1, 10, 10, true);
service.Pickup(match, 1, 7, Spawn("ELIXIR"));
Check(service.BeginUse(match, 1, 8, 1, 5) == ItemResult.Ok && service.GetSnapshot(1).Consumables[1].IsEmpty && service.GetSnapshot(1).UseItemId == "ELIXIR", "last immediate-use item retains progress identity");
service.Tick(6);
Check(!service.GetSnapshot(1).IsUsing && service.GetSnapshot(1).UseItemId == "", "completed use clears progress identity");

ushort rollbackNext = 0; var rollbackLimit = ushort.MaxValue;
var rollbackWorld = new MemoryItemWorld(() => rollbackNext < rollbackLimit ? ++rollbackNext : (ushort)0);
var rollback = new InventoryService(match, baseline, game, rollbackWorld); rollback.Register(0);
rollbackWorld.TrySpawn(new ItemStack("KNIFE"), 0, 0, out var rollbackWeapon); rollback.Pickup(match, 0, 1, rollbackWeapon);
rollbackWorld.TrySpawn(new ItemStack("GOD_LEAF"), 0, 0, out var rollbackMedicine); rollback.Pickup(match, 0, 2, rollbackMedicine);
rollbackLimit = (ushort)(rollbackNext + 1);
Check(rollback.DeathDrop(0, 1) == ItemResult.Full && rollbackWorld.Items.Count == 0, "partial death-drop allocation rolls back floor spawns");
Check(!rollback.GetSnapshot(0).Weapons[0].IsEmpty && !rollback.GetSnapshot(0).Consumables[0].IsEmpty, "failed death-drop preserves inventory for retry");
rollbackLimit = ushort.MaxValue;
Check(rollback.DeathDrop(0, 1) == ItemResult.Ok && rollbackWorld.Items.Count == 2, "retry after capacity recovery drops exactly all items");
Check(rollback.DeathDrop(0, 1) == ItemResult.Duplicate && rollbackWorld.Items.Count == 2, "repeated successful death-drop cannot duplicate");
var newService = new InventoryService(Guid.NewGuid(), baseline, game, floor); newService.Register(0);
Check(newService.GetSnapshot(0).Revision == 0, "fresh round has fresh state and request dedupe");
Console.WriteLine($"InventoryChecks: {passed} passed, 0 failed");
return 0;

sealed class GameplaySpy : IItemGameplayPort
{
    public int Heals, Effects, Rescues;
    public float HealTotal, RescueHp; public double RescueSeconds;
    public ItemEffect LastEffect;
    public void Heal(byte actor, float amount) { Heals++; HealTotal += amount; }
    public void ApplyEffect(byte actor, ItemEffect effect, double seconds) { Effects++; LastEffect = effect; }
    public void Rescue(byte actor, float hp, double seconds) { Rescues++; RescueHp = hp; RescueSeconds = seconds; }
}
