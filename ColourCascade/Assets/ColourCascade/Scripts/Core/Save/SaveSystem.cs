using System;
using System.IO;
using System.Text;

namespace ColourCascade.Core
{
    public enum SaveSlot
    {
        Main,
        Temp,
        Backup,
    }

    public interface ISaveStorage
    {
        /// <summary>Returns the stored text, or null if that slot does not exist.</summary>
        string Read(SaveSlot slot);
        /// <summary>Writes atomically: the previous main save becomes the backup only after the new data is fully written.</summary>
        void WriteAtomic(string text);
        void DeleteAll();
    }

    /// <summary>
    /// File storage: save.json (main), save.json.tmp (in-flight write), save.json.bak (previous save).
    /// Write order: tmp is written and flushed, main is rotated to bak, tmp is renamed to main.
    /// A crash at any point leaves at least one complete, loadable file.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        readonly string mainPath;
        readonly string tempPath;
        readonly string backupPath;

        public FileSaveStorage(string directory, string fileName = "save.json")
        {
            Directory.CreateDirectory(directory);
            mainPath = Path.Combine(directory, fileName);
            tempPath = mainPath + ".tmp";
            backupPath = mainPath + ".bak";
        }

        public string MainPath => mainPath;

        string PathFor(SaveSlot slot) => slot == SaveSlot.Main ? mainPath : slot == SaveSlot.Temp ? tempPath : backupPath;

        public string Read(SaveSlot slot)
        {
            string path = PathFor(slot);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        public void WriteAtomic(string text)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            if (File.Exists(mainPath))
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Move(mainPath, backupPath);
            }
            File.Move(tempPath, mainPath);
        }

        public void DeleteAll()
        {
            foreach (var p in new[] { mainPath, tempPath, backupPath })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>In-memory storage for tests.</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public string Main, Temp, Backup;
        public bool FailWrites;
        public int Writes;

        public string Read(SaveSlot slot) => slot == SaveSlot.Main ? Main : slot == SaveSlot.Temp ? Temp : Backup;

        public void WriteAtomic(string text)
        {
            if (FailWrites) throw new IOException("Simulated write failure");
            Writes++;
            if (Main != null) Backup = Main;
            Main = text;
            Temp = null;
        }

        public void DeleteAll()
        {
            Main = Temp = Backup = null;
        }
    }

    public enum LoadStatus
    {
        NewPlayer,
        Loaded,
        RecoveredFromTemp,
        RecoveredFromBackup,
        /// <summary>Every save file was unreadable; started fresh (the corrupt files are kept until the next save).</summary>
        CorruptReset,
    }

    /// <summary>Loads and saves SaveData through an ISaveStorage, with corruption recovery.</summary>
    public sealed class SaveSystem
    {
        readonly ISaveStorage storage;
        readonly IClock clock;
        readonly int startingCoins;

        public SaveSystem(ISaveStorage storage, IClock clock, int startingCoins = 0)
        {
            this.storage = storage;
            this.clock = clock;
            this.startingCoins = startingCoins;
            Data = SaveData.CreateNew(clock.UtcNow, startingCoins);
        }

        public SaveData Data { get; private set; }
        public LoadStatus LastLoad { get; private set; }
        public string LastError { get; private set; }
        /// <summary>Optional diagnostics sink (Unity routes this to Debug.Log).</summary>
        public Action<string> Log;

        public LoadStatus Load()
        {
            bool anyFile = false;
            var order = new[] { SaveSlot.Main, SaveSlot.Temp, SaveSlot.Backup };
            for (int i = 0; i < order.Length; i++)
            {
                string text;
                try
                {
                    text = storage.Read(order[i]);
                }
                catch (Exception e)
                {
                    Log?.Invoke("Save read failed (" + order[i] + "): " + e.Message);
                    continue;
                }
                if (text == null) continue;
                anyFile = true;
                try
                {
                    Data = SaveCodec.Decode(text);
                    LastLoad = order[i] == SaveSlot.Main ? LoadStatus.Loaded : order[i] == SaveSlot.Temp ? LoadStatus.RecoveredFromTemp : LoadStatus.RecoveredFromBackup;
                    if (LastLoad != LoadStatus.Loaded) Log?.Invoke("Save recovered from " + order[i]);
                    return LastLoad;
                }
                catch (Exception e)
                {
                    Log?.Invoke("Save slot " + order[i] + " unreadable: " + e.Message);
                }
            }
            Data = SaveData.CreateNew(clock.UtcNow, startingCoins);
            LastLoad = anyFile ? LoadStatus.CorruptReset : LoadStatus.NewPlayer;
            return LastLoad;
        }

        /// <summary>Writes the current data. Returns false (keeping data in memory) if the write failed.</summary>
        public bool Save()
        {
            try
            {
                Data.LastSavedUtcTicks = clock.UtcNow.Ticks;
                Data.SaveCount++;
                storage.WriteAtomic(SaveCodec.Encode(Data));
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Log?.Invoke("Save failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Erases all progress and writes a fresh save.</summary>
        public void ResetAll()
        {
            Data = SaveData.CreateNew(clock.UtcNow, startingCoins);
            Save();
        }
    }
}
