using UnityEngine;

public class KeyboardAdjuster : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Vector2 _initialPosition;
    private float _currentKeyboardHeight;
    public float Padding = 20f;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _initialPosition = _rectTransform.anchoredPosition;
    }

    private void Update()
    {
        float keyboardHeight = GetKeyboardHeight();
        float canvasScale = GetComponentInParent<Canvas>().scaleFactor;

        // 1. Calculate the TARGET position every frame
        Vector2 targetPos;
        if (keyboardHeight > 0)
        {
            float targetY = _initialPosition.y + (keyboardHeight / canvasScale) + Padding;
            targetPos = new Vector2(_initialPosition.x, targetY);
        }
        else
        {
            targetPos = _initialPosition;
        }

        // 2. Perform the SMOOTH MOVEMENT every frame
        // This will now continue to run until the UI reaches the targetPos
        _rectTransform.anchoredPosition = Vector2.Lerp(
            _rectTransform.anchoredPosition, 
            targetPos, 
            Time.deltaTime * 10f
        );

        // 3. Track height (Optional, used for debugging or other logic)
        _currentKeyboardHeight = keyboardHeight;
    }

    private float GetKeyboardHeight()
    {
#if UNITY_EDITOR
        return Input.GetKey(KeyCode.Space) ? 500 : 0;
#elif UNITY_ANDROID
    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
    {
        // 1. Get the current Android Activity
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        
        // 2. Get the Window and the DecorView (the root view of the window)
        AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
        AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView");
        
        // 3. Get the visible display frame
        AndroidJavaObject rect = new AndroidJavaObject("android.graphics.Rect");
        decorView.Call("getWindowVisibleDisplayFrame", rect);
        
        // 4. Height of the keyboard = Total Screen Height - Visible Frame Height
        int visibleHeight = rect.Call<int>("height");
        return Screen.height - visibleHeight;
    }
#elif UNITY_IOS
    return TouchScreenKeyboard.area.height;
#else
    return 0;
#endif
    }
}
