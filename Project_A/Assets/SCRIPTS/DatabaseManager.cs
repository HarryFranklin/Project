using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

public class DatabaseManager : MonoBehaviour
{
    private const string DATABASE_URL = "https://phd-policy-sim-default-rtdb.europe-west1.firebasedatabase.app/";

    // Singleton instance
    public static DatabaseManager Instance;

    private string _playerID;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Load or Generate ID on startup
        _playerID = GetPlayerID();
        Debug.Log($"Database: User Session Started. ID: {_playerID}");
    }

    // --- 1. ID LOGIC ---
    private string GetPlayerID()
    {
        if (PlayerPrefs.HasKey("PlayerID"))
        {
            return PlayerPrefs.GetString("PlayerID");
        }
        else
        {
            string newID = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerID", newID);
            PlayerPrefs.Save();
            return newID;
        }
    }

    // --- 2. DATA STRUCTURES ---
    // A wrapper to make JSON serialization easy for Unity
    [System.Serializable]
    public class GameSessionData
    {
        public string timestamp;
        // We use a List of strings to track the sequence "RED", "BLUE", "RED"
        public System.Collections.Generic.List<string> buttonLog; 
    }

    // --- 3. UPLOAD LOGIC ---
    public void UploadGameResult(GameSessionData data)
    {
        StartCoroutine(PutRequest(data));
    }

    private IEnumerator PutRequest(GameSessionData data)
    {
        // Convert data to JSON
        string json = JsonUtility.ToJson(data);

        // Construct the specific URL for THIS player
        // Format: URL/users/{ID}.json
        string url = $"{DATABASE_URL}users/{_playerID}.json";

        // Create the PUT Request (PUT overwrites/creates data at this path)
        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"Uploading to: {url}");

            // Send
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Database Error: {request.error}\nResponse: {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log("✅ Data saved successfully!");
            }
        }
    }
}