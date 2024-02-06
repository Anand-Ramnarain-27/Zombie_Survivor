using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public SaveSystem saveSystem;
    public MetaProgressionManager metaProgressionManager;

    private string filename = "SaveData";
    private List<WeaponType> weapons;

    private void Awake()
    {
        // Ensure there is only one instance of GameManager
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // If another instance is trying to be created, destroy it
            Destroy(gameObject);
            return;
        }
        InitializeSaveData();
    }

    private void InitializeSaveData()
    {
        SaveData saveData = GetSaveDataFromJson();

        if (saveData == null)
        {
            // If no existing save data is found, create a new instance
            saveData = new SaveData(weapons, GameManager.instance.metaProgressionManager.metaProgressionContainers);
            JSONFileHandler.SaveToJSON<SaveData>(saveData, filename);
        }
        else
        {
            // Existing save data found, you can perform any additional initialization if needed
        }

        // Do any other initialization based on the save data if required
    }

    private SaveData GetSaveDataFromJson()
    {
        SaveData saveData = JSONFileHandler.ReadListFromJSON<SaveData>(filename);
        return saveData;
    }


}
