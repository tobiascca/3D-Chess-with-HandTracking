using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    public Transform heldObject;
    private bool wasPinching = false;

    void Update()
    {
        Vector2 hand = HandReceiver.thumbPos;

        Vector3 screenPos = new Vector3(
            hand.x * Screen.width,
            (1 - hand.y) * Screen.height,
            0
        );

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.red);

        // Grab on pinch start
        if (HandReceiver.pinch && !wasPinching)
        {
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Grabbable"))
                {
                    heldObject = hit.collider.transform;
                }
            }
        }

        // Move held object to follow hand cursor
        if (HandReceiver.pinch && heldObject != null)  // ← was == null (the bug)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                heldObject.position = hit.point;  // ← actually moves the piece
            }
        }

        // Release
        if (!HandReceiver.pinch)
        {
            heldObject = null;
        }

        wasPinching = HandReceiver.pinch;
    }
}