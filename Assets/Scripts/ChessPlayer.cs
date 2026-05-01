using UnityEngine;
using Mirror;
using System.Collections;

public class ChessPlayer : NetworkBehaviour
{
    [SyncVar]
    public bool isWhite = false;

    [Server]
    public void ServerSetColor(bool white)
    {
        isWhite = white;
    }

    public override void OnStartLocalPlayer()
    {
        // Esperar varios frames para que el SyncVar llegue del servidor
        StartCoroutine(AssignColorDelayed());
    }

    private IEnumerator AssignColorDelayed()
    {
        // Esperar hasta que BoardManager este listo y el SyncVar haya llegado
        yield return null;
        yield return null;
        yield return null;

        ApplyColorToBoardManager(isWhite);
    }

    private void ApplyColorToBoardManager(bool white)
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.isLocalPlayerWhite = white;
        else
            Debug.LogError("[ChessPlayer] BoardManager.Instance es null!");

        Debug.Log($"[Client] Soy {(white ? "Blancas" : "Negras")}");

        // Posicionar camara segun color
        if (Camera.main != null)
        {
            if (white)
            {
                Camera.main.transform.position = new Vector3(4f, 7f, -3f);
                Camera.main.transform.rotation = Quaternion.Euler(53f, 0f, 0f);
            }
            else
            {
                Camera.main.transform.position = new Vector3(4f, 7f, 11f);
                Camera.main.transform.rotation = Quaternion.Euler(53f, 180f, 0f);
            }
        }
    }
}