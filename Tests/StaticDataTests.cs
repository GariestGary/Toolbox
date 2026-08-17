using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal sealed class StaticDataTests
    {
        private static readonly FieldInfo SettingsCacheField = typeof(StaticData).GetField(
            "_settings",
            BindingFlags.Static | BindingFlags.NonPublic);

        private SettingsData _originalCache;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(SettingsCacheField);
            _originalCache = SettingsCacheField.GetValue(null) as SettingsData;
        }

        [TearDown]
        public void TearDown()
        {
            SettingsCacheField.SetValue(null, _originalCache);
        }

        [Test]
        public void SettingsCanBeResolved()
        {
            Assert.IsNotNull(StaticData.Settings);
        }

        [Test]
        public void RepeatedSettingsReadsReturnSameInstance()
        {
            SettingsCacheField.SetValue(null, null);

            var first = StaticData.Settings;
            var second = StaticData.Settings;

            Assert.AreSame(first, second);
        }

        [Test]
        public void CreateSettingsWarmsCache()
        {
            SettingsCacheField.SetValue(null, null);

            StaticData.CreateSettings();

            var cachedSettings = SettingsCacheField.GetValue(null) as SettingsData;
            Assert.IsNotNull(cachedSettings);
            Assert.AreSame(cachedSettings, StaticData.Settings);
        }

        [Test]
        public void HasSettingsDoesNotPopulateCache()
        {
            SettingsCacheField.SetValue(null, null);

            var hasSettings = StaticData.HasSettings;

            Assert.IsTrue(hasSettings);
            Assert.IsNull(SettingsCacheField.GetValue(null));
        }

        [Test]
        public void SettingsMutationIsVisibleThroughSubsequentReads()
        {
            var settings = StaticData.Settings;
            var originalValue = settings.UseMessageCaching;

            try
            {
                settings.UseMessageCaching = !originalValue;

                Assert.AreSame(settings, StaticData.Settings);
                Assert.AreEqual(!originalValue, StaticData.Settings.UseMessageCaching);
            }
            finally
            {
                settings.UseMessageCaching = originalValue;
            }
        }

        [Test]
        public void DestroyedCachedReferenceIsResolvedAgain()
        {
            var destroyedSettings = ScriptableObject.CreateInstance<SettingsData>();
            SettingsCacheField.SetValue(null, destroyedSettings);
            Object.DestroyImmediate(destroyedSettings);

            var resolvedSettings = StaticData.Settings;

            Assert.IsNotNull(resolvedSettings);
            Assert.AreNotSame(destroyedSettings, resolvedSettings);
        }
    }
}
