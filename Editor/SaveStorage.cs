namespace ValheimEditor;

public static class SaveStorage
{
    public static string Write(string target, byte[] output, byte[]? expected, string backupFolder)
    {
        _ = new SaveFile(output);
        bool exists = File.Exists(target);
        if (exists != (expected != null) || (exists && !File.ReadAllBytes(target).SequenceEqual(expected!)))
            throw new IOException(Appearance.T("Файл изменился после открытия. Откройте его заново.", "The file changed since opening. Reopen it before saving."));
        Directory.CreateDirectory(backupFolder);
        string backup = Path.Combine(backupFolder, Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".bak");
        string temporary = Path.Combine(Path.GetDirectoryName(target)!, ".editor-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(output);
                stream.Flush(true);
            }
            if (exists)
            {
                if (!File.ReadAllBytes(target).SequenceEqual(expected!)) throw new IOException(Appearance.T("Сейв изменился во время сохранения.", "The save changed while saving."));
                // Replace atomically and retain the exact previous on-disk file as a backup.
                File.Replace(temporary, target, backup);
            }
            else File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return exists ? backup : "";
    }
}
