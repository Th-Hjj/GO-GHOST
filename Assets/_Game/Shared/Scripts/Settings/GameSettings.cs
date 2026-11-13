using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Player preferences and key bindings. Stored apart from the save, so they survive New Game.
public static class GameSettings
{
    public const int Steps = 10;
    public const int MoveLeft = 0, MoveRight = 1, Jump = 2, Dodge = 3, Fire = 4, RapidFire = 5, Reload = 6, Pause = 7;
    public static readonly string[] BindingNames = { "Move left", "Move right", "Jump", "Dodge", "Fire", "Rapidly fire", "Reload", "Pause / resume" };
    static readonly string[] DefaultPaths =
    {
        "<Keyboard>/a", "<Keyboard>/d", "<Keyboard>/space", "<Keyboard>/leftShift",
        "<Mouse>/leftButton", "<Mouse>/rightButton", "<Keyboard>/r", "<Keyboard>/escape"
    };
    static readonly string[] paths = (string[])DefaultPaths.Clone();
    public static int Master = Steps, Music = Steps, Effects = Steps, Voice = Steps;
    public static bool Fullscreen = true, VSync = true, ReduceFlashing, NoticeSeen;
    public static int Width, Height;
    public static event Action BindingsChanged;
    static bool loaded;

    public static float MusicScale => Music / (float)Steps;
    public static float EffectsScale => Effects / (float)Steps;
    public static float VoiceScale => Voice / (float)Steps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { loaded = false; BindingsChanged = null; }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        Master = PlayerPrefs.GetInt("settings.master", Steps);
        Music = PlayerPrefs.GetInt("settings.music", Steps);
        Effects = PlayerPrefs.GetInt("settings.effects", Steps);
        Voice = PlayerPrefs.GetInt("settings.voice", Steps);
        Fullscreen = PlayerPrefs.GetInt("settings.fullscreen", Screen.fullScreen ? 1 : 0) != 0;
        VSync = PlayerPrefs.GetInt("settings.vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
        ReduceFlashing = PlayerPrefs.GetInt("settings.reduceFlashing", 0) != 0;
        NoticeSeen = PlayerPrefs.GetInt("settings.noticeSeen", 0) != 0;
        Width = PlayerPrefs.GetInt("settings.width", 0);
        Height = PlayerPrefs.GetInt("settings.height", 0);
        for (int i = 0; i < paths.Length; i++) paths[i] = PlayerPrefs.GetString("settings.binding." + i, DefaultPaths[i]);
        Apply(Width > 0 && Height > 0);
        ApplyBindings();
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("settings.master", Master);
        PlayerPrefs.SetInt("settings.music", Music);
        PlayerPrefs.SetInt("settings.effects", Effects);
        PlayerPrefs.SetInt("settings.voice", Voice);
        PlayerPrefs.SetInt("settings.fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("settings.vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("settings.reduceFlashing", ReduceFlashing ? 1 : 0);
        PlayerPrefs.SetInt("settings.noticeSeen", NoticeSeen ? 1 : 0);
        PlayerPrefs.SetInt("settings.width", Width);
        PlayerPrefs.SetInt("settings.height", Height);
        for (int i = 0; i < paths.Length; i++) PlayerPrefs.SetString("settings.binding." + i, paths[i]);
        PlayerPrefs.Save();
    }

    // Audio and VSync always apply; the window only changes when the player asked for it.
    public static void Apply(bool display)
    {
        AudioListener.volume = Mathf.Clamp01(Master / (float)Steps);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        if (!display || Application.isEditor) return;
        // The browser page owns the canvas size; only fullscreen can change there.
        if (Application.platform == RuntimePlatform.WebGLPlayer) { Screen.fullScreen = Fullscreen; return; }
        int width = Width > 0 ? Width : Screen.currentResolution.width, height = Height > 0 ? Height : Screen.currentResolution.height;
        Screen.SetResolution(width, height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    public static string BindingPath(int index) => paths[index];

    public static string BindingLabel(int index)
    {
        string path = paths[index];
        switch (path)
        {
            case "<Mouse>/leftButton": return "Left mouse";
            case "<Mouse>/rightButton": return "Right mouse";
            case "<Mouse>/middleButton": return "Middle mouse";
            case "<Keyboard>/escape": return "Esc";
        }
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    // A key that another action uses swaps with it, so no action is left without a key.
    public static void Bind(int index, string path)
    {
        int other = Array.IndexOf(paths, path);
        if (other >= 0 && other != index) paths[other] = paths[index];
        paths[index] = path;
        Save();
        ApplyBindings();
    }

    public static void ResetBindings()
    {
        Array.Copy(DefaultPaths, paths, paths.Length);
        Save();
        ApplyBindings();
    }

    public static bool Pressed(int index)
    {
        return InputSystem.FindControl(paths[index]) is ButtonControl control && control.wasPressedThisFrame;
    }

    public static void ApplyBindings()
    {
        ApplyTo(InputSystem.actions);
        BindingsChanged?.Invoke();
    }

    // The combat actor reads its own copy of the actions, which starts without the player's keys.
    public static void ApplyTo(InputActionAsset asset)
    {
        if (asset == null) return;
        Override(asset, "Player/Move", "<Keyboard>/a", paths[MoveLeft]);
        Override(asset, "Player/Move", "<Keyboard>/d", paths[MoveRight]);
        Override(asset, "Player/Jump", "<Keyboard>/space", paths[Jump]);
        Override(asset, "Player/Dodge", "<Keyboard>/leftShift", paths[Dodge]);
        Override(asset, "Player/Attack", "<Mouse>/leftButton", paths[Fire]);
    }

    static void Override(InputActionAsset asset, string actionPath, string original, string path)
    {
        var action = asset.FindAction(actionPath);
        if (action == null) return;
        for (int i = 0; i < action.bindings.Count; i++)
            if (action.bindings[i].path == original) action.ApplyBindingOverride(i, path);
    }
}
