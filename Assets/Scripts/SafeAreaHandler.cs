using UnityEngine;

public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Rect _lastSafeArea = new Rect(0, 0, 0, 0);

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        Refresh();
    }

    void Update()
    {
        // Refresh if the screen orientation changes or the safe area shifts
        if (_lastSafeArea != Screen.safeArea)
        {
            Refresh();
        }
    }

    void Refresh()
    {
        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;

        // Convert safe area rectangle from pixels to normalized anchor coordinates (0 to 1)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
    }
}
