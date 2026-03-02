using UnityEngine;

public class PlayerIconRotator : MonoBehaviour
{
    public Transform playerTransform;
    public RectTransform iconRectTransform;

    void Start()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void LateUpdate()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (playerTransform == null || iconRectTransform == null) return;

        float yaw = playerTransform.eulerAngles.y;
        iconRectTransform.localRotation = Quaternion.Euler(0, 0, -yaw);
    }
}
