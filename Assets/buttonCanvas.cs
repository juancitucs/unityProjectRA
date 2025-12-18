using UnityEngine;
using UnityEngine.EventSystems; // Necesario para los eventos de puntero

public class buttonCanvas : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 originalScale;
    private bool isTweening = false;

    void Awake()
    {
        // Guardamos la escala original al iniciar
        originalScale = transform.localScale;
        // Inicializa LeanTween para asegurar que esté listo.
        LeanTween.init();
    }

    // Este método se llama cuando el puntero presiona el objeto
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("Pointer Down: Escalando hacia arriba.");
        // Cancela animaciones anteriores en este objeto para evitar conflictos
        LeanTween.cancel(gameObject);
        // Escala el botón a un tamaño mayor
        LeanTween.scale(gameObject, originalScale * 1.2f, 0.15f).setEase(LeanTweenType.easeOutQuad);
    }

    // Este método se llama cuando el puntero se suelta sobre el objeto
    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("Pointer Up: Volviendo a la escala original.");
        // Cancela animaciones anteriores en este objeto
        LeanTween.cancel(gameObject);
        // Devuelve el botón a su escala original
        LeanTween.scale(gameObject, originalScale, 0.15f).setEase(LeanTweenType.easeOutQuad);
    }
}
