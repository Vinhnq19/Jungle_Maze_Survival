#nullable disable
using System;
using System.Collections.Generic;

namespace JungleMaze.World.Inventory
{
    public enum ItemKind : byte { Unarmed, Weapon, Consumable, Helmet, Armor, Ammo }
    [Flags] public enum LootSource : byte { None = 0, Pot = 1, Chest = 2, Supply = 4, Death = 8 }
    public enum ItemEffect : byte { None, Speed, Cleanse, Invisible, LifePassive, Compass }

    /// <summary>Machine-readable item rules. These values remain a tentative GDD profile.</summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        public string Id = "";
        public ItemKind Kind;
        public int MaxStack = 1;
        public bool Enabled = true;
        public LootSource Sources;
        public int Weight = 1;
        public int MagazineSize;
        public int ReserveCapacity;
        public float Reduction;
        public float Durability;
        public float Heal;
        public double UseSeconds;
        public ItemEffect Effect;
        public double EffectSeconds;
        public bool UseWhileControlled;
        public bool IgnoreStunCancel;
        public bool EffectAtStart;

        public ItemDefinition Copy() => (ItemDefinition)MemberwiseClone();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 40 || MaxStack < 1 || MaxStack > 999 || Weight < 0 || Weight > 100000)
                throw new ArgumentException("Invalid item identity, stack or weight.");
            if (!Finite(Reduction) || Reduction < 0 || Reduction > 1 || !Finite(Durability) || Durability < 0 ||
                !Finite(Heal) || Heal < 0 || !Finite(UseSeconds) || UseSeconds < 0 || !Finite(EffectSeconds) || EffectSeconds < 0 ||
                MagazineSize < 0 || ReserveCapacity < 0 || MagazineSize > 999 || ReserveCapacity > 9999)
                throw new ArgumentException("Invalid item numeric rule: " + Id);
            if (!Enum.IsDefined(typeof(ItemKind), Kind) || !Enum.IsDefined(typeof(ItemEffect), Effect) || ((byte)Sources & ~15) != 0)
                throw new ArgumentException("Invalid item rule enum: " + Id);
            if ((MagazineSize == 0) != (ReserveCapacity == 0)) throw new ArgumentException("Gun needs magazine and reserve: " + Id);
            if (Kind == ItemKind.Unarmed && Sources != LootSource.None) throw new ArgumentException("HAND cannot drop.");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    public struct ItemStack
    {
        public string ItemId;
        public int Count;
        public float Durability;
        public int Ammo;
        public ItemStack(string itemId, int count = 1, float durability = 0, int ammo = 0)
        { ItemId = itemId; Count = count; Durability = durability; Ammo = ammo; }
        public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;
    }

    public sealed class ItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> _items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        public string Version { get; }
        public IReadOnlyCollection<ItemDefinition> Items
        {
            get { var copy = new List<ItemDefinition>(); foreach (var item in _items.Values) copy.Add(item.Copy()); return copy.AsReadOnly(); }
        }
        public ItemCatalog(string version, IEnumerable<ItemDefinition> items)
        {
            if (string.IsNullOrWhiteSpace(version) || items == null) throw new ArgumentException("Catalog/version required.");
            Version = version;
            foreach (var item in items)
            {
                if (item == null) throw new ArgumentException("Null catalog item.");
                item.Validate();
                if (_items.ContainsKey(item.Id)) throw new ArgumentException("Duplicate catalog ID: " + item.Id);
                _items.Add(item.Id, item.Copy());
            }
            if (!_items.ContainsKey("HAND") || !_items.ContainsKey("AMMO")) throw new ArgumentException("Catalog requires HAND and AMMO.");
        }
        public bool TryGet(string id, out ItemDefinition item)
        {
            var found = _items.TryGetValue(id ?? "", out var value); item = found ? value.Copy() : null; return found;
        }
        public ItemDefinition Get(string id) => _items.TryGetValue(id ?? "", out var value) ? value.Copy() : throw new ArgumentException("Unknown item: " + id);

        /// <summary>Fixture/demo default; production should load the versioned items JSON via an adapter.</summary>
        public static ItemCatalog CreateBaseline()
        {
            var all = new List<ItemDefinition>();
            var pc = LootSource.Pot | LootSource.Chest | LootSource.Death;
            var cs = LootSource.Chest | LootSource.Supply | LootSource.Death;
            var c = LootSource.Chest | LootSource.Death;
            all.Add(new ItemDefinition { Id = "HAND", Kind = ItemKind.Unarmed });
            foreach (var id in new[] { "TREE_BRANCH", "KNIFE", "BONE", "AXE", "MACE" })
                all.Add(new ItemDefinition { Id = id, Kind = ItemKind.Weapon, Sources = id == "MACE" ? LootSource.Supply | LootSource.Death : id == "AXE" ? c : pc, Weight = 25 });
            var guns = new[] { "RUBBER_GUN", "SCEPTER", "CROSSBOW", "SHOTGUN" };
            var magazines = new[] { 12, 10, 6, 4 }; var reserve = new[] { 60, 50, 30, 20 };
            for (var i = 0; i < guns.Length; i++) all.Add(new ItemDefinition { Id = guns[i], Kind = ItemKind.Weapon, Sources = i == 3 ? cs : c, Weight = i == 3 ? 8 : 25, Enabled = i == 3, MagazineSize = magazines[i], ReserveCapacity = reserve[i] });
            var thrown = new[] { "BANANA", "SMOKE", "METEORITE_FRAGMENT", "CRYSTAL", "SEED", "ROPE", "BELL", "SAHUR_BAT" };
            var caps = new[] { 3, 2, 3, 2, 3, 1, 1, 1 }; var weights = new[] { 25, 20, 20, 15, 20, 12, 10, 12 };
            for (var i = 0; i < thrown.Length; i++) all.Add(new ItemDefinition { Id = thrown[i], Kind = ItemKind.Weapon, MaxStack = caps[i], Sources = i == 0 || i == 4 ? pc : c, Weight = weights[i] });
            var armor = new[] { "WOODEN_HELMET", "SPIKE_HELMET", "MAGIC_HELMET", "WOODEN_ARMOR", "ARMOR", "MAGIC_ARMOR" };
            var reductions = new[] { .1f, .2f, .3f, .2f, .35f, .5f }; var durability = new[] { 30f, 50f, 70f, 50f, 80f, 120f };
            var armorWeight = new[] { 40, 25, 12, 35, 22, 10 };
            for (var i = 0; i < armor.Length; i++) all.Add(new ItemDefinition { Id = armor[i], Kind = i < 3 ? ItemKind.Helmet : ItemKind.Armor, Reduction = reductions[i], Durability = durability[i], Weight = armorWeight[i], Sources = i == 0 || i == 3 ? pc : i == 2 || i == 5 ? cs : c });
            var healIds = new[] { "GOD_LEAF", "SPIRIT_TEAR", "MERMAID_TAIL", "PHOENIX_FEATHERS", "UNICORN_BLOOD" };
            var heals = new[] { 10f, 15f, 25f, 45f, 100f }; var seconds = new[] { .5, .8, 1.2, 2.0, 3.5 }; var carry = new[] { 6, 5, 4, 3, 2 }; var hw = new[] { 25, 20, 15, 10, 6 };
            for (var i = 0; i < healIds.Length; i++) all.Add(new ItemDefinition { Id = healIds[i], Kind = ItemKind.Consumable, MaxStack = carry[i], Heal = heals[i], UseSeconds = seconds[i], Weight = hw[i], Sources = i < 2 ? pc : i == 4 ? cs : c });
            all.Add(new ItemDefinition { Id = "ADRENALINE", Kind = ItemKind.Consumable, MaxStack = 4, UseSeconds = 1.5, Effect = ItemEffect.Speed, EffectSeconds = 8, Weight = 12, Sources = pc });
            all.Add(new ItemDefinition { Id = "ELIXIR", Kind = ItemKind.Consumable, MaxStack = 2, Heal = 5, UseSeconds = 1, Effect = ItemEffect.Cleanse, EffectSeconds = 7, UseWhileControlled = true, IgnoreStunCancel = true, EffectAtStart = true, Weight = 6, Sources = cs });
            all.Add(new ItemDefinition { Id = "MAGIC", Kind = ItemKind.Consumable, MaxStack = 2, UseSeconds = 1, Effect = ItemEffect.Invisible, EffectSeconds = 5, Weight = 6, Sources = c });
            all.Add(new ItemDefinition { Id = "ELIXIR_OF_LIFE", Kind = ItemKind.Consumable, Heal = 50, Effect = ItemEffect.LifePassive, EffectSeconds = 2, Sources = LootSource.Supply | LootSource.Death });
            all.Add(new ItemDefinition { Id = "COMPASS", Kind = ItemKind.Consumable, UseSeconds = 2, Effect = ItemEffect.Compass, EffectSeconds = 5, Sources = LootSource.Supply | LootSource.Death });
            all.Add(new ItemDefinition { Id = "AMMO", Kind = ItemKind.Ammo, Sources = pc });
            return new ItemCatalog("gdd-v0.3-proposal", all);
        }
    }

    [Serializable]
    public sealed class ItemCatalogDocument
    {
        public string Version = "";
        public ItemDefinition[] Items = Array.Empty<ItemDefinition>();
        public LootProfile Loot = new LootProfile();
        public ItemCatalog Build() => new ItemCatalog(Version, Items);
    }
    [Serializable]
    public sealed class SupplyDropRule
    {
        public string ItemId = "";
        public int ChancePercent = 100;
        public int MinCount = 1, MaxCount = 1;
        public int Ammo;
    }
    [Serializable]
    public sealed class LootProfile
    {
        public int ChestMin = 3, ChestMax = 4, PotMin = 1, PotMax = 2;
        public int[] ChestGroupWeights = { 15, 20, 25, 15, 15, 10 };
        public int[] PotGroupWeights = { 25, 8, 30, 12, 20, 5 };
        public SupplyDropRule[] Supply = {
            new SupplyDropRule { ItemId="MACE" }, new SupplyDropRule { ItemId="COMPASS" },
            new SupplyDropRule { ItemId="SHOTGUN",ChancePercent=70,Ammo=20 },
            new SupplyDropRule { ItemId="MAGIC_ARMOR",ChancePercent=60 },
            new SupplyDropRule { ItemId="MAGIC_HELMET",ChancePercent=50 },
            new SupplyDropRule { ItemId="ELIXIR_OF_LIFE",ChancePercent=35 },
            new SupplyDropRule { ItemId="UNICORN_BLOOD",ChancePercent=80,MaxCount=2 },
            new SupplyDropRule { ItemId="ELIXIR",ChancePercent=60 }
        };
    }
    /// <summary>Versioned group weights and counts. Sampling conditions on feasible enabled groups, so reroll cannot hang.</summary>
    public sealed class LootTable
    {
        private readonly ItemCatalog _catalog;
        private readonly LootProfile _profile;
        public LootTable(ItemCatalog catalog, LootProfile profile = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var source = profile ?? new LootProfile();
            _profile = new LootProfile { ChestMin = source.ChestMin, ChestMax = source.ChestMax, PotMin = source.PotMin, PotMax = source.PotMax,
                ChestGroupWeights = source.ChestGroupWeights == null ? null : (int[])source.ChestGroupWeights.Clone(),
                PotGroupWeights = source.PotGroupWeights == null ? null : (int[])source.PotGroupWeights.Clone(),
                Supply = source.Supply == null ? null : new SupplyDropRule[source.Supply.Length] };
            if (source.Supply != null) for (var i = 0; i < source.Supply.Length; i++)
            {
                var rule = source.Supply[i];
                if (rule != null) _profile.Supply[i] = new SupplyDropRule { ItemId = rule.ItemId, ChancePercent = rule.ChancePercent, MinCount = rule.MinCount, MaxCount = rule.MaxCount, Ammo = rule.Ammo };
            }
            if (_profile.ChestMin < 1 || _profile.ChestMax < _profile.ChestMin || _profile.ChestMax > 64 ||
                _profile.PotMin < 1 || _profile.PotMax < _profile.PotMin || _profile.PotMax > 64)
                throw new ArgumentException("Invalid loot counts.");
            foreach (var weights in new[] { _profile.ChestGroupWeights, _profile.PotGroupWeights })
            {
                if (weights == null || weights.Length != 6) throw new ArgumentException("Loot profile needs six group weights.");
                foreach (var weight in weights) if (weight < 0 || weight > 100000) throw new ArgumentException("Invalid loot group weight.");
            }
            if (_profile.Supply == null || _profile.Supply.Length > 64) throw new ArgumentException("Invalid supply table.");
            foreach (var rule in _profile.Supply)
                if (rule == null || !_catalog.TryGet(rule.ItemId, out _) || rule.ChancePercent < 0 || rule.ChancePercent > 100 || rule.MinCount < 1 || rule.MaxCount < rule.MinCount || rule.MaxCount > 999 || rule.Ammo < 0)
                    throw new ArgumentException("Invalid supply drop rule.");
            ValidateSources();
        }
        public ItemStack[] Roll(LootSource source, uint seed)
        {
            if (source != LootSource.Pot && source != LootSource.Chest && source != LootSource.Supply) throw new ArgumentException("Unsupported loot source.");
            var rng = new ItemRandom(seed);
            if (source == LootSource.Supply) return Supply(rng);
            var min = source == LootSource.Chest ? _profile.ChestMin : _profile.PotMin;
            var max = source == LootSource.Chest ? _profile.ChestMax : _profile.PotMax;
            var count = min + rng.Next(max - min + 1);
            var output = new ItemStack[count];
            for (var i = 0; i < count; i++)
            {
                var item = Sample(source, rng); output[i] = new ItemStack(item.Id, 1, item.Durability);
            }
            return output;
        }
        public void ValidateSources() { Sample(LootSource.Pot, new ItemRandom(1)); Sample(LootSource.Chest, new ItemRandom(1)); }
        private ItemDefinition Sample(LootSource source, ItemRandom rng)
        {
            var groups = new List<List<ItemDefinition>>(); var groupWeights = new List<int>();
            int[] weights = source == LootSource.Chest ? _profile.ChestGroupWeights : _profile.PotGroupWeights;
            for (var group = 0; group < 6; group++)
            {
                var options = new List<ItemDefinition>();
                foreach (var item in _catalog.Items) if (item.Enabled && item.Weight > 0 && (item.Sources & source) != 0 && Group(item) == group) options.Add(item);
                options.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                if (options.Count > 0 && weights[group] > 0) { groups.Add(options); groupWeights.Add(weights[group]); }
            }
            if (groups.Count == 0) throw new InvalidOperationException("Loot source has no enabled eligible items: " + source);
            var selected = groups[Weighted(groupWeights, rng)]; var itemWeights = new List<int>();
            foreach (var item in selected) itemWeights.Add(item.Weight);
            return selected[Weighted(itemWeights, rng)];
        }
        private ItemStack[] Supply(ItemRandom rng)
        {
            var result = new List<ItemStack>();
            foreach (var rule in _profile.Supply)
            {
                var item = _catalog.Get(rule.ItemId);
                if (item.Enabled && (item.Sources & LootSource.Supply) != 0 && rng.Next(100) < rule.ChancePercent)
                    result.Add(new ItemStack(item.Id, rule.MinCount + rng.Next(rule.MaxCount - rule.MinCount + 1), item.Durability, rule.Ammo));
            }
            return result.ToArray();
        }
        private static int Group(ItemDefinition i)
        {
            if (i.Kind == ItemKind.Ammo) return 2;
            if (i.Kind == ItemKind.Helmet || i.Kind == ItemKind.Armor) return 5;
            if (i.Kind == ItemKind.Consumable) return 4;
            if (i.MagazineSize > 0) return 1;
            return i.Id == "TREE_BRANCH" || i.Id == "KNIFE" || i.Id == "BONE" || i.Id == "AXE" || i.Id == "MACE" ? 0 : 3;
        }
        private static int Weighted(List<int> weights, ItemRandom rng)
        {
            var total = 0; foreach (var weight in weights) checked { total += weight; }
            var roll = rng.Next(total);
            for (var i = 0; i < weights.Count; i++) { roll -= weights[i]; if (roll < 0) return i; }
            throw new InvalidOperationException("Invalid loot weights.");
        }
    }
    internal sealed class ItemRandom
    {
        private uint _state;
        public ItemRandom(uint seed) { _state = seed == 0 ? 0x9e3779b9u : seed; }
        public int Next(int max) { if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max)); _state ^= _state << 13; _state ^= _state >> 17; _state ^= _state << 5; return (int)(_state % (uint)max); }
    }
}
