using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Data;
using UnityEditor;

namespace RichCoast.Tests
{
    /// <summary>
    /// The shipped tuning assets are what the game actually loads, while every other test runs
    /// against the authored defaults. These pin the two together, so an accidental Inspector edit
    /// (or a missing asset after a merge) fails the build instead of silently changing the game.
    /// </summary>
    public class ConfigAssetTests
    {
        private const string ProgressionAssetPath = "Assets/Game/Data/ProgressionConfig.asset";
        private const string TierTableAssetPath = "Assets/Game/Data/BallTierTable.asset";

        [Test]
        public void ProgressionAssetExistsAndMatchesTheAuthoredStages()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ProgressionConfigSO>(ProgressionAssetPath);
            Assert.That(asset, Is.Not.Null, $"missing {ProgressionAssetPath} — run Rich Coast/Set Up Project");

            var fromAsset = asset.ToProgression();
            var authored = DefaultProgression.Create();

            Assert.That(fromAsset.Stages.Count, Is.EqualTo(authored.Stages.Count));
            for (var i = 0; i < authored.Stages.Count; i++)
            {
                var a = authored.Stages[i];
                var b = fromAsset.Stages[i];
                Assert.That(b.FromLevel, Is.EqualTo(a.FromLevel), $"stage {i} level");
                Assert.That(b.Window.Min, Is.EqualTo(a.Window.Min), $"stage {i} window min");
                Assert.That(b.Window.Max, Is.EqualTo(a.Window.Max), $"stage {i} window max");
                Assert.That(b.ScoreBarTarget, Is.EqualTo(a.ScoreBarTarget), $"stage {i} target");
                Assert.That(b.Tightness, Is.EqualTo(a.Tightness).Within(1e-6f), $"stage {i} tightness");
                Assert.That(b.Palette ?? "", Is.EqualTo(a.Palette ?? ""), $"stage {i} palette");
            }
        }

        [Test]
        public void TierTableAssetExistsAndMatchesTheAuthoredLadder()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BallTierTableSO>(TierTableAssetPath);
            Assert.That(asset, Is.Not.Null, $"missing {TierTableAssetPath} — run Rich Coast/Set Up Project");

            var fromAsset = asset.ToTable();
            var authored = DefaultTierLadder.CreateTable();

            Assert.That(fromAsset.RadiusTableSize, Is.EqualTo(authored.RadiusTableSize));
            Assert.That(fromAsset.MaterialCount, Is.EqualTo(authored.MaterialCount));
            Assert.That(fromAsset.RadiusGrowth, Is.EqualTo(authored.RadiusGrowth).Within(1e-6f));

            for (var tier = 1; tier <= authored.MaterialCount + 4; tier++) // past the ladder, into the wrap
            {
                Assert.That(fromAsset.RadiusForTier(tier), Is.EqualTo(authored.RadiusForTier(tier)).Within(1e-4f),
                    $"radius at tier {tier}");
                var expected = authored.MaterialForTier(tier);
                var actual = fromAsset.MaterialForTier(tier);
                Assert.That(actual.Def.Name, Is.EqualTo(expected.Def.Name), $"material at tier {tier}");
                Assert.That(actual.Cycle, Is.EqualTo(expected.Cycle), $"cycle at tier {tier}");
                Assert.That(actual.Def.BaseColor, Is.EqualTo(expected.Def.BaseColor), $"colour at tier {tier}");
            }
        }

        [Test]
        public void TheGameSceneIsTheOnlyBuildScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.EqualTo(1), "the build should contain exactly the game scene");
            Assert.That(scenes[0].path, Is.EqualTo("Assets/Game/Scenes/Game.unity"));
            Assert.That(scenes[0].enabled, Is.True);
        }
    }
}
