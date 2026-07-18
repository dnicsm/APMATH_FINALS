using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Tracking Setup")]
    public Player target; 
    public Vector3 offset = new Vector3(0f, 2f, -10f);
    
    [Header("Movement Smoothing")]
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.meshPosition + offset;

        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.unscaledDeltaTime);
    }
}