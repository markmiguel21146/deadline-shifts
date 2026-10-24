using UnityEngine;
using UnityEngine.EventSystems;

public class HandsTypingController : MonoBehaviour
{
    private Animator animator;

    [Header("Settings")]
    public float typingTimeout = 0.2f;

    private float lastTypeTime = -10f;
    private bool wasTyping = false;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.anyKeyDown)
        {
            lastTypeTime = Time.time;
        }

        bool isTyping = (Time.time - lastTypeTime) < typingTimeout;
        if (isTyping != wasTyping)
        {
            wasTyping = isTyping;
            if (animator != null)
            {
                animator.SetBool("isTyping", isTyping);
            }
        }
    }
}