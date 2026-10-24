using UnityEngine;
using UnityEngine.UI;

public class CustomCursor : MonoBehaviour
{
    public static CustomCursor Instance { get; private set; }

    private RectTransform rectTransform;
    private Image cursorImage;
    private Sprite defaultSprite;
    private Vector3 defaultScale;

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        cursorImage = GetComponent<Image>();

        if (cursorImage != null)
        {
            defaultSprite = cursorImage.sprite;
        }

        defaultScale = transform.localScale;
    }

    void Start()
    {
        // Hide the normal Windows/Unity cursor
        Cursor.visible = false;

        // Make sure our cursor starts somewhere visible
        if (rectTransform != null)
        {
            rectTransform.position = Input.mousePosition;
        }
    }

    void Update()
    {
        if (PauseMenuManager.IsPaused)
        {
            if (cursorImage != null && cursorImage.enabled)
            {
                cursorImage.enabled = false;
            }
            return;
        }

        if (cursorImage != null && !cursorImage.enabled)
        {
            cursorImage.enabled = true;
        }

        if (rectTransform != null)
        {
            rectTransform.position = Input.mousePosition;
        }
    }

    public void SetCursorVisible(bool visible)
    {
        if (cursorImage != null)
        {
            cursorImage.enabled = visible;
        }
    }

    public void SetHover(bool isHovering, Sprite hoverSprite = null)
    {
        if (cursorImage != null && defaultSprite != null)
        {
            cursorImage.sprite = (isHovering && hoverSprite != null) ? hoverSprite : defaultSprite;
        }

        transform.localScale = isHovering ? defaultScale * 1.2f : defaultScale;
    }

    void OnDestroy()
    {
        Cursor.visible = true;
    }
}