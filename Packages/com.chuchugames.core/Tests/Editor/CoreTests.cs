using System;
using System.IO;
using NUnit.Framework;

namespace ChuchuGames.Core.Tests
{
    public class StateMachineTests
    {
        enum S { A, B, C }

        [Test]
        public void EnterExitAndChanged_InOrder()
        {
            var log = "";
            var sm = new StateMachine<S>(S.A).OnExit(S.A, () => log += "exitA ").OnEnter(S.B, () => log += "enterB ");
            sm.Changed += (f, t) => log += $"{f}->{t}";
            sm.Go(S.B);
            Assert.AreEqual("exitA enterB A->B", log);
            Assert.AreEqual(S.B, sm.Current);
        }

        [Test]
        public void Rules_RefuseUndeclared()
        {
            var sm = new StateMachine<S>(S.A).Allow(S.A, S.B).Allow(S.B, S.C);
            Assert.IsFalse(sm.CanGo(S.C));
            Assert.Throws<InvalidOperationException>(() => sm.Go(S.C));
            sm.Go(S.B);
            sm.Go(S.C);
            Assert.AreEqual(S.C, sm.Current);
        }
    }

    public class EventBusTests
    {
        [Test]
        public void PublishSubscribeDispose()
        {
            var bus = new EventBus();
            int sum = 0;
            var token = bus.Subscribe<int>(n => sum += n);
            bus.Subscribe<string>(_ => sum += 100);
            bus.Publish(5);
            token.Dispose();
            bus.Publish(5);
            Assert.AreEqual(5, sum);
        }
    }

    public class SaveSlotsTests
    {
        string _dir;
        SaveSlots<string> _slots;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "chuchu-save-tests-" + Guid.NewGuid().ToString("N"));
            _slots = new SaveSlots<string>(_dir, s => s, s => s.StartsWith("{") ? s : throw new FormatException());
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void SaveLoad_RoundTrip_AndOverwriteKeepsBackup()
        {
            Assert.IsFalse(_slots.Exists(1));
            _slots.Save(1, "{v1}");
            _slots.Save(1, "{v2}");
            Assert.AreEqual("{v2}", _slots.Load(1));
            Assert.IsTrue(File.Exists(_slots.PathFor(1) + ".bak"));
            Assert.IsFalse(File.Exists(_slots.PathFor(1) + ".tmp"));
        }

        [Test]
        public void CorruptSave_FallsBackToBackup()
        {
            _slots.Save(2, "{good}");
            _slots.Save(2, "{newer}");
            File.WriteAllText(_slots.PathFor(2), "garbage");
            Assert.AreEqual("{good}", _slots.Load(2));
        }

        [Test]
        public void Delete_RemovesEverything_AndBadSlotThrows()
        {
            _slots.Save(3, "{x}");
            _slots.Save(3, "{y}");
            _slots.Delete(3);
            Assert.IsFalse(_slots.Exists(3));
            Assert.IsNull(_slots.Load(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => _slots.Save(4, "{z}"));
        }
    }
}
