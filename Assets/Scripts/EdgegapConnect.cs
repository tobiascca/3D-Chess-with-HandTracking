using Edgegap;
using UnityEngine;
using UnityEngine.Networking;
using Mirror;
using System.Collections;
using System.Text;

public class EdgegapConnect : MonoBehaviour
{
    [Header("Edgegap Configuration")]
    public string relayProfileToken = "YOUR_TOKEN_HERE";
    public string relayProfileId = "Chess";

    [Header("Network References")]
    public EdgegapKcpTransport edgegapTransport;

    [Header("User Interface")]
    public TMPro.TMP_InputField sessionInputField;
    public GameObject mainMenuPanel;

    // ─────────────────────────────────────────────────────────────────────────
    // HOST
    // ─────────────────────────────────────────────────────────────────────────

    public void StartHost()
    {
        StartCoroutine(CreateRelaySession());
    }

    IEnumerator CreateRelaySession()
    {
        Debug.Log("[Edgegap] Fetching public IP...");

        // 1. Get real public IP so the relay can route correctly
        string myIp = "0.0.0.0";
        using (var ipReq = UnityWebRequest.Get("https://api.ipify.org"))
        {
            yield return ipReq.SendWebRequest();
            if (ipReq.result == UnityWebRequest.Result.Success)
                myIp = ipReq.downloadHandler.text.Trim();
            else
                Debug.LogWarning("[Edgegap] Could not fetch public IP, using 0.0.0.0");
        }

        Debug.Log($"[Edgegap] Public IP: {myIp}");
        Debug.Log("[Edgegap] Requesting new relay session...");

        string url        = "https://api.edgegap.com/v1/relays/sessions";
        string cleanToken = CleanToken(relayProfileToken);

        // 2. Create relay session
        string body = "{\"relay_profile_id\": \"" + relayProfileId +
                      "\", \"users\": [{\"ip\": \"" + myIp +
                      "\"}, {\"ip\": \"" + myIp + "\"}]}";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Token " + cleanToken);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Edgegap] POST failed: {req.downloadHandler.text}");
                yield break;
            }

            Debug.Log($"[Edgegap] Session response: {req.downloadHandler.text}");

            RelaySessionResponse data = JsonUtility.FromJson<RelaySessionResponse>(req.downloadHandler.text);
            string sessionId = data.session_id;
            sessionInputField.text = sessionId;

            // 3. Poll until session is Ready
            while (!data.ready)
            {
                Debug.Log("[Edgegap] Session not ready yet, retrying in 3s...");
                yield return new WaitForSeconds(3);

                using (var poll = UnityWebRequest.Get(url + "/" + sessionId))
                {
                    poll.SetRequestHeader("Authorization", "Token " + cleanToken);
                    yield return poll.SendWebRequest();

                    if (poll.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[Edgegap] Poll failed: {poll.error}");
                        yield break;
                    }

                    Debug.Log($"[Edgegap] Poll response: {poll.downloadHandler.text}");
                    data = JsonUtility.FromJson<RelaySessionResponse>(poll.downloadHandler.text);
                }
            }

            GUIUtility.systemCopyBuffer = sessionId;
            Debug.Log($"[Edgegap] Session ready! ID copied to clipboard: {sessionId}");

            InitializeAndConnect(data, userIndex: 0);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLIENT
    // ─────────────────────────────────────────────────────────────────────────

    public void JoinGame()
    {
        string code = sessionInputField.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[Edgegap] Session ID is empty!");
            return;
        }
        StartCoroutine(JoinRelaySession(code));
    }

    IEnumerator JoinRelaySession(string code)
    {
        Debug.Log("[Edgegap] Attempting to join session: " + code);

        string url        = "https://api.edgegap.com/v1/relays/sessions/" + code;
        string cleanToken = CleanToken(relayProfileToken);

        RelaySessionResponse data = null;

        // Poll until session is Ready
        while (data == null || !data.ready)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", "Token " + cleanToken);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[Edgegap] GET Error: " + req.error);
                    yield break;
                }

                Debug.Log($"[Edgegap] Join poll response: {req.downloadHandler.text}");
                data = JsonUtility.FromJson<RelaySessionResponse>(req.downloadHandler.text);
            }

            if (!data.ready)
            {
                Debug.Log("[Edgegap] Session not ready, retrying in 3s...");
                yield return new WaitForSeconds(3);
            }
        }

        InitializeAndConnect(data, userIndex: 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SHARED CONNECTION LOGIC
    // ─────────────────────────────────────────────────────────────────────────

    void InitializeAndConnect(RelaySessionResponse data, int userIndex)
    {
        // Relay network address and ports
        edgegapTransport.relayAddress        = data.relay.ip;
        edgegapTransport.relayGameServerPort = (ushort)data.relay.ports.server.port;
        edgegapTransport.relayGameClientPort = (ushort)data.relay.ports.client.port;

        // userId  = THIS player's unique authorization token (different per slot)
        // sessionId = the shared session-level authorization token (same for both)
        edgegapTransport.userId    = data.session_users[userIndex].authorization_token;
        edgegapTransport.sessionId = data.authorization_token;

        Debug.Log($"[Edgegap] Transport configured — relay: {data.relay.ip} | " +
                  $"userId: {edgegapTransport.userId} | sessionId: {edgegapTransport.sessionId}");
        if (userIndex == 0)
        {
            Debug.Log("[Edgegap] Starting Mirror Host...");
            NetworkManager.singleton.StartHost();
        }
        else
        {
            Debug.Log("[Edgegap] Starting Mirror Client...");
            NetworkManager.singleton.StartClient();
        }
        
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    // Strips any accidental "Token " prefix the user may have pasted in
    string CleanToken(string raw) => raw.Replace("Token", "").Trim();

    // ─────────────────────────────────────────────────────────────────────────
    // JSON MODELS  (must match Edgegap REST API response exactly)
    // ─────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class RelaySessionResponse
    {
        public string session_id;
        public bool   ready;
        public uint   authorization_token; // shared session-level token → sessionId
        public RelayInfo   relay;
        public SessionUser[] session_users;
    }

    [System.Serializable]
    public class RelayInfo
    {
        public string ip;
        public RelayPorts ports;
    }

    [System.Serializable]
    public class RelayPorts
    {
        public RelayPort server;
        public RelayPort client;
    }

    [System.Serializable]
    public class RelayPort
    {
        public int port;
    }

    [System.Serializable]
    public class SessionUser
    {
        public uint authorization_token; // per-user token → userId
    }
}