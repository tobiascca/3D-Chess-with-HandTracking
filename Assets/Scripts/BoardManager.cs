using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class BoardManager : NetworkBehaviour
{
    public static BoardManager Instance { get; private set; }

    private const float TILE_SIZE   = 1.0f;
    private const float TILE_OFFSET = 0.5f;

    [SyncVar] public bool isWhiteTurn = true;
    [SyncVar] private int enPassantX = -1;
    [SyncVar] private int enPassantY = -1;

    public int[] EnPassantMove
    {
        get => new int[] { enPassantX, enPassantY };
        set { enPassantX = value[0]; enPassantY = value[1]; }
    }

    [HideInInspector] public bool isLocalPlayerWhite;

    // El array de piezas existe en AMBOS lados ahora
    public Chessman[,] Chessmans { get; private set; }
    private List<GameObject> activeChessman = new List<GameObject>();

    private int selectionX = -1;
    private int selectionY = -1;
    private Chessman selectedChessman;
    private bool[,] allowedMoves;
    private Material previousMat;

    public List<GameObject> chessmanPrefabs;
    public Material selectedMat;

    private Quaternion whiteOrientation = Quaternion.Euler(0, 270, 0);
    private Quaternion blackOrientation = Quaternion.Euler(0, 90,  0);

    // ─────────────────────────────────────────────────────────────────────────
    // INIT
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        Chessmans = new Chessman[8, 8];
    }

    public override void OnStartServer()
    {
        enPassantX = -1;
        enPassantY = -1;
        //ServerSpawnAllChessmans();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.Escape)) Application.Quit();
        if (!isClient) return;

        bool myTurn = (isWhiteTurn == isLocalPlayerWhite);
        if (!myTurn) return;

        UpdateSelection();

        if (Input.GetMouseButtonDown(0) && selectionX >= 0 && selectionY >= 0)
        {
            if (selectedChessman == null)
                LocalSelectChessman(selectionX, selectionY);
            else
                LocalTryMove(selectionX, selectionY);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SELECCION LOCAL
    // ─────────────────────────────────────────────────────────────────────────

    private void LocalSelectChessman(int x, int y)
    {
        Chessman piece = GetChessmanAt(x, y);
        if (piece == null) return;
        if (piece.isWhite != isLocalPlayerWhite) return;

        bool[,] moves = piece.PossibleMoves();
        bool hasMove = false;
        for (int i = 0; i < 8 && !hasMove; i++)
            for (int j = 0; j < 8 && !hasMove; j++)
                if (moves[i, j]) hasMove = true;

        if (!hasMove) return;

        DeselectCurrent();
        selectedChessman = piece;
        allowedMoves = moves;
        previousMat = selectedChessman.GetComponent<MeshRenderer>().material;
        selectedMat.mainTexture = previousMat.mainTexture;
        selectedChessman.GetComponent<MeshRenderer>().material = selectedMat;
        BoardHighlights.Instance.HighLightAllowedMoves(allowedMoves);
    }

    private void LocalTryMove(int x, int y)
    {
        if (allowedMoves != null && allowedMoves[x, y])
            CmdMovePiece(selectedChessman.CurrentX, selectedChessman.CurrentY, x, y);
        DeselectCurrent();
    }

    private void DeselectCurrent()
    {
        if (selectedChessman != null)
        {
            selectedChessman.GetComponent<MeshRenderer>().material = previousMat;
            selectedChessman = null;
        }
        BoardHighlights.Instance.HideHighlights();
        allowedMoves = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // COMMAND
    // ─────────────────────────────────────────────────────────────────────────

    [Command(requiresAuthority = false)]
    public void CmdMovePiece(int fromX, int fromY, int toX, int toY)
    {
        if (Chessmans == null) return;
        Chessman piece = Chessmans[fromX, fromY];
        if (piece == null) return;
        if (piece.isWhite != isWhiteTurn) return;

        bool[,] moves = piece.PossibleMoves();
        if (!moves[toX, toY]) return;

        ServerExecuteMove(fromX, fromY, toX, toY);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LOGICA SERVIDOR
    // ─────────────────────────────────────────────────────────────────────────

    [Server]
    private void ServerExecuteMove(int fromX, int fromY, int toX, int toY)
    {
        Chessman moving = Chessmans[fromX, fromY];
        Chessman target = Chessmans[toX, toY];

        if (target != null && target.isWhite != isWhiteTurn)
        {
            if (target is King) { ServerEndGame(); return; }
            ServerDestroyPiece(toX, toY);
        }

        if (toX == enPassantX && toY == enPassantY)
        {
            int capY = isWhiteTurn ? toY - 1 : toY + 1;
            if (Chessmans[toX, capY] != null)
                ServerDestroyPiece(toX, capY);
        }

        enPassantX = -1;
        enPassantY = -1;

        bool promoted = false;
        int promotionIndex = -1;

        if (moving is Pawn)
        {
            if (isWhiteTurn && toY == 7)  { promotionIndex = 1;  promoted = true; }
            if (!isWhiteTurn && toY == 0) { promotionIndex = 7;  promoted = true; }
            if (fromY == 1 && toY == 3)   { enPassantX = toX; enPassantY = toY - 1; }
            if (fromY == 6 && toY == 4)   { enPassantX = toX; enPassantY = toY + 1; }
        }

        // Mover en el array del servidor
        Chessmans[fromX, fromY] = null;
        Chessmans[toX, toY] = moving;
        moving.SetPosition(toX, toY);
        moving.transform.position = GetTileCenter(toX, toY);

        if (promoted)
        {
            ServerDestroyPiece(toX, toY);
            ServerSpawnChessman(promotionIndex, toX, toY, isWhiteTurn);
        }
        else
        {
            // Notificar a clientes que muevan la pieza
            RpcMovePiece(fromX, fromY, toX, toY);
        }

        isWhiteTurn = !isWhiteTurn;
    }

    [Server]
    private void ServerDestroyPiece(int x, int y)
    {
        Chessman piece = Chessmans[x, y];
        if (piece == null) return;
        Chessmans[x, y] = null;
        activeChessman.Remove(piece.gameObject);
        RpcDestroyPieceAt(x, y);  // mandamos coordenadas ANTES de mover nada
        Destroy(piece.gameObject);
    }

    [ClientRpc]
    private void RpcDestroyPieceAt(int x, int y)
    {
        if (isServer) return; // el host ya lo destruyó

        Chessman piece = Chessmans[x, y];
        if (piece != null)
        {
            Chessmans[x, y] = null;
            Destroy(piece.gameObject);
        }
    }

    [Server]
    private void ServerSpawnChessman(int index, int x, int y, bool white)
    {
        Vector3 pos    = GetTileCenter(x, y);
        Quaternion rot = white ? whiteOrientation : blackOrientation;
        GameObject go  = Instantiate(chessmanPrefabs[index], pos, rot);
        go.transform.SetParent(transform);
        Chessman cm = go.GetComponent<Chessman>();
        cm.SetPosition(x, y);
        Chessmans[x, y] = cm;
        activeChessman.Add(go);
        // Notificar al cliente para que cree la pieza tambien
        RpcSpawnChessman(index, x, y, white);
    }

    [Server]
    public void ServerSpawnAllChessmans()
    {
        foreach (var go in activeChessman) if (go) Destroy(go);
        activeChessman.Clear();
        Chessmans = new Chessman[8, 8];

        ServerSpawnChessman(0,  3, 0, true);
        ServerSpawnChessman(1,  4, 0, true);
        ServerSpawnChessman(2,  0, 0, true);
        ServerSpawnChessman(2,  7, 0, true);
        ServerSpawnChessman(3,  2, 0, true);
        ServerSpawnChessman(3,  5, 0, true);
        ServerSpawnChessman(4,  1, 0, true);
        ServerSpawnChessman(4,  6, 0, true);
        for (int i = 0; i < 8; i++) ServerSpawnChessman(5, i, 1, true);

        ServerSpawnChessman(6,  4, 7, false);
        ServerSpawnChessman(7,  3, 7, false);
        ServerSpawnChessman(8,  0, 7, false);
        ServerSpawnChessman(8,  7, 7, false);
        ServerSpawnChessman(9,  2, 7, false);
        ServerSpawnChessman(9,  5, 7, false);
        ServerSpawnChessman(10, 1, 7, false);
        ServerSpawnChessman(10, 6, 7, false);
        for (int i = 0; i < 8; i++) ServerSpawnChessman(11, i, 6, false);
    }

    [Server]
    private void ServerEndGame()
    {
        RpcEndGame(isWhiteTurn);
        Invoke(nameof(ServerRestart), 3f);
    }

    [Server]
    private void ServerRestart()
    {
        isWhiteTurn = true;
        enPassantX = enPassantY = -1;
        // Limpiar clientes primero, luego spawnear piezas nuevas
        RpcClearAllPieces();
        ServerSpawnAllChessmans();
    }

    [ClientRpc]
    private void RpcClearAllPieces()
    {
        if (isServer) return; // el servidor limpia en ServerSpawnAllChessmans
        Chessmans = new Chessman[8, 8];
        foreach (Chessman c in FindObjectsByType<Chessman>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Destroy(c.gameObject);
    }

    // Sincronizar estado actual al cliente que acaba de conectarse
    [Server]
    public void ServerSyncBoardToClient(NetworkConnectionToClient conn)
    {
        // Primero limpiar el cliente
        TargetRpcClearBoard(conn);
        // Luego enviar cada pieza viva
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Chessman p = Chessmans[x, y];
                if (p == null) continue;
                int idx = GetPrefabIndex(p);
                if (idx >= 0) TargetRpcSpawnPiece(conn, idx, x, y, p.isWhite);
            }
    }

    private int GetPrefabIndex(Chessman p)
    {
        if (p is King)   return p.isWhite ? 0 : 6;
        if (p is Queen)  return p.isWhite ? 1 : 7;
        if (p is Rook)   return p.isWhite ? 2 : 8;
        if (p is Bishop) return p.isWhite ? 3 : 9;
        if (p is Knight) return p.isWhite ? 4 : 10;
        if (p is Pawn)   return p.isWhite ? 5 : 11;
        return -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLIENT RPCs
    // ─────────────────────────────────────────────────────────────────────────

    // Mover pieza en el cliente (y actualizar su array local)
    [ClientRpc]
    private void RpcMovePiece(int fromX, int fromY, int toX, int toY)
    {
        Chessman piece = GetChessmanAt(fromX, fromY);
        if (piece == null) return;

        // Actualizar array local del cliente
        if (Chessmans != null)
        {
            Chessmans[fromX, fromY] = null;
            Chessmans[toX, toY] = piece;
        }

        piece.transform.position = GetTileCenter(toX, toY);
        piece.SetPosition(toX, toY);

        if (selectedChessman == piece) DeselectCurrent();
    }

   

    // Spawn de pieza en cliente (promocion o sync inicial)
    [ClientRpc]
    private void RpcSpawnChessman(int index, int x, int y, bool white)
    {
        if (isServer) return; // el host ya lo hizo en el servidor
        ClientSpawnPiece(index, x, y, white);
    }

    [TargetRpc]
    private void TargetRpcClearBoard(NetworkConnectionToClient conn)
    {
        // Limpiar array local
        Chessmans = new Chessman[8, 8];
        // Destruir GameObjects de piezas en escena
        foreach (Chessman c in FindObjectsByType<Chessman>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            Destroy(c.gameObject);
    }

    [TargetRpc]
    private void TargetRpcSpawnPiece(NetworkConnectionToClient conn, int index, int x, int y, bool white)
    {
        ClientSpawnPiece(index, x, y, white);
    }

    private void ClientSpawnPiece(int index, int x, int y, bool white)
    {
        Vector3 pos    = GetTileCenter(x, y);
        Quaternion rot = white ? whiteOrientation : blackOrientation;
        GameObject go  = Instantiate(chessmanPrefabs[index], pos, rot);
        go.transform.SetParent(transform);
        Chessman cm = go.GetComponent<Chessman>();
        cm.SetPosition(x, y);
        if (Chessmans != null) Chessmans[x, y] = cm;
    }

    [ClientRpc]
    public void RpcStartGame()
    {
        Debug.Log("[Client] ¡Partida iniciada! Turno: Blancas");
        // La sincronizacion del tablero se hace via ServerSyncBoardToClient
        // No hacemos nada aqui para no pisar las piezas
    }

    [ClientRpc]
    private void RpcEndGame(bool whiteWon)
    {
        Debug.Log($"[Client] {(whiteWon ? "Blancas" : "Negras")} ganan!");
        DeselectCurrent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 GetTileCenter(int x, int y) =>
        new Vector3(TILE_SIZE * x + TILE_OFFSET, 0f, TILE_SIZE * y + TILE_OFFSET);

    private void UpdateSelection()
    {
        if (!Camera.main) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (ray.direction.y >= 0) return;

        float t = -ray.origin.y / ray.direction.y;
        Vector3 point = ray.origin + t * ray.direction;
        selectionX = (int)point.x;
        selectionY = (int)point.z;

        if (selectionX < 0 || selectionX >= 8 || selectionY < 0 || selectionY >= 8)
            selectionX = selectionY = -1;
    }

    // Funciona en cliente Y servidor usando el array local
    public Chessman GetChessmanAt(int x, int y)
    {
        if (Chessmans != null) return Chessmans[x, y];
        return null;
    }
}