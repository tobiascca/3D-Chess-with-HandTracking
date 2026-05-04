using UnityEngine;

public class HandCursor : MonoBehaviour
{
    private Vector3 lastValidPos;
    private bool hasValidPos = false;
    public float smoothSpeed = 20f;
    public float hoverHeight = 0.3f;

    void Start()
    {
        lastValidPos = transform.position;
    }

    void Update()
    {
        Vector2 hand = HandReceiver.thumbPos;
        if (hand == Vector2.zero) return; // ignore uninitialized data

        Vector3 screenPos = new Vector3(
            hand.x * Screen.width,
            (1 - hand.y) * Screen.height,
            0
        );

        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            lastValidPos = hit.point + Vector3.up * hoverHeight;
            hasValidPos = true;
        }

        // Only move if we have ever had a valid hit, never snap to ray origin
        if (hasValidPos)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                lastValidPos,
                Time.deltaTime * smoothSpeed
            );
        }
    }
}