using System.Collections;
using NUnit.Framework;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.TestTools;

namespace Orsuun.Client.Tests
{
    /// <summary>Boots the real grey-box and plays it. Any error logged by the game fails these tests.</summary>
    public class GreyBoxSmokeTest
    {
        private static GameRoot Root()
        {
            var root = Object.FindFirstObjectByType<GameRoot>();
            Assert.NotNull(root, "GameRoot did not boot.");
            // Tests exercise the local rules path; the server path is covered by the HTTP smoke script.
            root.Server.Disconnect();
            return root;
        }

        [UnityTest]
        public IEnumerator Lane_runs_and_the_hero_kills_mobs()
        {
            yield return null;
            GameRoot root = Root();
            root.SpeedMultiplier = 8;

            float deadline = Time.realtimeSinceStartup + 30f;
            while (root.Session.Lane.MobsKilled < 10 && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.GreaterOrEqual(root.Session.Lane.MobsKilled, 10);
            Assert.Greater(root.Session.Inventory.Sorn, 20_000);
        }

        [UnityTest]
        public IEnumerator Forge_attempt_plays_the_sequence_and_resolves()
        {
            yield return null;
            GameRoot root = Root();
            root.Session.Inventory.Sorn += 1_000_000;
            int levelBefore = root.Session.Weapon.UpgradeLevel;

            root.Forge.Open();
            root.Forge.StartAttempt(ForgeMethod.ScrollOfMercy);
            Assert.IsTrue(root.Forge.Busy, "The anvil sequence should be running.");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (root.Forge.Busy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(root.Forge.Busy);
            Assert.IsTrue(root.Forge.LastResult.HasValue);
            Assert.AreEqual(levelBefore, root.Forge.LastResult.Value.LevelBefore);
            Assert.AreEqual(root.Session.Weapon.UpgradeLevel, root.Forge.LastResult.Value.LevelAfter);

            root.Forge.Close();
            Assert.IsFalse(root.Forge.IsOpen);
        }
    }
}
