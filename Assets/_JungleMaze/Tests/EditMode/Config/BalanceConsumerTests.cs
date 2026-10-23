using System;
using System.IO;
using JungleMaze.App.Player;
using JungleMaze.Infrastructure.Config;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.Config
{
    public sealed class BalanceConsumerTests
    {
        [Test]
        public void ChangingOnlyWeaponJson_ChangesConsumedShotValues_AfterReload()
        {
            var root = Path.Combine(Path.GetTempPath(), "jungle-balance-" + Guid.NewGuid().ToString("N"));
            var directory = Path.Combine(root, BalanceConfigProvider.Folder);
            Directory.CreateDirectory(directory);
            try
            {
                foreach (var file in new[] { "match.json", "weapons.json", "equipments.json" })
                    File.Copy(Path.Combine(Application.streamingAssetsPath, BalanceConfigProvider.Folder, file), Path.Combine(directory, file));
                var before = new BalanceConfigProvider(root, null);
                Assert.IsNull(before.LastError);
                var shot = new ConfiguredShot(before, "RUBBER_GUN");
                var path = Path.Combine(directory, "weapons.json");
                File.WriteAllText(path, File.ReadAllText(path).Replace("\"projectileSpeed\": 18", "\"projectileSpeed\": 36")
                    .Replace("\"range\": 6, \"fireRatePerSecond\": 3", "\"range\": 9, \"fireRatePerSecond\": 6"));
                var after = new BalanceConfigProvider(root, null);
                Assert.IsNull(after.LastError);
                var changed = new ConfiguredShot(after, "RUBBER_GUN");
                Assert.AreEqual(18, shot.Speed);
                Assert.AreEqual(36, changed.Speed);
                Assert.AreEqual(9, changed.Range);
                Assert.That(changed.Interval, Is.EqualTo(1f / 6).Within(.00001f));
                Assert.Throws<ArgumentException>(() => new ConfiguredShot(after, "HAND"));
                Assert.Throws<ArgumentException>(() => new ConfiguredShot(after, "MISSING"));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
