using System;
using System.IO;
using System.Text;
using UnityEngine;

public enum PoliceArchiveLoadStatus { Loaded, CreatedEmpty, RecoveredBackup, RecoveredEmpty, UnsupportedVersion, Unavailable }

/// <summary>Versioned local JSON with atomic replacement, one backup and preservation of invalid originals.</summary>
public sealed class PoliceArchiveStore
{
    public const int CurrentVersion = 1;
    public string FilePath { get; }
    public PoliceArchiveLoadStatus LastLoadStatus { get; private set; }
    public string LastError { get; private set; }
    private bool writesBlocked;
    private bool loadAttempted;

    public PoliceArchiveStore(string filePath)
    {
        FilePath = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
    }

    public PoliceArchiveProfile Load()
    {
        loadAttempted = true;
        writesBlocked = false;
        LastError = null;
        var empty = new PoliceArchiveProfile();
        try
        {
            if (!File.Exists(FilePath))
            {
                // A backup can also recover a missing primary file.
                var recovered = ReadValidBackup();
                var profile = recovered ?? empty;
                if (!TrySave(profile, out var error)) return Unavailable(profile, error);
                LastLoadStatus = recovered != null ? PoliceArchiveLoadStatus.RecoveredBackup : PoliceArchiveLoadStatus.CreatedEmpty;
                return profile;
            }
            var data = JsonUtility.FromJson<PoliceArchiveData>(File.ReadAllText(FilePath));
            if (data != null && data.saveVersion > CurrentVersion)
            {
                writesBlocked = true; // Never overwrite a newer save with this older schema.
                LastLoadStatus = PoliceArchiveLoadStatus.UnsupportedVersion;
                LastError = "Save version is newer than supported; disk is preserved and saving is disabled.";
                return empty;
            }
            if (PoliceArchiveProfile.TryFromData(data, out var loaded))
            {
                LastLoadStatus = PoliceArchiveLoadStatus.Loaded;
                return loaded;
            }
            return RecoverInvalid(empty);
        }
        catch (ArgumentException) { return RecoverInvalid(empty); } // Invalid JSON.
        catch (IOException e) { return Unavailable(empty, e.Message); }
        catch (UnauthorizedAccessException e) { return Unavailable(empty, e.Message); }
    }

    private PoliceArchiveProfile RecoverInvalid(PoliceArchiveProfile empty)
    {
        try
        {
            // Preserve evidence before any replacement. Failure to preserve means read-only fallback.
            File.Copy(FilePath, FilePath + ".invalid-" + Guid.NewGuid().ToString("N"));
            var recovered = ReadValidBackup();
            var profile = recovered ?? empty;
            if (!TrySave(profile, out var error)) return Unavailable(profile, error);
            LastLoadStatus = recovered != null ? PoliceArchiveLoadStatus.RecoveredBackup : PoliceArchiveLoadStatus.RecoveredEmpty;
            LastError = "Invalid save preserved; " + (recovered != null ? "valid backup restored." : "empty archive created.");
            return profile;
        }
        catch (IOException e) { return Unavailable(empty, e.Message); }
        catch (UnauthorizedAccessException e) { return Unavailable(empty, e.Message); }
    }

    private PoliceArchiveProfile ReadValidBackup()
    {
        string backup = FilePath + ".bak";
        if (!File.Exists(backup)) return null;
        try
        {
            var data = JsonUtility.FromJson<PoliceArchiveData>(File.ReadAllText(backup));
            return PoliceArchiveProfile.TryFromData(data, out var profile) ? profile : null;
        }
        catch (ArgumentException) { return null; }
    }

    private PoliceArchiveProfile Unavailable(PoliceArchiveProfile fallback, string message)
    {
        writesBlocked = true;
        LastLoadStatus = PoliceArchiveLoadStatus.Unavailable;
        LastError = message;
        return fallback; // A save failure does not prevent a fresh gameplay session.
    }

    public bool TrySave(PoliceArchiveProfile profile, out string error)
    {
        error = null;
        if (!loadAttempted) { error = "Load the archive before saving so its schema and existing data are checked."; return false; }
        if (writesBlocked) { error = LastError ?? "Archive is read-only after load failure."; return false; }
        if (profile == null) { error = "Archive profile is required."; return false; }
        string temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(profile.ToData(), true));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // Never delete the primary first. If replacement fails, the prior save remains intact.
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
            return true;
        }
        catch (IOException e) { error = e.Message; return false; }
        catch (UnauthorizedAccessException e) { error = e.Message; return false; }
        catch (NotSupportedException e) { error = e.Message; return false; }
    }
}
