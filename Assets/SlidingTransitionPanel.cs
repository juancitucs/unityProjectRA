using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System; // Required for Action

// INSTRUCTIONS:
// 1. Create a Canvas and a UI > Panel inside it.
// 2. Add this script directly to that Panel.
// 3. To trigger a full scene transition, call the public "LoadScene(string sceneName)" method.
// 4. For standalone slide IN/OUT animations, call "SlideIn()" or "SlideOut()".

public enum TransitionState
{
    Idle,     // The panel is idle and off-screen (or fully covering the screen after a SlideIn)
    Entering, // The panel is sliding IN to cover the screen
    Exiting   // The panel is sliding OUT to reveal the view
}

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class SlidingTransitionPanel : MonoBehaviour
{
    public static SlidingTransitionPanel Instance { get; private set; }

    [Header("Animation Settings")]
    [Tooltip("The duration of the transition animation in seconds.")]
    [SerializeField] private float transitionDuration = 0.5f;

    [Tooltip("The easing function to use for the animation.")]
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeInOutExpo;

    /// <summary>
    /// The current state of the transition animation. Can be checked from other scripts.
    /// </summary>
    public TransitionState CurrentState { get; private set; } = TransitionState.Idle;

    private RectTransform rectTransform;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                DontDestroyOnLoad(parentCanvas.gameObject);
            }
            else
            {
                Debug.LogError("SlidingTransitionPanel: Must be placed on a UI element inside a Canvas.", this);
                return;
            }
        }
        else
        {
            Destroy(GetComponentInParent<Canvas>().gameObject);
            return;
        }

        rectTransform = GetComponent<RectTransform>();

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        float width = rectTransform.rect.width;
        rectTransform.offsetMin = new Vector2(width, 0);
        rectTransform.offsetMax = new Vector2(width, 0);

        gameObject.SetActive(true);
        CurrentState = TransitionState.Idle;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Public method to start a full scene transition. Call this from your UI buttons.
    /// The panel will slide IN, load the scene, then slide OUT.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    public void LoadScene(string sceneName)
    {
        if (CurrentState != TransitionState.Idle)
        {
            Debug.LogWarning("Transition already in progress, ignoring new scene load request.");
            return;
        }

        CurrentState = TransitionState.Entering;
        _SlideInTween(() => {
            SceneManager.LoadScene(sceneName);
        });
    }

    /// <summary>
    /// Triggers the panel to slide IN and cover the screen.
    /// The panel will remain covering the screen (Idle state) after animation.
    /// </summary>
    public void SlideIn()
    {
        if (CurrentState != TransitionState.Idle)
        {
            Debug.LogWarning("Transition already in progress, ignoring SlideIn request.");
            return;
        }
        CurrentState = TransitionState.Entering;
        _SlideInTween(() => {
            CurrentState = TransitionState.Idle; // Panel is now covering screen, idle state.
        });
    }

    /// <summary>
    /// Triggers the panel to slide OUT and reveal the view.
    /// The panel will return to its off-screen right position (Idle state) after animation.
    /// </summary>
    public void SlideOut()
    {
        // Allow SlideOut if Idle (from a previous SlideIn) or if in Exiting state already.
        if (CurrentState == TransitionState.Entering)
        {
            Debug.LogWarning("SlideOut called while panel is still entering. Forcing immediate slide out.");
            LeanTween.cancel(gameObject); // Cancel incoming animation
        }
        else if (CurrentState == TransitionState.Exiting)
        {
            Debug.LogWarning("Transition already in progress (Exiting), ignoring SlideOut request.");
            return;
        }

        CurrentState = TransitionState.Exiting;
        _SlideOutTween(() => {
            CurrentState = TransitionState.Idle; // Panel is now off-screen, idle state.
        });
    }

    /// <summary>
    /// Called automatically by Unity when a new scene has finished loading.
    /// This triggers the slide OUT animation as part of a LoadScene call.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CurrentState = TransitionState.Exiting;
        _SlideOutTween(() => {
            CurrentState = TransitionState.Idle; // Full transition finished, back to idle
        });
    }

    /// <summary>
    /// Helper method for the slide IN animation (right to center).
    /// </summary>
    /// <param name="onCompleteCallback">Callback to invoke when animation finishes.</param>
    private void _SlideInTween(Action onCompleteCallback)
    {
        float width = rectTransform.rect.width;
        Vector2 onScreenOffset = Vector2.zero;
        Vector2 offScreenRightOffset = new Vector2(width, 0);

        LeanTween.value(gameObject, (Vector2 offset) => {
            rectTransform.offsetMin = offset;
            rectTransform.offsetMax = offset;
        }, offScreenRightOffset, onScreenOffset, transitionDuration)
        .setEase(easeType)
        .setOnComplete(() => {
            onCompleteCallback?.Invoke();
        });
    }

    /// <summary>
    /// Helper method for the slide OUT animation (center to left).
    /// </summary>
    /// <param name="onCompleteCallback">Callback to invoke when animation finishes.</param>
    private void _SlideOutTween(Action onCompleteCallback)
    {
        float width = rectTransform.rect.width;
        Vector2 onScreenOffset = Vector2.zero;
        Vector2 offScreenLeftOffset = new Vector2(-width, 0);
        Vector2 offScreenRightOffset = new Vector2(width, 0); // For resetting position

        LeanTween.value(gameObject, (Vector2 offset) => {
            rectTransform.offsetMin = offset;
            rectTransform.offsetMax = offset;
        }, onScreenOffset, offScreenLeftOffset, transitionDuration)
        .setEase(easeType)
        .setOnComplete(() => {
            // Reset the panel to the right for the next transition after sliding out.
            rectTransform.offsetMin = offScreenRightOffset;
            rectTransform.offsetMax = offScreenRightOffset;
            onCompleteCallback?.Invoke();
        });
    }
}
