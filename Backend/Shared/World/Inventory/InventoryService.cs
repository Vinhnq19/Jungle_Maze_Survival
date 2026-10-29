#nullable disable
using System;
using System.Collections.Generic;

namespace JungleMaze.World.Inventory
{
    [Flags] public enum ItemControlFlags : byte { None = 0, Stun = 1, Root = 2, Reverse = 4 }
    public enum ItemCancelAction : byte { Move, Attack, Swap, Stun, Damage }
    public enum ItemResult : byte { Ok, Duplicate, WrongMatch, UnknownActor, Dead, Invalid, OutOfRange, Missing, Full, Controlled, Busy, Rejected, Stale }
    public interface IItemGameplayPort
    {
        void Heal(byte actor, float amount);
        void ApplyEffect(byte actor, ItemEffect effect, double seconds);
        void Rescue(byte actor, float hp, double invulnerableSeconds);
    }
    public interface IItemWorldPort
    {
        bool TryGet(ushort id, out FloorItem item);
        bool TrySpawn(ItemStack stack, float x, float y, out ushort id);
        bool Remove(ushort id);
        void SetStack(ushort id, ItemStack stack);
    }
    [Serializable]
    public struct FloorItem
    {
        public ushort Id;
        public ItemStack Stack;
        public float X, Y;
    }
    public sealed class MemoryItemWorld : IItemWorldPort
    {
        private readonly Dictionary<ushort, FloorItem> _items = new Dictionary<ushort, FloorItem>();
        private readonly Func<ushort> _allocate;
        public MemoryItemWorld(Func<ushort> allocate) { _allocate = allocate ?? throw new ArgumentNullException(nameof(allocate)); }
        public IReadOnlyCollection<FloorItem> Items => _items.Values;
        public bool TryGet(ushort id, out FloorItem item) => _items.TryGetValue(id, out item);
        public bool TrySpawn(ItemStack stack, float x, float y, out ushort id)
        {
            id = 0;
            if (stack.IsEmpty || !Finite(x) || !Finite(y)) return false;
            var allocated = _allocate();
            if (allocated == 0 || _items.ContainsKey(allocated)) return false;
            id = allocated; _items.Add(id, new FloorItem { Id = id, Stack = stack, X = x, Y = y }); return true;
        }
        public bool Remove(ushort id) => _items.Remove(id);
        public void SetStack(ushort id, ItemStack stack)
        {
            if (!_items.TryGetValue(id, out var item)) throw new InvalidOperationException("Unknown floor item.");
            if (stack.IsEmpty) { _items.Remove(id); return; }
            item.Stack = stack; _items[id] = item;
        }
        public void Clear() => _items.Clear();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    public sealed class InventorySnapshot
    {
        public byte Actor;
        public ItemStack[] Weapons = new ItemStack[4];
        public ItemStack[] Consumables = new ItemStack[4];
        public ItemStack Helmet, Armor;
        public int ActiveWeapon = -1;
        public bool IsUsing;
        public int UseSlot = -1;
        public string UseItemId = "";
        public double UseEnd, UseDuration;
        public uint Revision;
    }
    public interface IItemPassive
    {
        bool TryConsumeLifePassive(byte actor, uint transactionId);
    }
    /// <summary>Single simulation thread only. Owns item state, never HP or effects. All public commands are match/session-scoped.</summary>
    public sealed class InventoryService : IItemPassive
    {
        private sealed class Actor
        {
            public readonly ItemStack[] Weapons = new ItemStack[4];
            public readonly ItemStack[] Consumables = new ItemStack[4];
            public ItemStack Helmet, Armor;
            public int ActiveWeapon = -1;
            public float X, Y;
            public bool Alive = true;
            public ItemControlFlags Control;
            public uint Request, Revision;
            public uint EquipmentGeneration;
            public bool HasRequest, DeathDropped, PassiveConsumed;
            public int UseSlot = -1;
            public double UseEnd, UseDuration;
        }
        private readonly Dictionary<byte, Actor> _actors = new Dictionary<byte, Actor>();
        private readonly ItemCatalog _catalog;
        private readonly IItemGameplayPort _gameplay;
        private readonly IItemWorldPort _world;
        private double _now;
        public Guid MatchId { get; }
        public float PickupRadius { get; }
        public event Action<byte> Changed;
        public InventoryService(Guid matchId, ItemCatalog catalog, IItemGameplayPort gameplay, IItemWorldPort world, float pickupRadius = 1.2f)
        {
            if (matchId == Guid.Empty || !Finite(pickupRadius) || pickupRadius <= 0) throw new ArgumentException("Match/pickup radius invalid.");
            MatchId = matchId; _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay)); _world = world ?? throw new ArgumentNullException(nameof(world)); PickupRadius = pickupRadius;
        }
        public void Register(byte actor)
        {
            if (actor == byte.MaxValue || _actors.ContainsKey(actor)) throw new ArgumentException("Invalid/duplicate actor.");
            _actors.Add(actor, new Actor());
        }
        public void SetActorState(byte actor, float x, float y, bool alive, ItemControlFlags controls = ItemControlFlags.None)
        {
            if (!_actors.TryGetValue(actor, out var a) || !Finite(x) || !Finite(y)) throw new ArgumentException("Invalid actor state.");
            a.X = x; a.Y = y; a.Alive = alive; a.Control = controls;
            if (!alive) Cancel(a, actor);
            else if ((controls & ItemControlFlags.Stun) != 0) CancelForAction(actor, ItemCancelAction.Stun);
        }
        public InventorySnapshot GetSnapshot(byte actor)
        {
            if (!_actors.TryGetValue(actor, out var a)) throw new ArgumentException("Unknown actor.");
            return new InventorySnapshot { Actor = actor, Weapons = (ItemStack[])a.Weapons.Clone(), Consumables = (ItemStack[])a.Consumables.Clone(),
                Helmet = a.Helmet, Armor = a.Armor, ActiveWeapon = a.ActiveWeapon, IsUsing = a.UseSlot >= 0,
                UseSlot = a.UseSlot, UseItemId = a.UseSlot >= 0 ? UseDefinition(a, actor).Id : "", UseEnd = a.UseEnd, UseDuration = a.UseDuration, Revision = a.Revision };
        }
        public ItemResult Pickup(Guid matchId, byte actor, uint requestId, ushort floorId)
        {
            var check = Check(matchId, actor, requestId, out var a); if (check != ItemResult.Ok) return check;
            return PickupCore(actor, a, floorId);
        }
        /// <summary>Authoritative walk-over pickup, separate from the client request sequence.</summary>
        public ItemResult AutoPickup(byte actor, ushort floorId)
        {
            if (!_actors.TryGetValue(actor, out var a) || !a.Alive) return ItemResult.Dead;
            if (!_world.TryGet(floorId, out var floor) || !_catalog.TryGet(floor.Stack.ItemId, out var item)) return ItemResult.Missing;
            if (item.Kind != ItemKind.Ammo && item.Kind != ItemKind.Helmet && item.Kind != ItemKind.Armor) return ItemResult.Invalid;
            return PickupCore(actor, a, floorId);
        }
        private ItemResult PickupCore(byte actor, Actor a, ushort floorId)
        {
            if (!_world.TryGet(floorId, out var floor)) return ItemResult.Missing;
            if (!Finite(floor.X) || !Finite(floor.Y) || !Finite(a.X) || !Finite(a.Y) ||
                (floor.X - a.X) * (floor.X - a.X) + (floor.Y - a.Y) * (floor.Y - a.Y) > PickupRadius * PickupRadius) return ItemResult.OutOfRange;
            if (!_catalog.TryGet(floor.Stack.ItemId, out var item) || !item.Enabled || floor.Stack.Count < 1 || item.Kind == ItemKind.Unarmed) return ItemResult.Invalid;
            if (!Finite(floor.Stack.Durability) || floor.Stack.Durability < 0 || floor.Stack.Durability > item.Durability ||
                floor.Stack.Ammo < 0 || floor.Stack.Ammo > item.ReserveCapacity ||
                floor.Stack.MagazineKnown && (floor.Stack.Magazine < 0 || floor.Stack.Magazine > item.MagazineSize)) return ItemResult.Invalid;
            if (a.UseSlot >= 0) return ItemResult.Busy;
            if (item.Kind == ItemKind.Ammo)
            {
                var slot = GunForAmmo(a); if (slot < 0) return ItemResult.Full;
                var stack = a.Weapons[slot]; var gun = _catalog.Get(stack.ItemId);
                var needed = gun.ReserveCapacity - stack.Ammo;
                var boxes = Math.Min(floor.Stack.Count, (needed + gun.MagazineSize - 1) / gun.MagazineSize);
                stack.Ammo = Math.Min(gun.ReserveCapacity, stack.Ammo + boxes * gun.MagazineSize); a.Weapons[slot] = stack;
                TakeFloor(floor, boxes); Mark(a, actor); return ItemResult.Ok;
            }
            if (item.Kind == ItemKind.Helmet || item.Kind == ItemKind.Armor)
            {
                var existing = item.Kind == ItemKind.Helmet ? a.Helmet : a.Armor;
                if (!existing.IsEmpty && _catalog.Get(existing.ItemId).Reduction >= item.Reduction) return ItemResult.Rejected;
                if (!existing.IsEmpty && !_world.TrySpawn(existing, a.X, a.Y, out _)) return ItemResult.Full;
                var worn = new ItemStack(item.Id, 1, floor.Stack.Durability > 0 ? floor.Stack.Durability : item.Durability);
                if (item.Kind == ItemKind.Helmet) a.Helmet = worn; else a.Armor = worn;
                TakeFloor(floor, 1); Mark(a, actor); return ItemResult.Ok;
            }
            var slots = item.Kind == ItemKind.Weapon ? a.Weapons : a.Consumables;
            var index = FindStack(slots, item.Id);
            if (index < 0) index = FindEmpty(slots);
            if (index < 0)
            {
                if (item.Kind != ItemKind.Weapon) return ItemResult.Full;
                index = a.ActiveWeapon >= 0 ? a.ActiveWeapon : 0;
                if (!_world.TrySpawn(slots[index], a.X, a.Y, out _)) return ItemResult.Full;
                slots[index] = default(ItemStack);
            }
            var current = slots[index]; var received = Math.Min(floor.Stack.Count, item.MaxStack - (current.IsEmpty ? 0 : current.Count));
            if (received <= 0) return ItemResult.Full;
            if (current.IsEmpty) current = new ItemStack(item.Id, 0, floor.Stack.Durability, floor.Stack.Ammo,
                item.MagazineSize > 0 ? floor.Stack.MagazineKnown ? floor.Stack.Magazine : item.MagazineSize : -1);
            current.Count += received; slots[index] = current;
            if (item.Kind == ItemKind.Weapon && index == a.ActiveWeapon) a.EquipmentGeneration++;
            TakeFloor(floor, received); Mark(a, actor); return ItemResult.Ok;
        }
        public ItemResult Drop(Guid matchId, byte actor, uint requestId, int slot)
        {
            var check = Check(matchId, actor, requestId, out var a); if (check != ItemResult.Ok) return check;
            if (slot < 0 || slot > 7) return ItemResult.Invalid;
            var slots = slot < 4 ? a.Weapons : a.Consumables; var index = slot % 4;
            if (slots[index].IsEmpty) return ItemResult.Missing;
            if (!_world.TrySpawn(slots[index], a.X, a.Y, out _)) return ItemResult.Full;
            Cancel(a, actor); slots[index] = default(ItemStack);
            if (slot < 4 && a.ActiveWeapon == index) { a.ActiveWeapon = -1; a.EquipmentGeneration++; }
            Mark(a, actor); return ItemResult.Ok;
        }
        public ItemResult Select(Guid matchId, byte actor, uint requestId, int slot)
        {
            var check = Check(matchId, actor, requestId, out var a); if (check != ItemResult.Ok) return check;
            if (slot < -1 || slot > 3 || slot >= 0 && a.Weapons[slot].IsEmpty) return ItemResult.Invalid;
            if ((a.Control & ItemControlFlags.Stun) != 0) return ItemResult.Controlled;
            Cancel(a, actor); a.ActiveWeapon = a.ActiveWeapon == slot ? -1 : slot; a.EquipmentGeneration++; Mark(a, actor); return ItemResult.Ok;
        }
        public ItemResult BeginUse(Guid matchId, byte actor, uint requestId, int consumableSlot, double now)
        {
            var check = Check(matchId, actor, requestId, out var a); if (check != ItemResult.Ok) return check;
            if (!Finite(now) || now < _now || consumableSlot < 0 || consumableSlot > 3) return ItemResult.Invalid;
            if (a.UseSlot >= 0) return ItemResult.Busy;
            var stack = a.Consumables[consumableSlot]; if (stack.IsEmpty) return ItemResult.Missing;
            var item = _catalog.Get(stack.ItemId);
            if (!Finite(now + item.UseSeconds)) return ItemResult.Invalid;
            if (item.Effect == ItemEffect.LifePassive) return ItemResult.Rejected;
            // Root/Reverse do not prohibit using items. Stun does; ELIXIR is its explicit exception.
            if ((a.Control & ItemControlFlags.Stun) != 0 && !item.UseWhileControlled) return ItemResult.Controlled;
            a.UseSlot = consumableSlot; a.UseDuration = item.UseSeconds; a.UseEnd = now + item.UseSeconds;
            // An immediate effect consumes its item immediately; cancelling its later heal cannot grant infinite cleanses.
            if (item.EffectAtStart)
            {
                Consume(a, consumableSlot); _gameplay.ApplyEffect(actor, item.Effect, item.EffectSeconds);
                _immediateUses[actor] = item;
            }
            Mark(a, actor); return ItemResult.Ok;
        }
        private readonly Dictionary<byte, ItemDefinition> _immediateUses = new Dictionary<byte, ItemDefinition>();
        public void CancelForAction(byte actor, ItemCancelAction action)
        {
            if (!_actors.TryGetValue(actor, out var a) || a.UseSlot < 0 || action == ItemCancelAction.Damage) return;
            var item = UseDefinition(a, actor);
            if (action == ItemCancelAction.Stun && item.IgnoreStunCancel) return;
            Cancel(a, actor);
        }
        public bool IsMovementLocked(byte actor) => _actors.TryGetValue(actor, out var a) && a.UseSlot >= 0;
        public bool IsUsing(byte actor) => _actors.TryGetValue(actor, out var a) && a.UseSlot >= 0;
        public ItemStack HeldWeapon(byte actor)
        {
            return _actors.TryGetValue(actor, out var a) && a.ActiveWeapon >= 0 ? a.Weapons[a.ActiveWeapon] : default(ItemStack);
        }
        public uint EquipmentGeneration(byte actor) => _actors.TryGetValue(actor, out var a) ? a.EquipmentGeneration : 0;
        public bool CanAttack(byte actor) => _actors.TryGetValue(actor, out var a) && a.Alive && (a.Control & ItemControlFlags.Stun) == 0;
        public bool TrySetWeaponAmmo(byte actor, string itemId, int magazine, int reserve)
        {
            if (!_actors.TryGetValue(actor, out var a) || !_catalog.TryGet(itemId, out var item) ||
                magazine < 0 || magazine > item.MagazineSize || reserve < 0 || reserve > item.ReserveCapacity) return false;
            var index = FindStack(a.Weapons, itemId);
            if (index < 0) return false;
            var stack = a.Weapons[index];
            if (stack.MagazineKnown && stack.Magazine == magazine && stack.Ammo == reserve) return true;
            stack.Magazine = magazine; stack.MagazineKnown = true; stack.Ammo = reserve;
            a.Weapons[index] = stack; Mark(a, actor); return true;
        }
        /// <summary>Only confirmed weapon damage enters armor; environmental damage bypasses this method.</summary>
        public int AbsorbWeaponDamage(byte actor, int rawDamage, float reductionCap = .7f)
        {
            if (rawDamage <= 0 || !_actors.TryGetValue(actor, out var a)) return 0;
            var helmet = a.Helmet.IsEmpty ? 0 : _catalog.Get(a.Helmet.ItemId).Reduction;
            var armor = a.Armor.IsEmpty ? 0 : _catalog.Get(a.Armor.ItemId).Reduction;
            var total = helmet + armor;
            if (total <= 0) return rawDamage;
            var blocked = rawDamage * Math.Min(reductionCap, total);
            if (helmet > 0) Wear(ref a.Helmet, blocked * helmet / total);
            if (armor > 0) Wear(ref a.Armor, blocked * armor / total);
            Mark(a, actor);
            return Math.Max(0, (int)Math.Ceiling(rawDamage - blocked));
        }
        private static void Wear(ref ItemStack stack, float amount)
        {
            stack.Durability = Math.Max(0, stack.Durability - amount);
            if (stack.Durability <= 0) stack = default(ItemStack);
        }
        public void Tick(double now)
        {
            if (!Finite(now) || now < _now) throw new ArgumentException("Inventory time must be finite and monotonic.");
            _now = now;
            foreach (var pair in _actors)
            {
                var a = pair.Value; if (!a.Alive || a.UseSlot < 0 || now < a.UseEnd) continue;
                var item = UseDefinition(a, pair.Key);
                if (!item.EffectAtStart) Consume(a, a.UseSlot);
                a.UseSlot = -1; a.UseDuration = 0; a.UseEnd = 0; _immediateUses.Remove(pair.Key);
                if (item.Heal > 0) _gameplay.Heal(pair.Key, item.Heal);
                if (!item.EffectAtStart && item.Effect != ItemEffect.None) _gameplay.ApplyEffect(pair.Key, item.Effect, item.EffectSeconds);
                Mark(a, pair.Key);
            }
        }
        public bool TryConsumeLifePassive(byte actor, uint transactionId)
        {
            if (transactionId == 0 || !_actors.TryGetValue(actor, out var a) || !a.Alive || a.DeathDropped || a.PassiveConsumed) return false;
            var slot = FindStack(a.Consumables, "ELIXIR_OF_LIFE"); if (slot < 0) return false;
            a.PassiveConsumed = true; Consume(a, slot); Cancel(a, actor);
            var item = _catalog.Get("ELIXIR_OF_LIFE"); _gameplay.Rescue(actor, item.Heal, item.EffectSeconds); Mark(a, actor); return true;
        }
        public ItemResult DeathDrop(byte actor, uint eliminationId)
        {
            if (eliminationId == 0 || !_actors.TryGetValue(actor, out var a)) return ItemResult.Invalid;
            if (a.DeathDropped) return ItemResult.Duplicate;
            // Spawn all before changing inventory. Rollback prevents a full allocator producing partial duplicate death drops.
            var spawned = new List<ushort>();
            foreach (var item in DeathItems(a))
            {
                if (!_world.TrySpawn(item, a.X, a.Y, out var id)) { foreach (var old in spawned) _world.Remove(old); return ItemResult.Full; }
                spawned.Add(id);
            }
            a.DeathDropped = true; a.Alive = false; Cancel(a, actor);
            Array.Clear(a.Weapons, 0, 4); Array.Clear(a.Consumables, 0, 4); a.Helmet = default(ItemStack); a.Armor = default(ItemStack); a.ActiveWeapon = -1;
            Mark(a, actor); return ItemResult.Ok;
        }
        private IEnumerable<ItemStack> DeathItems(Actor a)
        {
            foreach (var stack in a.Weapons) if (!stack.IsEmpty) yield return stack;
            foreach (var stack in a.Consumables) if (!stack.IsEmpty) yield return stack;
            if (!a.Helmet.IsEmpty) yield return a.Helmet; if (!a.Armor.IsEmpty) yield return a.Armor;
        }
        private ItemDefinition UseDefinition(Actor a, byte actor) => _immediateUses.TryGetValue(actor, out var item) ? item : _catalog.Get(a.Consumables[a.UseSlot].ItemId);
        private ItemResult Check(Guid matchId, byte actor, uint requestId, out Actor a)
        {
            a = null;
            if (matchId != MatchId) return ItemResult.WrongMatch;
            if (!_actors.TryGetValue(actor, out a)) return ItemResult.UnknownActor;
            if (!a.Alive) return ItemResult.Dead;
            if (requestId == 0) return ItemResult.Invalid;
            if (a.HasRequest)
            {
                var delta = unchecked((int)(requestId - a.Request));
                if (delta == 0) return ItemResult.Duplicate;
                if (delta < 0) return ItemResult.Stale;
            }
            a.Request = requestId; a.HasRequest = true; return ItemResult.Ok;
        }
        private int GunForAmmo(Actor a)
        {
            if (a.ActiveWeapon >= 0 && CanReceiveAmmo(a.Weapons[a.ActiveWeapon])) return a.ActiveWeapon;
            for (var i = 0; i < 4; i++) if (CanReceiveAmmo(a.Weapons[i])) return i;
            return -1;
        }
        private bool CanReceiveAmmo(ItemStack stack) => !stack.IsEmpty && _catalog.Get(stack.ItemId).MagazineSize > 0 && stack.Ammo < _catalog.Get(stack.ItemId).ReserveCapacity;
        private static int FindStack(ItemStack[] slots, string id) { for (var i = 0; i < slots.Length; i++) if (!slots[i].IsEmpty && slots[i].ItemId == id) return i; return -1; }
        private static int FindEmpty(ItemStack[] slots) { for (var i = 0; i < slots.Length; i++) if (slots[i].IsEmpty) return i; return -1; }
        private void TakeFloor(FloorItem floor, int count) { floor.Stack.Count -= count; if (floor.Stack.Count == 0) _world.Remove(floor.Id); else _world.SetStack(floor.Id, floor.Stack); }
        private static void Consume(Actor a, int slot) { var stack = a.Consumables[slot]; stack.Count--; a.Consumables[slot] = stack.Count > 0 ? stack : default(ItemStack); }
        private void Cancel(Actor a, byte actor) { if (a.UseSlot < 0) return; a.UseSlot = -1; a.UseEnd = 0; a.UseDuration = 0; _immediateUses.Remove(actor); Mark(a, actor); }
        private void Mark(Actor a, byte actor) { unchecked { a.Revision++; } Changed?.Invoke(actor); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
