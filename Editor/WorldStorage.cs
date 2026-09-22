namespace ValheimEditor;

public static class WorldStorage
{
    public static string WorldBackupFolder(string worldsFolder, string worldName) => Path.Combine(worldsFolder, "Backups", worldName);

    /// <summary>Atomically replaces several world files. Every target must still match the
    /// bytes seen when the world was opened; the exact on-disk versions are kept as
    /// stamped .bak copies inside backupFolder.</summary>
    public static List<string> Write(IEnumerable<(string Target, byte[] Output, byte[] Expected)> files, string backupFolder)
    {
        var list = files.ToList();
        foreach (var (target, _, expected) in list)
        {
            if (!File.Exists(target)) throw new IOException(Appearance.T("Файл мира не найден.", "World file not found."));
            if (!File.ReadAllBytes(target).SequenceEqual(expected))
                throw new IOException(Appearance.T("Файл мира изменился после открытия. Откройте его заново.", "The world file changed since opening. Reopen it before saving."));
        }
        Directory.CreateDirectory(backupFolder);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var staged = new List<(string Temp, string Target, string Backup, byte[] Expected)>();
        var backups = new List<string>();
        try
        {
            foreach (var (target, output, expected) in list)
            {
                string temp = Path.Combine(Path.GetDirectoryName(target)!, ".editor-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(output);
                    stream.Flush(true);
                }
                string backup = Path.Combine(backupFolder, Path.GetFileName(target) + "." + stamp + "." + Guid.NewGuid().ToString("N") + ".bak");
                staged.Add((temp, target, backup, expected));
            }
            foreach (var (temp, target, backup, expected) in staged)
            {
                if (!File.ReadAllBytes(target).SequenceEqual(expected))
                    throw new IOException(Appearance.T("Сейв изменился во время сохранения.", "The save changed while saving."));
                File.Replace(temp, target, backup);
                backups.Add(backup);
            }
        }
        finally
        {
            foreach (var (temp, _, _, _) in staged) if (File.Exists(temp)) File.Delete(temp);
        }
        return backups;
    }

    /// <summary>Atomically replaces one file, keeping the current on-disk version as a stamped backup.</summary>
    public static string ReplaceFile(string target, byte[] output, byte[] expected, string backupFolder)
    {
        if (!File.Exists(target)) throw new IOException(Appearance.T("Файл не найден.", "File not found."));
        if (!File.ReadAllBytes(target).SequenceEqual(expected))
            throw new IOException(Appearance.T("Файл изменился после открытия. Откройте его заново.", "The file changed since opening. Reopen it before saving."));
        Directory.CreateDirectory(backupFolder);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backup = Path.Combine(backupFolder, Path.GetFileName(target) + "." + stamp + "." + Guid.NewGuid().ToString("N") + ".bak");
        string temp = Path.Combine(Path.GetDirectoryName(target)!, ".editor-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(output);
                stream.Flush(true);
            }
            if (!File.ReadAllBytes(target).SequenceEqual(expected))
                throw new IOException(Appearance.T("Сейв изменился во время сохранения.", "The save changed while saving."));
            File.Replace(temp, target, backup);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return backup;
    }
}
