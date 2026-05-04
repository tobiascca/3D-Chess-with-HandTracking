using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class ChessNetworkManager : NetworkManager
{
    [Header("UI")]
    public GameObject mainMenuPanel;
    public GameObject chessBoardVisuals; 

    public override void Start()
    {
        base.Start();
        if (chessBoardVisuals != null)
                chessBoardVisuals.SetActive(false);
    }

    public override void Awake()
    {
        base.Awake();
        maxConnections = 2;
    }

    private readonly List<NetworkConnectionToClient> players = new();

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (players.Count >= 2)
        {
            conn.Disconnect();
            return;
        }

        bool isWhite = (players.Count == 0);
        players.Add(conn);

        GameObject playerObj = Instantiate(playerPrefab);
        ChessPlayer cp = playerObj.GetComponent<ChessPlayer>();
        if (cp != null)
            cp.isWhite = isWhite;

        NetworkServer.AddPlayerForConnection(conn, playerObj);

        Debug.Log($"[Server] Jugador {players.Count} conectado — {(isWhite ? "Blancas" : "Negras")}");

        if (players.Count == 2)
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(false);
            if (chessBoardVisuals != null)
                chessBoardVisuals.SetActive(true);

            BoardManager.Instance?.ServerSpawnAllChessmans();
            BoardManager.Instance?.RpcStartGame();
            BoardManager.Instance?.ServerSyncBoardToClient(conn);
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        players.Remove(conn);
        base.OnServerDisconnect(conn);
        Debug.Log("[Server] Jugador desconectado.");
    }

    public override void OnStopServer()
    {
        players.Clear();
        base.OnStopServer();
    }
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        if (NetworkServer.active) return;

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (chessBoardVisuals != null)
            chessBoardVisuals.SetActive(true);
    }
}