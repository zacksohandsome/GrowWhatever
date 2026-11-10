using System;
using System.IO;
using GrowWhatever.Core;
using NUnit.Framework;

namespace GrowWhatever.Tests
{
    public sealed class FarmSaveStoreTests
    {
        private string folder, path;
        [SetUp] public void SetUp()
        {
            folder=Path.Combine(Path.GetTempPath(),"GrowWhatever-synthetic-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); path=Path.Combine(folder,"farm.json");
        }
        [TearDown] public void TearDown()
        {
            foreach(string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
        [Test] public void AtomicSaveCanBeReloadedAndReplacedWithoutDuplicatingGoods()
        {
            var store=new FarmSaveStore(path); var game=FarmProgressionTests.Prepared(); game.QueueRecipe("flour");
            Assert.That(store.Write(game.Capture(DateTime.UtcNow.Ticks)),Is.True);
            Assert.That(store.Read(out var state),Is.EqualTo(SaveReadStatus.Loaded));
            var restored=FarmGame.Restore(FarmProgressionTests.Definitions(),FarmProgressionTests.Rules(),state);
            restored.Advance(30); Assert.That(store.Write(restored.Capture(DateTime.UtcNow.Ticks)),Is.True);
            store.Read(out state); restored=FarmGame.Restore(FarmProgressionTests.Definitions(),FarmProgressionTests.Rules(),state);
            restored.Advance(300); Assert.That(restored.ProduceCount("flour"),Is.EqualTo(21));
            Assert.That(File.Exists(path+".tmp"),Is.False);
        }
        [TestCase("not a save")]
        [TestCase("{\"Version\":999}")]
        public void InvalidOrUnsupportedReadDoesNotOverwriteOriginal(string bytes)
        {
            File.WriteAllText(path,bytes);
            var store=new FarmSaveStore(path); var result=store.Read(out _);
            Assert.That(result,Is.Not.EqualTo(SaveReadStatus.Loaded));
            Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));
        }
        [Test] public void MissingSaveDoesNotCreateAFileDuringRead()
        {
            Assert.That(new FarmSaveStore(path).Read(out _),Is.EqualTo(SaveReadStatus.Missing));
            Assert.That(File.Exists(path),Is.False);
        }
    }
}
