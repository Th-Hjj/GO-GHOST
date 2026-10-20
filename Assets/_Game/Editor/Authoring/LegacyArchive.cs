#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Explicit maintenance commands only. The game never reads or imports the archive.
public static class LegacyArchive
{
    const string ArchiveRoot = "Legacy";
    const string AssetRoot = "Assets/_Legacy";
    const string PackageName = "GO-GHOST-Legacy.unitypackage";

    [Serializable] class Snapshot
    {
        public int schema;
        public string restore_root;
        public Package package;
        public ArchivedFile[] files;
        public ArchivedAsset[] assets;
    }
    [Serializable] class Package { public string file; public string sha256; }
    [Serializable] class ArchivedFile { public string path; public string sha256; public long bytes; }
    [Serializable] class ArchivedAsset { public string path; public string guid; public bool is_folder; }

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string ArchivePath => Path.Combine(ProjectRoot, ArchiveRoot);
    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    static string CheckedPath(string root, string relative)
    {
        Require(!string.IsNullOrEmpty(relative) && !Path.IsPathRooted(relative), "Invalid archive path.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        Require(full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Archive path escapes its root: " + relative);
        return full;
    }
    static bool IsArchivedPath(string path) => path == AssetRoot || path == AssetRoot + ".meta"
        || path.StartsWith(AssetRoot + "/", StringComparison.Ordinal);

    static Snapshot VerifySnapshot()
    {
        string manifest = Path.Combine(ArchivePath, "manifest.json");
        Require(File.Exists(manifest), "Legacy archive is absent. Recover the Legacy folder from version control or backup first.");
        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(manifest));
        Require(snapshot != null && snapshot.schema == 1 && snapshot.restore_root == AssetRoot
            && snapshot.package != null && snapshot.package.file == PackageName
            && snapshot.files != null && snapshot.assets != null, "Unsupported or incomplete Legacy manifest.");
        Require(Hash(Path.Combine(ArchivePath, PackageName)) == snapshot.package.sha256, "Legacy package hash differs from manifest.");
        Require(snapshot.files.Select(f => f.path).Distinct().Count() == snapshot.files.Length, "Duplicate archive file paths.");
        Require(snapshot.assets.Select(a => a.guid).Distinct().Count() == snapshot.assets.Length, "Duplicate archive GUIDs.");
        foreach (var file in snapshot.files)
        {
            Require(IsArchivedPath(file.path), "Unexpected asset root: " + file.path);
            string path = CheckedPath(ArchivePath, file.path);
            Require(File.Exists(path) && new FileInfo(path).Length == file.bytes && Hash(path) == file.sha256,
                "Legacy mirror differs from manifest: " + file.path);
        }
        foreach (var asset in snapshot.assets)
            Require(IsArchivedPath(asset.path) && !string.IsNullOrEmpty(asset.guid) && asset.guid.Length == 32,
                "Invalid archived asset identity.");
        return snapshot;
    }

    [MenuItem("GO GHOST/Legacy Archive/Verify Snapshot")]
    public static void VerifyArchive()
    {
        var snapshot = VerifySnapshot();
        Debug.Log($"Legacy archive verified: {snapshot.files.Length} files, {snapshot.assets.Length} GUIDs. No import performed.");
    }

    [MenuItem("GO GHOST/Legacy Archive/Restore Historical Assets")]
    public static void Restore()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
            "Finish Play Mode and compilation before restoring Legacy.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save the open scenes before restoring Legacy.");
        var snapshot = VerifySnapshot();
        Require(!Directory.Exists(Path.Combine(ProjectRoot, AssetRoot))
            && !File.Exists(Path.Combine(ProjectRoot, AssetRoot))
            && !File.Exists(Path.Combine(ProjectRoot, AssetRoot + ".meta")), "Assets/_Legacy already exists; restore never overwrites it.");
        foreach (var asset in snapshot.assets)
        {
            string current = AssetDatabase.GUIDToAssetPath(asset.guid);
            // Unity can retain a recently removed asset's GUID/path until the Editor restarts.
            Require(string.IsNullOrEmpty(current) || string.IsNullOrEmpty(
                AssetDatabase.AssetPathToGUID(current, AssetPathToGUIDOptions.OnlyExistingAssets)),
                "Archived GUID is already imported at " + current);
        }
        Debug.Log("Importing the verified local Legacy snapshot. Unity may show Missing signature; review that prompt in the Editor. After compilation, run Complete and Verify Restoration.");
        AssetDatabase.ImportPackage(Path.Combine(ArchivePath, PackageName), false);
        Debug.Log("Legacy import requested. Current Play/build entry is unchanged.");
    }

    [MenuItem("GO GHOST/Legacy Archive/Complete and Verify Restoration")]
    public static void CompleteRestoration()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling
            && !EditorUtility.scriptCompilationFailed, "Finish Play Mode and compilation before completing restoration.");
        var snapshot = VerifySnapshot();
        var folders = snapshot.assets.Where(a => a.is_folder).ToArray();
        // Unity's package importer skips empty directories. Validate the actual import
        // before restoring only their original folder metadata from the verified mirror.
        foreach (var file in snapshot.files)
        {
            string destination = CheckedPath(ProjectRoot, file.path);
            bool missingFolderMeta = !File.Exists(destination) && folders.Any(a => a.path + ".meta" == file.path);
            Require(missingFolderMeta || (File.Exists(destination) && Hash(destination) == file.sha256),
                "Imported snapshot differs: " + file.path);
        }
        var missing = folders.Where(a => !File.Exists(CheckedPath(ProjectRoot, a.path + ".meta"))).ToArray();
        foreach (var folder in missing)
        {
            string current = AssetDatabase.GUIDToAssetPath(folder.guid);
            Require(string.IsNullOrEmpty(current) || string.IsNullOrEmpty(
                AssetDatabase.AssetPathToGUID(current, AssetPathToGUIDOptions.OnlyExistingAssets)),
                "Archived folder GUID is already imported at " + current);
            string destination = CheckedPath(ProjectRoot, folder.path);
            Require(!Directory.Exists(destination), "Untracked folder already exists: " + folder.path);
        }
        foreach (var folder in missing.OrderBy(a => a.path.Length))
        {
            string destination = CheckedPath(ProjectRoot, folder.path);
            Directory.CreateDirectory(destination);
            File.Copy(CheckedPath(ArchivePath, folder.path + ".meta"), destination + ".meta", false);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        VerifyRestored();
    }

    [MenuItem("GO GHOST/Legacy Archive/Verify Restored Assets")]
    public static void VerifyRestored()
    {
        Require(!EditorApplication.isCompiling && !EditorUtility.scriptCompilationFailed, "Unity must finish compiling without errors.");
        var snapshot = VerifySnapshot();
        foreach (var file in snapshot.files)
        {
            string path = CheckedPath(ProjectRoot, file.path);
            Require(File.Exists(path) && Hash(path) == file.sha256, "Restored file differs: " + file.path);
        }
        foreach (var asset in snapshot.assets)
        {
            string path = CheckedPath(ProjectRoot, asset.path);
            Require(asset.is_folder ? Directory.Exists(path) : File.Exists(path),
                "Restored asset is absent: " + asset.path);
            Require(AssetDatabase.GUIDToAssetPath(asset.guid) == asset.path
                && AssetDatabase.AssetPathToGUID(asset.path, AssetPathToGUIDOptions.OnlyExistingAssets) == asset.guid,
                "Restored GUID differs: " + asset.path);
        }
        Debug.Log($"Legacy restore verified: {snapshot.files.Length} files and {snapshot.assets.Length} GUIDs match.");
    }
}
#endif
