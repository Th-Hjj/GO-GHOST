using System.IO;
using UnityEngine;

/// <summary>
/// SAVE SYSTEM (static helper).
/// Reads and writes save.json in Application.persistentDataPath.
///  - HasSave():  used by the title screen to enable/disable "Continue".
///  - Save():     writes to a temp file first, then swaps it in, so a crash mid-save can't corrupt the real file.
///  - Load():     returns a fresh SaveData if the file is missing or unreadable.
///  - Delete():   wipes the save (use for a "delete save" option if you ever want one).
/// </summary>
public static class SaveSystem
{
    static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    public static bool HasSave() => File.Exists(FilePath);

    public static void Save(SaveData data)
    {
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
        if (File.Exists(FilePath)) File.Delete(FilePath);
        File.Move(tmp, FilePath);
    }

    public static SaveData Load()
    {
        if (!HasSave()) return new SaveData();
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData(); }
        catch { return new SaveData(); }
    }

    public static void Delete() { if (HasSave()) File.Delete(FilePath); }
}