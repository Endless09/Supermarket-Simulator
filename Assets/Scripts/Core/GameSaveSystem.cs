using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles save-file IO and delegates world-state capture/apply to the game coordinator.
/// </summary>
public class GameSaveSystem : MonoBehaviour
{
    [SerializeField] private string saveFileName = "savegame.json";

    private GameManager gameManager;

    public void Initialize(GameManager owner)
    {
        gameManager = owner;
    }

    public bool HasSaveGame()
    {
        return File.Exists(GetSaveFilePath());
    }

    public void SaveGame()
    {
        if (gameManager == null)
        {
            return;
        }

        try
        {
            GameSaveData saveData = gameManager.CaptureSaveData();
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(GetSaveFilePath(), json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to save game data: " + exception.Message);
        }
    }

    public void LoadGame()
    {
        if (gameManager == null)
        {
            return;
        }

        string saveFilePath = GetSaveFilePath();
        if (!File.Exists(saveFilePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(saveFilePath);
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
            if (saveData == null)
            {
                return;
            }

            gameManager.ApplySaveData(saveData);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to load game data: " + exception.Message);
        }
    }

    public void StartNewGame()
    {
        if (gameManager == null)
        {
            return;
        }

        gameManager.ResetToNewGameState();

        try
        {
            string saveFilePath = GetSaveFilePath();
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to clear save file: " + exception.Message);
        }

        SaveGame();
    }

    public void SaveOnApplicationQuit()
    {
        if (gameManager != null && gameManager.CanPersistState)
        {
            SaveGame();
        }
    }

    private string GetSaveFilePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }
}
