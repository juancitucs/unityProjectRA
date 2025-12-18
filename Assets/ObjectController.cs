using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Controla la escala y rotación de un objeto a través de elementos de la UI.
/// - Escala: Usa un Slider para ajustar la escala uniforme del objeto.
/// - Rotación: Permite rotar el objeto en el eje Y arrastrando el dedo/ratón horizontalmente.
/// </summary>
public class ObjectController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Object to Control")]
    [Tooltip("El Transform del objeto que se va a escalar y rotar (ej. el cubo sincronizado).")]
    public Transform objectToControl;

    [Header("UI References")]
    [Tooltip("El Slider que controla la escala del objeto.")]
    public Slider scaleSlider;
    
    [Header("Networking")]
    [Tooltip("El cliente UDP para notificar cuándo el control local está activo.")]
    public UDPCubeClient udpClient;

    [Header("Rotation Settings")]
    [Tooltip("La velocidad de rotación al arrastrar.")]
    public float rotationSpeed = 20f;

    [Header("Pinch Settings")]
    [Tooltip("La sensibilidad del gesto de pellizco para escalar.")]
    public float pinchSpeed = 0.05f;
    
    private bool isPointerDown = false;
    private Vector3 lastMousePosition;
    private float initialObjectScale;

    void Start()
    {
        if (objectToControl == null)
        {
            Debug.LogError("[ObjectController] 'Object to Control' no está asignado.");
            enabled = false;
            return;
        }

        if (scaleSlider == null)
        {
            Debug.LogError("[ObjectController] 'Scale Slider' no está asignado.");
            enabled = false;
            return;
        }
        
        // Guardar la escala inicial para calcular los cambios con el slider
        initialObjectScale = objectToControl.localScale.x;

        // Configurar el slider
        scaleSlider.minValue = 0.1f;
        scaleSlider.maxValue = 3f;
        
        // Asignar el valor DESPUÉS de añadir el listener para asegurar que el estado inicial es consistente
        scaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);
        scaleSlider.value = initialObjectScale;
    }

    void Update()
    {
        // --- Lógica de Pellizco para Escalar (Pinch-to-Scale) ---
        if (Input.touchCount == 2)
        {
            // Hay dos dedos en la pantalla
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            // Encuentra la posición de cada toque en el frame anterior
            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            // Calcula la distancia entre los toques en este frame y en el anterior
            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            // Calcula la diferencia de distancia
            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            // Ajusta la escala del objeto
            float newScaleValue = objectToControl.localScale.x - (deltaMagnitudeDiff * pinchSpeed * Time.deltaTime);
            
            // Limita la escala a los valores del slider y la aplica
            newScaleValue = Mathf.Clamp(newScaleValue, scaleSlider.minValue, scaleSlider.maxValue);
            objectToControl.localScale = Vector3.one * newScaleValue;
            
            // Actualiza el valor del slider para que coincida
            scaleSlider.value = newScaleValue;
            
            // Desactiva la rotación mientras se pellizca
            isPointerDown = false;
        }
        // --- Lógica de Arrastre para Rotar ---
        else if (isPointerDown)
        {
            // Solo procesar la rotación si el puntero está presionado y no se está pellizcando
            // Calcular el cambio en la posición X del ratón/dedo
            float deltaX = Input.mousePosition.x - lastMousePosition.x;
            
            // Aplicar la rotación alrededor del eje Y del mundo para un efecto de "tocadiscos" consistente
            if (Mathf.Abs(deltaX) > 0)
            {
                objectToControl.Rotate(Vector3.up, -deltaX * rotationSpeed * Time.deltaTime, Space.World);
            }
            
            // Actualizar la última posición del ratón
            lastMousePosition = Input.mousePosition;
        }
    }
    
    /// <summary>
    /// Este método es llamado por el evento onValueChanged del Slider.
    /// </summary>
    public void OnScaleSliderChanged(float value)
    {
        if (objectToControl != null)
        {
            // Aplica una escala uniforme al objeto
            objectToControl.localScale = new Vector3(value, value, value);
        }
    }

    /// <summary>
    /// Actualiza el valor del slider basándose en un valor recibido de la red.
    /// Se desuscribe y resuscribe al evento para evitar que se llame a OnScaleSliderChanged.
    /// </summary>
    public void UpdateSliderFromNetwork(float networkScale)
    {
        if (scaleSlider != null)
        {
            scaleSlider.onValueChanged.RemoveListener(OnScaleSliderChanged);
            scaleSlider.value = networkScale;
            scaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);
        }
    }

    /// <summary>
    /// Detecta cuándo el usuario presiona la pantalla.
    /// Este evento se activa gracias a que el panel donde está este script capturará los clics.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // --- Diagnóstico: Imprime en la consola de Unity a qué objeto se le hizo clic ---
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            Debug.Log("OnPointerDown - Objeto clickeado: " + eventData.pointerCurrentRaycast.gameObject.name);
        }
        else
        {
            Debug.Log("OnPointerDown - No se ha detectado ningún objeto de UI.");
            return;
        }

        // --- Lógica de inicio de rotación ---
        // Si el objeto sobre el que hacemos clic es parte del slider, no hacemos nada.
        if (eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<Slider>() != null)
        {
            return; // Ignoramos el clic.
        }

        // Si no es el slider, iniciamos la rotación.
        isPointerDown = true;
        lastMousePosition = Input.mousePosition;
        if (udpClient != null) udpClient.StartLocalControl();
    }

    /// <summary>
    /// Detecta cuándo el usuario suelta la pantalla.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        if (udpClient != null) udpClient.StopLocalControl();
    }

    private void OnDestroy()
    {
        // Limpiar el listener para evitar errores
        if (scaleSlider != null)
        {
            scaleSlider.onValueChanged.RemoveListener(OnScaleSliderChanged);
        }
    }
}
