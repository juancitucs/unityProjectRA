using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Gestiona la interfaz de usuario para crear, unirse y gestionar la conexión de la sala.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Campo para la IP del servidor.")]
    public TMP_InputField serverIpInput;
    [Tooltip("Campo para el puerto del servidor.")]
    public TMP_InputField portInput;
    [Tooltip("Campo para introducir el código de la sala a la que unirse.")]
    public TMP_InputField roomCodeInput;
    [Tooltip("Texto para mostrar el código de la sala actual.")]
    public TMP_Text roomCodeDisplay;
    [Tooltip("Botón para crear una nueva sala.")]
    public Button createRoomButton;
    [Tooltip("Botón para unirse a una sala existente.")]
    public Button joinRoomButton;
    [Tooltip("Panel que contiene los controles de conexión (para desactivarlo después de conectar).")]
    public GameObject connectionPanel;

    [Header("Networking Client")]
    [Tooltip("Arrastra aquí el objeto que contiene el script UDPCubeClient.")]
    public UDPCubeClient udpClient;

    void Start()
    {
        // --- Validaciones ---
        if (udpClient == null)
        {
            Debug.LogError("[ConnectionManager] UDPCubeClient no está asignado.");
            gameObject.SetActive(false);
            return;
        }
        if (serverIpInput == null || portInput == null || roomCodeInput == null || roomCodeDisplay == null || createRoomButton == null || joinRoomButton == null || connectionPanel == null)
        {
            Debug.LogError("[ConnectionManager] Faltan una o más referencias de UI.");
            gameObject.SetActive(false);
            return;
        }

        // --- Configuración Inicial ---
        serverIpInput.text = udpClient.serverIP;
        portInput.text = udpClient.serverPort.ToString();
        roomCodeDisplay.text = "Room Code: N/A";

        // --- Listeners ---
        serverIpInput.onValueChanged.AddListener(value => udpClient.serverIP = value);
        portInput.onValueChanged.AddListener(value => {
            if (int.TryParse(value, out int port))
            {
                udpClient.serverPort = port;
            }
        });

        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
    }

    void Update()
    {
        // Actualiza la UI si el cliente ya está en una sala
        if (udpClient != null && udpClient.IsInRoom)
        {
            if (connectionPanel.activeSelf)
            {
                connectionPanel.SetActive(false); // Oculta el panel de conexión
                roomCodeDisplay.text = $"Room Code: {udpClient.CurrentRoomCode}";
            }
        }
    }

    private void OnCreateRoomClicked()
    {
        Debug.Log("Solicitando crear una sala...");
        udpClient.CreateRoom();
    }

    private void OnJoinRoomClicked()
    {
        string code = roomCodeInput.text;
        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning("El código de la sala no puede estar vacío.");
            return;
        }
        Debug.Log($"Intentando unirse a la sala {code}...");
        udpClient.JoinRoom(code);
    }

    private void OnDestroy()
    {
        if (createRoomButton != null) createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        if (joinRoomButton != null) joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
    }
}
