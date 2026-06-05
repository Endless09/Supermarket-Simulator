using UnityEngine;

/// <summary>
/// Keeps a world-space label facing the main camera so shelf stock text is easy to read.
/// </summary>
public class BillboardToCamera : MonoBehaviour
{
    private Camera mainCamera;

    private void LateUpdate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        transform.forward = mainCamera.transform.forward;
    }
}
