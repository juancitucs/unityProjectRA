using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds customizable animations to UI elements for hover and click events.
/// Uses LeanTween for animations.
/// </summary>
public class UIElementAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Animation Settings")]
    [Tooltip("The duration of the animations.")]
    [SerializeField] private float animationDuration = 0.15f;

    [Header("Hover Animation")]
    [Tooltip("Enable animation when the pointer hovers over the element.")]
    [SerializeField] private bool animateOnHover = true;
    [Tooltip("The scale multiplier on hover.")]
    [SerializeField] private float hoverScaleMultiplier = 1.05f;
    [Tooltip("Enable color change on hover.")]
    [SerializeField] private bool changeColorOnHover = false;
    [Tooltip("The color to apply on hover.")]
    [SerializeField] private Color hoverColor = Color.white;

    [Header("Click Animation")]
    [Tooltip("Enable animation on click.")]
    [SerializeField] private bool animateOnClick = true;
    [Tooltip("The scale multiplier on click (less than 1 for a 'press' effect).")]
    [SerializeField] private float clickScaleMultiplier = 0.95f;
    [Tooltip("Enable color change on click.")]
    [SerializeField] private bool changeColorOnClick = false;
    [Tooltip("The color to apply on click.")]
    [SerializeField] private Color clickColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private Vector3 originalScale;
    private Color originalColor;
    private Graphic targetGraphic;

    void Awake()
    {
        originalScale = transform.localScale;
        targetGraphic = GetComponent<Graphic>();
        if (targetGraphic != null)
        {
            originalColor = targetGraphic.color;
        }

        // Initialize LeanTween if it hasn't been already.
        LeanTween.init();
    }

    /// <summary>
    /// Called when the pointer enters the element.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (animateOnHover)
        {
            AnimateToHover();
        }
    }

    /// <summary>
    /// Called when the pointer exits the element.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateToNormal();
    }

    /// <summary>
    /// Called when the pointer is pressed down on the element.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (animateOnClick)
        {
            AnimateToPressed();
        }
    }

    /// <summary>
    /// Called when the pointer is released over the element.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        // After a click, if the pointer is still over the element, return to hover state.
        if (animateOnHover)
        {
            AnimateToHover();
        }
        else
        {
            AnimateToNormal();
        }
    }

    private void AnimateToHover()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale * hoverScaleMultiplier, animationDuration).setEase(LeanTweenType.easeOutQuad);
        if (changeColorOnHover && targetGraphic != null)
        {
            LeanTween.color(targetGraphic.rectTransform, hoverColor, animationDuration).setEase(LeanTweenType.easeOutQuad);
        }
    }

    private void AnimateToPressed()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale * clickScaleMultiplier, animationDuration).setEase(LeanTweenType.easeOutQuad);
        if (changeColorOnClick && targetGraphic != null)
        {
            LeanTween.color(targetGraphic.rectTransform, clickColor, animationDuration).setEase(LeanTweenType.easeOutQuad);
        }
    }

    private void AnimateToNormal()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, originalScale, animationDuration).setEase(LeanTweenType.easeOutQuad);
        if (targetGraphic != null && (changeColorOnHover || changeColorOnClick))
        {
            LeanTween.color(targetGraphic.rectTransform, originalColor, animationDuration).setEase(LeanTweenType.easeOutQuad);
        }
    }

    /// <summary>
    /// On disable, reset the state to avoid getting stuck in an animated state.
    /// </summary>
    void OnDisable()
    {
        LeanTween.cancel(gameObject);
        transform.localScale = originalScale;
        if (targetGraphic != null)
        {
            targetGraphic.color = originalColor;
        }
    }
}
