using NUnit.Framework;

namespace ChuchuGames.Core.Tests
{
    public class CommandStackTests
    {
        int _value;

        ICommand Add(int n) => new DelegateCommand($"+{n}", () => _value += n, () => _value -= n);

        [SetUp]
        public void Reset() => _value = 0;

        [Test]
        public void UndoRedo_RestoresValues()
        {
            var s = new CommandStack();
            s.Execute(Add(1));
            s.Execute(Add(10));
            Assert.AreEqual(11, _value);

            Assert.IsTrue(s.Undo());
            Assert.AreEqual(1, _value);
            Assert.IsTrue(s.Redo());
            Assert.AreEqual(11, _value);
        }

        [Test]
        public void NewCommand_ClearsRedo()
        {
            var s = new CommandStack();
            s.Execute(Add(1));
            s.Undo();
            s.Execute(Add(5));
            Assert.IsFalse(s.CanRedo);
            Assert.AreEqual(5, _value);
        }

        [Test]
        public void EmptyStack_UndoRedoReturnFalse()
        {
            var s = new CommandStack();
            Assert.IsFalse(s.Undo());
            Assert.IsFalse(s.Redo());
        }

        [Test]
        public void Capacity_DropsOldest()
        {
            var s = new CommandStack(capacity: 2);
            s.Execute(Add(1));
            s.Execute(Add(2));
            s.Execute(Add(3));
            Assert.AreEqual(2, s.UndoCount);
            s.Undo();
            s.Undo();
            Assert.IsFalse(s.Undo());
            Assert.AreEqual(1, _value);
        }

        [Test]
        public void Changed_FiresOnEveryMutation()
        {
            var s = new CommandStack();
            int fired = 0;
            s.Changed += () => fired++;
            s.Execute(Add(1));
            s.Undo();
            s.Redo();
            s.Clear();
            Assert.AreEqual(4, fired);
        }
    }
}
