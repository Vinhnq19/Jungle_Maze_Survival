#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using JungleMaze.App.Player;
using JungleMaze.App.Pooling;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Infrastructure.Pooling;
using JungleMaze.Presentation.Player;
using JungleMaze.Presentation.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode
{
    public sealed class FoundationAcceptanceTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PoolService _pool;
        private SimulationMode2D _previousSimulation;
        private BalanceConfigProvider _balance;

        [SetUp]
        public void SetUp()
        {
            _previousSimulation = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            _balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            Assert.IsNull(_balance.LastError);
            var registry = new ServiceRegistry();
            _pool = new PoolService(new Injector(registry), null);
            registry.Register<IPoolService>(_pool);
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            Physics2D.simulationMode = _previousSimulation;
        }

        [UnityTest]
        public IEnumerator Hero_ConfigAfterAwake_DiagonalSpeed_AndAim()
        {
            var root = Path.Combine(Path.GetTempPath(), "jungle-hero-" + System.Guid.NewGuid().ToString("N"));
            var folder = Path.Combine(root, BalanceConfigProvider.Folder);
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var file in new[] { "match.json", "weapons.json", "equipments.json" })
                {
                    var data = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, BalanceConfigProvider.Folder, file));
                    if (file == "match.json") data = data.Replace("\"heroColliderRadius\": 0.4", "\"heroColliderRadius\": 0.65");
                    File.WriteAllText(Path.Combine(folder, file), data);
                }
                var changed = new BalanceConfigProvider(root, null);
                Assert.IsNull(changed.LastError);
                var hero = SpawnHero(new Vector2(20, 20));
                hero.Construct(changed); // AddComponent/Instantiate has already run Awake at this point.
                Assert.That(hero.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.65f).Within(.0001f));
                hero.ApplyMovement(Vector2.one);
                Assert.That(hero.GetComponent<Rigidbody2D>().linearVelocity.magnitude, Is.EqualTo(4).Within(.0001f));
                var start = hero.GetComponent<Rigidbody2D>().position;
                for (var i = 0; i < 50; i++) Physics2D.Simulate(.02f);
                Assert.That(Vector2.Distance(start, hero.GetComponent<Rigidbody2D>().position), Is.EqualTo(4).Within(.01f));
                var cameraGo = Track(new GameObject("Aim camera"));
                var camera = cameraGo.AddComponent<Camera>();
                camera.orthographic = true;
                camera.transform.position = new Vector3(10, 10, -10);
                var cursor = camera.WorldToScreenPoint(hero.transform.position + Vector3.up * 3);
                hero.ApplyAim(camera, cursor, .08f);
                Assert.That(hero.AimDegrees, Is.EqualTo(90 * (1 - Mathf.Exp(-1))).Within(.01f));
                for (var i = 0; i < 120; i++) hero.ApplyAim(camera, cursor, 1f / 60);
                Assert.That(hero.AimDegrees, Is.EqualTo(90).Within(.01f));
            }
            finally { Directory.Delete(root, true); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapGrid_Has50Cells_48Ppu_AndFourSolidBoundaries()
        {
            var map = Track(new GameObject("Map grid")).AddComponent<MapGridView>();
            map.Construct(_balance);
            yield return null; // Actual Start builds the configured grid.
            Assert.AreEqual(50, map.MapSizeCells);
            Assert.AreEqual(48, map.CellSizePixels);
            var floor = map.transform.Find("Grid floor").GetComponent<SpriteRenderer>();
            Assert.AreEqual(new Vector2(50, 50), floor.size);
            Assert.AreEqual(48, floor.sprite.pixelsPerUnit);
            Assert.AreEqual(4, map.GetComponentsInChildren<BoxCollider2D>().Length);
            var hero = SpawnHero(new Vector2(25, 25));
            var body = hero.GetComponent<Rigidbody2D>();
            foreach (var direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                var start = new Vector2(25, 25) + direction * 24;
                body.position = start;
                hero.transform.position = start;
                hero.ApplyMovement(direction);
                Physics2D.SyncTransforms();
                for (var i = 0; i < 100; i++) Physics2D.Simulate(.02f);
                var coordinate = direction.x != 0 ? body.position.x : body.position.y;
                var expected = direction.x + direction.y < 0 ? .4f : 49.6f;
                Assert.That(coordinate, Is.EqualTo(expected).Within(.03f), direction.ToString());
                Assert.IsNotNull(CoverQuery.Sweep(new Vector2(25, 25), direction, 30).collider);
            }
        }

        [UnityTest]
        public IEnumerator EveryCover_BlocksHero_ProjectileAndLos_AndPoolReusesProjectile()
        {
            var projectilePrefab = AssetDatabase.LoadAssetAtPath<CoverProbeProjectile>(
                "Assets/_JungleMaze/Prefabs/Diagnostics/CoverProbeProjectile.prefab");
            Assert.IsNotNull(projectilePrefab);
            var factory = new PooledPrefabFactory<CoverProbeProjectile>(_pool, projectilePrefab, 1);
            var shot = new ConfiguredShot(_balance, "RUBBER_GUN");
            CoverProbeProjectile first = null;
            foreach (var name in new[] { "AncientPillar", "CityWall", "BoulderRock", "AncientStatue" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/Obstacles/" + name + ".prefab");
                var cover = Track(Object.Instantiate(prefab));
                cover.transform.position = new Vector3(10, 10, 0);
                var collider = cover.GetComponent<BoxCollider2D>();
                Assert.IsFalse(collider.isTrigger);
                var hero = SpawnHero(new Vector2(6, 10));
                hero.ApplyMovement(Vector2.right);
                Physics2D.SyncTransforms();
                for (var i = 0; i < 100; i++) Physics2D.Simulate(.02f);
                Assert.That(hero.GetComponent<Rigidbody2D>().position.x,
                    Is.EqualTo(10 - collider.bounds.extents.x - .4f).Within(.03f), name);
                Assert.AreEqual(collider, CoverQuery.Sweep(new Vector2(6, 10), Vector2.right, 8).collider, name);
                var pose = new SpawnPose(new Vector3(6, 10), Quaternion.identity);
                var projectile = factory.Create(in pose);
                if (first == null) first = projectile;
                else Assert.AreSame(first, projectile, "Actual projectile must be reused across covers.");
                projectile.Launch(in shot, Vector2.right);
                projectile.Advance(1); // Crosses the whole cover in one step: must not tunnel.
                Assert.AreEqual(collider, projectile.LastHit, name);
                Assert.That(projectile.transform.position.x + .09f, Is.LessThanOrEqualTo(collider.bounds.min.x + .001f),
                    "The visible projectile nose must also stop before the cover.");
                Assert.IsFalse(projectile.gameObject.activeSelf);
                Assert.AreEqual(0, _pool.CountActive(projectilePrefab));
                Object.DestroyImmediate(hero.gameObject);
                Object.DestroyImmediate(cover);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Projectile_UsesChangedJsonSpeedRange_AndExpiresBackIntoPool()
        {
            var root = Path.Combine(Path.GetTempPath(), "jungle-shot-" + System.Guid.NewGuid().ToString("N"));
            var directory = Path.Combine(root, BalanceConfigProvider.Folder);
            Directory.CreateDirectory(directory);
            try
            {
                foreach (var file in new[] { "match.json", "weapons.json", "equipments.json" })
                {
                    var data = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, BalanceConfigProvider.Folder, file));
                    if (file == "weapons.json") data = data.Replace("\"projectileSpeed\": 18", "\"projectileSpeed\": 36");
                    File.WriteAllText(Path.Combine(directory, file), data);
                }
                var changed = new BalanceConfigProvider(root, null);
                Assert.IsNull(changed.LastError);
                var prefab = AssetDatabase.LoadAssetAtPath<CoverProbeProjectile>("Assets/_JungleMaze/Prefabs/Diagnostics/CoverProbeProjectile.prefab");
                var factory = new PooledPrefabFactory<CoverProbeProjectile>(_pool, prefab, 1);
                var normal = new ConfiguredShot(_balance, "RUBBER_GUN");
                var fast = new ConfiguredShot(changed, "RUBBER_GUN");
                var pose = new SpawnPose(new Vector3(15, 15), Quaternion.identity);
                var bullet = factory.Create(in pose);
                bullet.Launch(in normal, Vector2.right);
                bullet.Advance(.05f);
                Assert.That(bullet.transform.position.x, Is.EqualTo(15.9f).Within(.001f));
                _pool.Despawn(bullet);
                bullet = factory.Create(in pose);
                bullet.Launch(in fast, Vector2.right);
                bullet.Advance(.05f);
                Assert.That(bullet.transform.position.x, Is.EqualTo(16.8f).Within(.001f));
                bullet.Advance(1);
                Assert.IsFalse(bullet.gameObject.activeSelf);
                Assert.AreEqual(0, _pool.CountActive(prefab));
            }
            finally { Directory.Delete(root, true); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeroAndCamera_StayStillWithoutInput_AndCameraClampsAtMapEdge()
        {
            var hero = SpawnHero(new Vector2(25, 25));
            hero.ApplyMovement(Vector2.zero);
            var cameraGo = Track(new GameObject("Follow camera"));
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.aspect = 16f / 9;
            cameraGo.transform.position = new Vector3(25, 25, -10);
            var follow = cameraGo.AddComponent<HeroCameraFollow>();
            follow.Construct(_balance);
            follow.Target = hero.transform;
            for (var i = 0; i < 30; i++)
            {
                Physics2D.Simulate(.02f);
                yield return null;
                Assert.That(Vector3.Distance(hero.transform.position, new Vector3(25, 25)), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(cameraGo.transform.position, new Vector3(25, 25, -10)), Is.LessThan(.0001f));
            }
            hero.GetComponent<Rigidbody2D>().position = new Vector2(1, 1);
            hero.transform.position = new Vector3(1, 1);
            Physics2D.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(cameraGo.transform.position.x, Is.EqualTo(8 * 16f / 9).Within(.001f));
            Assert.That(cameraGo.transform.position.y, Is.EqualTo(8).Within(.001f));
        }

        private HeroController SpawnHero(Vector2 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/Hero.prefab");
            var go = Track(Object.Instantiate(prefab));
            var hero = go.GetComponent<HeroController>();
            hero.enabled = false; // Deterministic input through the same kinematics methods used by Update/FixedUpdate.
            hero.Construct(_balance);
            go.GetComponent<Rigidbody2D>().position = position;
            go.transform.position = position;
            return hero;
        }

        private GameObject Track(GameObject go) { _objects.Add(go); return go; }
    }
}
#endif
