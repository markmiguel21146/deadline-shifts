using UnityEngine;

public class FollowCursor : MonoBehaviour
{
    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (PauseMenuManager.IsPaused) return;

        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }

        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f; // Keep light on the 2D plane
        transform.position = mousePos;
    }
}