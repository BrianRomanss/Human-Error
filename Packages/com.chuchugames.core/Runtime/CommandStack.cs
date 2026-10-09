using System;
using System.Collections.Generic;

namespace ChuchuGames.Core
{
    /// <summary>A reversible action. Execute must be safe to call again after Undo (that is Redo).</summary>
    public interface ICommand
    {
        string Name { get; }
        void Execute();
        void Undo();
    }

    /// <summary>
    /// Undo/redo history (Command pattern). Executing a new command discards the redo branch.
    /// </summary>
    public sealed class CommandStack
    {
        readonly List<ICommand> _undo = new List<ICommand>();
        readonly Stack<ICommand> _redo = new Stack<ICommand>();
        readonly int _capacity;

        /// <summary>Raised after any execute, undo, redo or clear.</summary>
        public event Action Changed;

        /// <param name="capacity">Maximum undo depth; 0 means unlimited.</param>
        public CommandStack(int capacity = 0)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        public void Execute(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            command.Execute();
            _undo.Add(command);
            if (_capacity > 0 && _undo.Count > _capacity) _undo.RemoveAt(0);
            _redo.Clear();
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var command = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            command.Undo();
            _redo.Push(command);
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            var command = _redo.Pop();
            command.Execute();
            _undo.Add(command);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }
    }

    /// <summary>Convenience command built from two delegates.</summary>
    public sealed class DelegateCommand : ICommand
    {
        readonly Action _execute;
        readonly Action _undo;

        public string Name { get; }

        public DelegateCommand(string name, Action execute, Action undo)
        {
            Name = name;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        }

        public void Execute() => _execute();
        public void Undo() => _undo();
    }
}
