using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GAME MANAGER (persistent singleton).
/// Lives on an empty object in the Title scene and survives every scene load (DontDestroyOnLoad).
/// Holds the current SaveData so Battle and Upgrade scenes all read/write the same object.
///  - NewGame():   fresh SaveData, saves it, loads Battle. (The ONLY thing that resets commitments.)
///  - Continue():  loads save.json, loads Battle.
///  - Autosave():  call after any change that should persist (death, picking a level, committing an ability).
///  - OnPlayerDied(): call from your Battle death handling; bumps run count, autosaves, goes to Upgrade.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public SaveData Data { get; private set; }

    public const string TitleScene = "Title";
    public const string BattleScene = "Battle";
    public const string UpgradeScene = "Upgrade";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }   // a duplicate (e.g. returning to Title) removes itself
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Data = SaveSystem.Load();
    }

    public void NewGame()
    {
        Data = new SaveData();
        Autosave();
        SceneManager.LoadScene(BattleScene);
    }

    public void Continue()
    {
        Data = SaveSystem.Load();
        SceneManager.LoadScene(BattleScene);
    }

    public void Autosave() => SaveSystem.Save(Data);

    public void OnPlayerDied()
    {
        Data.runNumber++;
        Autosave();
        SceneManager.LoadScene(UpgradeScene);
    }
}