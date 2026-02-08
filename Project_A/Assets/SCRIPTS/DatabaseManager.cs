using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;

public class DatabaseManager : MonoBehaviour
{
    // PASTE YOUR FIREBASE URL HERE (Keep the / at the end)
    private const string DATABASE_URL = "https://phd-policy-sim-default-rtdb.europe-west1.firebasedatabase.app/";

    public static DatabaseManager Instance;
    private string _playerID;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        _playerID = GetPlayerID();
    }

    private string GetPlayerID()
    {
        if (PlayerPrefs.HasKey("PlayerID")) return PlayerPrefs.GetString("PlayerID");
        string newID = System.Guid.NewGuid().ToString();
        PlayerPrefs.SetString("PlayerID", newID);
        PlayerPrefs.Save();
        return newID;
    }

    // --- DATA STRUCTURES (Corrected for Game Loop) ---
    [System.Serializable]
    public class GameSessionData
    {
        public string playerID;
        public string timestamp;
        
        // Metrics
        public float startSocietalFairness;
        public float startAverageLS;
        public float endSocietalFairness;
        public float endAverageLS;
        
        // Game State
        public int totalTurnsPlayed;
        public string gameOverReason;

        // The History of Moves
        public List<TurnLog> turnHistory = new List<TurnLog>();
    }

    [System.Serializable]
    public struct TurnLog
    {
        public int turnNumber;
        public string chosenPolicy;
        public int costPaid;
        public string[] availableOptions;
        public float resultingFairness;
    }

    // --- UPLOAD LOGIC ---
    public void UploadGameResult(GameSessionData data)
    {
        StartCoroutine(PutRequest(data));
    }

    private IEnumerator PutRequest(GameSessionData data)
    {
        string json = JsonUtility.ToJson(data);
        string url = $"{DATABASE_URL}users/{_playerID}.json";

        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Database Error: {request.error}");
            }
            else
            {
                Debug.Log("✅ Data saved successfully!");
            }
        }
    }
}