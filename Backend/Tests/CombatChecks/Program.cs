using System.Numerics;
using GameServer.Simulation;
using GameServer.Simulation.Combat;
using CombatChecks;
using JungleBackend.Protocol.Gameplay;

Console.WriteLine("=== Starting Jungle Maze CombatChecks (WPN-02) ===");

var passed = 0;
var failed = 0;

void Check(bool condition, string testName)
{
    if (condition)
    {
        Console.WriteLine($"[PASS] {testName}");
        passed++;
    }
    else
    {
        Console.WriteLine($"[FAIL] {testName}");
        failed++;
    }
}

var rules = WeaponRules.Load(GameDataLoader.ReadGameDataFile("weapons.json"));
Check(rules.Count == 10, "weapons.json: 10 WPN-02 weapons loaded (thrown/special skipped)");
Check(rules.TryGet(CombatWeaponId.Shotgun, out var shotgun) && shotgun.Pellets == 5 && shotgun.SpreadDegrees == 25f,
    "weapons.json: Shotgun has 5 pellets over 25 degrees");
Check(rules.TryGet(CombatWeaponId.Axe, out var axe) && axe.IsBreakableAxe && axe.Damage == 42,
    "weapons.json: Axe is the breakable axe with damage 42");
Check(rules.TryGet(CombatWeaponId.Shotgun, out var shotgunReload) && shotgunReload.ReloadSeconds == 2.8,
    "weapons.json: Shotgun reload is 2.8 s from JSON");
Check(rules.TryGet(CombatWeaponId.RubberGun, out var slingReload) && slingReload.ReloadSeconds == 1.2,
    "weapons.json: Rubber Slingshot reload is 1.2 s from JSON");

var noMap = new List<AABB>();

CombatService Build(FakeRegistry registry, List<AABB> obstacles, FakePassive passive, FakeEffects effects, FakeBreakables breakables) =>
    new CombatService(registry, new HealthService(registry), obstacles, rules, effects, passive, breakables);

AttackRequestPacket Attack(ushort seq, CombatWeaponId weapon, ushort aim = 0) =>
    new() { RequestSeq = seq, Weapon = weapon, Aim = aim };

List<CombatHit> hits = new();

// ---- Geometry ----
Check(CombatGeometry.SegmentIntersectsAabb(new Vector2(0, 0), new Vector2(2, 0), new AABB("W", 0.5f, 1f, -1f, 1f), out _),
    "geometry: segment crosses a box");
Check(!CombatGeometry.SegmentIntersectsAabb(new Vector2(0, 2), new Vector2(2, 2), new AABB("W", 0.5f, 1f, -1f, 1f), out _),
    "geometry: segment above a box misses");
Check(CombatGeometry.RayHitsCircle(Vector2.Zero, Vector2.UnitX, 5f, new Vector2(3, 0), 0.4f, out var circleDistance) && MathF.Abs(circleDistance - 2.6f) < 1e-4f,
    "geometry: ray enters circle at the expected distance");

// ---- Melee ----
{
    var registry = new FakeRegistry();
    var attacker = registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 1.0f, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    var result = combat.TryAttack(0, Attack(1, CombatWeaponId.Knife), 100, hits);
    Check(result.Outcome == CombatOutcome.Accepted && target.Hp == 78 && hits.Count == 1,
        "melee: knife hits a target inside the arc for 22 damage");
    Check(attacker.Hp == 100, "melee: attacker HP is untouched");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 0, 1.0f));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    combat.TryAttack(0, Attack(1, CombatWeaponId.Knife), 100, hits);
    Check(target.Hp == 100 && hits.Count == 0, "melee: target 90 degrees off aim is outside the knife arc");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 1.0f, 0));
    var cover = new List<AABB> { new("HardCover", 0.5f, 0.6f, -0.5f, 0.5f) };
    var combat = Build(registry, cover, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Mace);
    hits.Clear();

    combat.TryAttack(0, Attack(1, CombatWeaponId.Mace), 100, hits);
    Check(target.Hp == 100, "melee: Hard Cover between attacker and target blocks the hit");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 1.0f, 0));
    var effects = new FakeEffects();
    var combat = Build(registry, noMap, new FakePassive(false), effects, new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Mace);
    hits.Clear();

    var result = combat.TryAttack(0, Attack(1, CombatWeaponId.Mace), 100, hits);
    Check(result.Outcome == CombatOutcome.Accepted && target.Hp == 40 && effects.Stuns.Count == 1 && effects.Stuns[0].Seconds == 1.0,
        "melee: mace deals 60 damage and stuns for 1.0 s");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    registry.Add(new FakeEntity(1, 1.0f, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    var first = combat.TryAttack(0, Attack(1, CombatWeaponId.Knife), 100, hits);
    var early = combat.TryAttack(0, Attack(2, CombatWeaponId.Knife), 101, hits);
    var onTime = combat.TryAttack(0, Attack(3, CombatWeaponId.Knife), 114, hits);
    Check(first.Outcome == CombatOutcome.Accepted && early.Outcome == CombatOutcome.Cooldown && onTime.Outcome == CombatOutcome.Accepted,
        "cooldown: knife is rejected inside 0.45 s and accepted at the cooldown tick");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 1.0f, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    combat.TryAttack(0, Attack(5, CombatWeaponId.Knife), 100, hits);
    var repeated = combat.TryAttack(0, Attack(5, CombatWeaponId.Knife), 200, hits);
    var older = combat.TryAttack(0, Attack(4, CombatWeaponId.Knife), 200, hits);
    Check(repeated.Outcome == CombatOutcome.Duplicate && older.Outcome == CombatOutcome.Duplicate && target.Hp == 78,
        "dedupe: repeated or older RequestSeq is Duplicate and does no damage");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    var wrongWeapon = combat.TryAttack(0, Attack(1, CombatWeaponId.Axe), 100, hits);
    var reloadMelee = combat.TryReload(0, new ReloadRequestPacket { RequestSeq = 2, Weapon = CombatWeaponId.Knife }, 100);
    Check(wrongWeapon.Outcome == CombatOutcome.InvalidWeapon && reloadMelee.Outcome == CombatOutcome.InvalidWeapon,
        "validation: unheld weapon and melee reload are InvalidWeapon");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0) { Hp = 0, Flags = PlayerStateFlags.Dead });
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    var result = combat.TryAttack(0, Attack(1, CombatWeaponId.Knife), 100, hits);
    Check(result.Outcome == CombatOutcome.Dead, "validation: dead attacker is rejected");
}

// ---- Ranged ----
{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 3.0f, 0, 255));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.RubberGun);
    hits.Clear();

    var tick = 100u;
    var accepted = 0;
    for (var shot = 0; shot < 12; shot++)
    {
        if (combat.TryAttack(0, Attack((ushort)(shot + 1), CombatWeaponId.RubberGun), tick, hits).Outcome == CombatOutcome.Accepted)
            accepted++;
        tick += 10;
    }

    var empty = combat.TryAttack(0, Attack(100, CombatWeaponId.RubberGun), tick, hits);
    Check(accepted == 12 && empty.Outcome == CombatOutcome.NoAmmo && target.Hp == 255 - 12 * 15,
        "ranged: magazine of 12 fires 12 shots for 15 damage each, then NoAmmo");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 3.0f, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Shotgun);
    hits.Clear();

    var result = combat.TryAttack(0, Attack(1, CombatWeaponId.Shotgun), 100, hits);
    Check(result.Outcome == CombatOutcome.Accepted && target.Hp == 100 - 3 * 9 && result.Magazine == 3,
        "shotgun: 5 pellets over 25 degrees hit 3 pellets at 3 m and cost one shell");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 3.0f, 0));
    var cover = new List<AABB> { new("HardCover", 1.5f, 2.0f, -1f, 1f) };
    var combat = Build(registry, cover, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Crossbow);
    hits.Clear();

    var result = combat.TryAttack(0, Attack(1, CombatWeaponId.Crossbow), 100, hits);
    Check(result.Outcome == CombatOutcome.Accepted && target.Hp == 100 && hits.Count == 0,
        "ranged: Hard Cover stops the bolt and the target takes no damage");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Crossbow);
    hits.Clear();

    var tick = 100u;
    for (var shot = 0; shot < 6; shot++)
    {
        combat.TryAttack(0, Attack((ushort)(shot + 1), CombatWeaponId.Crossbow), tick, hits);
        tick += 40;
    }

    var reload = combat.TryReload(0, new ReloadRequestPacket { RequestSeq = 50, Weapon = CombatWeaponId.Crossbow }, tick);
    var duringReload = combat.TryAttack(0, Attack(51, CombatWeaponId.Crossbow), tick + 1, hits);
    var reloadEnd = reload.ReloadEndTick;
    combat.Tick(reloadEnd);
    var refilled = combat.TryAttack(0, Attack(52, CombatWeaponId.Crossbow), reloadEnd + 60, hits);

    Check(reload.Outcome == CombatOutcome.Accepted && reloadEnd == tick + 72 && duringReload.Outcome == CombatOutcome.Reloading,
        "reload: Crossbow request is accepted, lasts 2.4 s (72 ticks), and attacks during it are Reloading");
    Check(refilled.Outcome == CombatOutcome.Accepted && combat.GetMagazine(0, CombatWeaponId.Crossbow) == 5 && combat.GetReserve(0, CombatWeaponId.Crossbow) == 24,
        "reload: magazine refills 6 from reserve (30 to 24) when the reload finishes, then one shot is fired");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var combat = Build(registry, noMap, new FakePassive(false), new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Crossbow);

    var full = combat.TryReload(0, new ReloadRequestPacket { RequestSeq = 1, Weapon = CombatWeaponId.Crossbow }, 100);
    Check(full.Outcome == CombatOutcome.MagazineFull, "reload: full magazine is rejected with MagazineFull");
}

// ---- Life save and lethal damage ----
{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 1.0f, 0, 10));
    var passive = new FakePassive(true);
    var effects = new FakeEffects();
    var combat = Build(registry, noMap, passive, effects, new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    combat.TryAttack(0, Attack(1, CombatWeaponId.Knife), 100, hits);
    Check(target.IsAlive && target.Hp == CombatService.LifeSaveHp && passive.Calls == 1
        && effects.IsInvulnerable(1, 159) && !effects.IsInvulnerable(1, 160),
        "life save: lethal hit saved once, HP set to 50, invulnerable for 2 s");

    registry.Add(new FakeEntity(2, 0, 0));
    combat.SetHeldWeapon(2, CombatWeaponId.Knife);
    var sameTick = combat.TryAttack(2, Attack(1, CombatWeaponId.Knife), 100, hits);
    var laterHit = combat.TryAttack(0, Attack(2, CombatWeaponId.Knife), 115, hits);
    Check(sameTick.Outcome == CombatOutcome.Accepted && laterHit.Outcome == CombatOutcome.Accepted
        && target.Hp == CombatService.LifeSaveHp && passive.Calls == 1,
        "life save: hits during invulnerability do no damage and the passive is not used again");
}

{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var target = registry.Add(new FakeEntity(1, 1.0f, 0, 10));
    var passive = new FakePassive(false);
    var combat = Build(registry, noMap, passive, new FakeEffects(), new FakeBreakables());
    combat.SetHeldWeapon(0, CombatWeaponId.Knife);
    hits.Clear();

    combat.TryAttack(0, Attack(1, CombatWeaponId.Knife), 100, hits);
    Check(!target.IsAlive && target.Hp == 0 && hits.Count == 1 && hits[0].Killed,
        "lethal: without a life save the target dies and the hit is reported as a kill");
}

// ---- Breakable axe ----
{
    var registry = new FakeRegistry();
    registry.Add(new FakeEntity(0, 0, 0));
    var cover = new List<AABB> { new("Crate_01", 0.8f, 1.2f, -0.5f, 0.5f), new("Wall", 0f, 0.2f, 3f, 4f) };
    var breakables = new FakeBreakables();
    var combat = Build(registry, cover, new FakePassive(false), new FakeEffects(), breakables);
    combat.SetHeldWeapon(0, CombatWeaponId.Axe);
    hits.Clear();

    combat.TryAttack(0, Attack(1, CombatWeaponId.Axe), 100, hits);
    Check(breakables.Damages.Count == 1 && breakables.Damages[0].Name == "Crate_01" && breakables.Damages[0].Damage == 63,
        "axe: breakable crate takes 42 * 1.5 = 63 damage, non-breakable wall is not forwarded");
}

Console.WriteLine($"=== CombatChecks: {passed} passed, {failed} failed ===");
return failed == 0 ? 0 : 1;
