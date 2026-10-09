using System;
using System.IO;

namespace ChuchuGames.Core
{
    /// <summary>
    /// Numbered save slots stored as text files. Writes are atomic: the data goes to a temp file
    /// that then replaces the old save, which is kept as a .bak. A corrupt or unreadable save
    /// falls back to the backup. The serializer is injected, so in Unity pass JsonUtility and
    /// Application.persistentDataPath.
    /// </summary>
    public sealed class SaveSlots<T> where T : class
    {
        readonly string _folder;
        readonly string _prefix;
        readonly Func<T, string> _serialize;
        readonly Func<string, T> _deserialize;

        public int SlotCount { get; }

        public SaveSlots(string folder, Func<T, string> serialize, Func<string, T> deserialize,
            int slotCount = 3, string prefix = "slot")
        {
            _folder = folder ?? throw new ArgumentNullException(nameof(folder));
            _serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
            _deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
            _prefix = prefix;
            SlotCount = slotCount;
        }

        public string PathFor(int slot)
        {
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Slots are 1..{SlotCount}");
            return Path.Combine(_folder, $"{_prefix}{slot}.json");
        }

        public bool Exists(int slot) => File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak");

        public DateTime? LastWritten(int slot) => File.Exists(PathFor(slot)) ? File.GetLastWriteTime(PathFor(slot)) : (DateTime?)null;

        /// <summary>The slot's data, falling back to the backup; null if neither can be read.</summary>
        public T Load(int slot)
        {
            var path = PathFor(slot);
            return TryRead(path) ?? TryRead(path + ".bak");
        }

        public void Save(int slot, T data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Directory.CreateDirectory(_folder);
            var path = PathFor(slot);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, _serialize(data));
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
            else File.Move(tmp, path);
        }

        public void Delete(int slot)
        {
            var path = PathFor(slot);
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        T TryRead(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return _deserialize(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
