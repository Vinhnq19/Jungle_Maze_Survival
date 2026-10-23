using JungleMaze.Presentation.Player;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.App
{
    public sealed class HeroAimTests
    {
        [TestCase(5, 0, 0)]
        [TestCase(0, 5, 90)]
        [TestCase(-5, 0, 180)]
        [TestCase(0, -5, -90)]
        public void AimUsesHeroPosition_NotScreenCentre(float dx, float dy, float expected)
        {
            var go = new GameObject("Offset test camera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 8;
                camera.transform.position = new Vector3(20, 20, -10);
                var hero = new Vector3(23, 22, 0);
                var cursor = camera.WorldToScreenPoint(hero + new Vector3(dx, dy, 0));
                Assert.IsTrue(HeroAim.TryGetTargetDegrees(camera, hero, cursor, out var actual));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(expected, actual)), Is.LessThan(.001f));
                Assert.IsFalse(HeroAim.TryGetTargetDegrees(camera, hero, camera.WorldToScreenPoint(hero), out _));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
