using System;
using System.IO;
using GrowWhatever.Core;
using NUnit.Framework;

namespace GrowWhatever.Tests
{
    public sealed class PlayerSettingsTests
    {
        [Test] public void PreviewCannotPersistUntilConfirmedAndExpiresAtDeadline()
        {
            var original = new DisplayChoice(1280,720);
            var preview = new DisplayPreview(original);
            preview.Begin(new DisplayChoice(1920,1080), 100);
            Assert.That(preview.Confirmed, Is.EqualTo(original));
            Assert.That(preview.Expire(114.9), Is.False);
            Assert.That(preview.Keep(115), Is.False);
            Assert.That(preview.IsActive, Is.False);
            Assert.That(preview.Confirmed, Is.EqualTo(original));
            preview.Begin(new DisplayChoice(1600,900), 200);
            Assert.That(preview.Keep(201), Is.True);
            Assert.That(preview.Confirmed.Width, Is.EqualTo(1600));
            preview.Begin(new DisplayChoice(1920,1080,true), 300);
            Assert.That(preview.Expire(400), Is.True);
            Assert.That(preview.Pending, Is.EqualTo(preview.Confirmed));
        }

        [Test] public void SmallerMonitorDowngradesAndTinyMonitorUsesBorderless()
        {
            var settings = new PlayerPreferences { Display = new DisplayChoice(3840,2160) };
            Assert.That(settings.Normalize(1920,1080), Is.True);
            Assert.That(settings.Display, Is.EqualTo(new DisplayChoice(1920,1080)));
            settings.Normalize(1024,600);
            Assert.That(settings.Display.Borderless, Is.True);
            Assert.That(PlayerPreferences.AvailableResolutions(1024,600), Is.Empty);
            Assert.That(PlayerPreferences.AvailableResolutions(1920,1080).Length, Is.EqualTo(3));
        }

        [Test] public void InvalidFieldsAreCorrectedWithoutDiscardingValidFields()
        {
            var settings = new PlayerPreferences { Master = float.NaN, Music = -10, Effects = 4,
                Display = new DisplayChoice(-1,500) };
            settings.Normalize(1920,1080);
            Assert.That(settings.Master, Is.EqualTo(1)); Assert.That(settings.Music, Is.Zero);
            Assert.That(settings.Effects, Is.EqualTo(1));
            Assert.That(settings.Display, Is.EqualTo(new DisplayChoice(1280,720)));
        }

        [Test] public void SavingAudioDuringPreviewKeepsConfirmedDisplayAcrossRestart()
        {
            WithStore((path, store) =>
            {
                var settings = new PlayerPreferences();
                var preview = new DisplayPreview(settings.Display);
                preview.Begin(new DisplayChoice(1920,1080,true), 0);
                settings.Master = 0.25f; settings.Effects = 0;
                Assert.That(store.Write(settings), Is.True);
                var restored = store.Read(out bool invalid);
                Assert.That(invalid, Is.False); Assert.That(restored.Display, Is.EqualTo(preview.Confirmed));
                Assert.That(restored.Master, Is.EqualTo(0.25f)); Assert.That(restored.Effects, Is.Zero);
                preview.Cancel(); Assert.That(settings.Master, Is.EqualTo(0.25f));
            });
        }

        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("{\"Version\":999}")]
        [TestCase("{}")]
        public void DamagedSettingsReturnDefaultsAndDoNotOverwriteOriginal(string content)
        {
            WithStore((path, store) =>
            {
                File.WriteAllText(path, content);
                var settings = store.Read(out bool invalid);
                Assert.That(invalid, Is.True); Assert.That(settings.Display.Width, Is.EqualTo(1280));
                Assert.That(settings.Effects, Is.EqualTo(0.7f)); Assert.That(File.ReadAllText(path), Is.EqualTo(content));
            });
        }

        [Test] public void PartialSettingsKeepDefaultsAndAtomicReplacementCanBeRead()
        {
            WithStore((path, store) =>
            {
                Assert.That(store.Read(out bool invalid).Master, Is.EqualTo(1)); Assert.That(invalid, Is.False);
                Assert.That(File.Exists(path), Is.False);
                File.WriteAllText(path, "{\"Version\":1,\"Master\":0.5}");
                var settings = store.Read(out invalid);
                Assert.That(invalid, Is.False); Assert.That(settings.Music, Is.EqualTo(0.7f));
                Assert.That(store.Write(settings), Is.True);
                settings.Music = 0.1f; Assert.That(store.Write(settings), Is.True);
                Assert.That(store.Read(out invalid).Music, Is.EqualTo(0.1f));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            });
        }

        private static void WithStore(Action<string, PlayerPreferencesStore> test)
        {
            string directory = Path.Combine(Path.GetTempPath(), "GrowWhatever-settings-synthetic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings.json");
            try { test(path, new PlayerPreferencesStore(path)); }
            finally
            {
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }
}
