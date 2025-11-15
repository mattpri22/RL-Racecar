using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Assign the player's transform
    public float yOffset = 5f; // Offset above the player
    public float smoothSpeed = 5f; // Smoothing factor

    private void Start()
    {
        // Store initial X and Z positions
        if (player != null)
        {
            // Only modify Y, keep initial X and Z
            transform.position = new Vector3(
                transform.position.x,
                player.position.y + yOffset,
                transform.position.z
            );
        }
    }

    void LateUpdate()
    {
        if (player == null) return; // Prevent errors if no player is assigned

        // Calculate target position with only Y-axis movement
        Vector3 targetPosition = new Vector3(
            transform.position.x, // Keep current X
            player.position.y + yOffset, // Follow player on Y with offset
            transform.position.z  // Keep current Z
        );

        // Smoothly move camera to target position
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }
}