using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

/// <summary>
/// Cliente UDP para sincronizar un objeto en una sala.
/// - Se conecta a un servidor y puede crear o unirse a una sala.
/// - Envía el estado (posición/rotación/escala) del objeto.
/// - Recibe el estado de otros clientes en la misma sala y lo aplica.
/// </summary>
public class UDPCubeClient : MonoBehaviour
{
    [Header("Server Settings")]
    public string serverIP = "shall-yu.gl.at.ply.gg";
    public int serverPort = 5000;

    [Header("Object to Sync")]
    public Transform objectToSync;

    [Header("Network Settings")]
    [Tooltip("Rate at which to send transform updates (Hz)")]
    public float sendRate = 30f;
    [Tooltip("Interpolation speed for smoothing remote object movement")]
    public float lerpSpeed = 10f;

    [Header("Component References")]
    [Tooltip("El controlador de UI para este objeto.")]
    public ObjectController objectController;

    // Public property to expose the current room code
    public string CurrentRoomCode { get; private set; }
    public bool IsInRoom { get; private set; }
    
    // Flag to prevent network updates while local user is controlling the object
    public bool IsLocallyControlled { get; private set; }
    private float localControlCooldownEnd = -1f;

    // Networking components
    private UdpClient udpClient;
    private IPEndPoint serverEP;
    private Thread listenerThread;
    private volatile bool isRunning = false;

    // Thread-safe queue for messages received from the server
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    // State for sending data
    private float sendInterval;
    private float lastSendTime;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;

    // State for receiving data
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;
    private bool hasReceivedData = false;
    
    void Awake()
    {
        if (objectToSync == null)
        {
            Debug.LogError("[UDPCubeClient] Object to Sync is not assigned!");
            enabled = false;
            return;
        }

        sendInterval = 1f / sendRate;
        
        targetPosition = objectToSync.position;
        targetRotation = objectToSync.rotation;
        targetScale = objectToSync.localScale;
    }

    void Update()
    {
        // Ejecuta acciones programadas desde el hilo de escucha
        while (mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }

        if (!isRunning || !IsInRoom) return;

        if (IsLocallyControlled)
        {
            // MODO EMISOR: Soy el que controla el objeto.
            // Compruebo si el objeto se ha movido desde el ÚLTIMO ESTADO ENVIADO.
            if (HasTransformChanged())
            {
                // Si ha pasado el intervalo de envío, transmito el nuevo estado.
                if (Time.time - lastSendTime > sendInterval)
                {
                    SendTransform();
                    lastSendTime = Time.time;

                    // **LA SOLUCIÓN CLAVE**: Solo actualizo el "último estado conocido" DESPUÉS de enviar un paquete.
                    UpdateLastTransform();
                }
            }
        }
        else
        {
            // MODO RECEPTOR: Otro controla el objeto.
            // Aplico los datos de la red si ha pasado el cooldown.
            if (hasReceivedData && Time.time > localControlCooldownEnd)
            {
                // Aplica la interpolación suave hacia el estado de la red.
                objectToSync.position = Vector3.Lerp(objectToSync.position, targetPosition, Time.deltaTime * lerpSpeed);
                objectToSync.rotation = Quaternion.Slerp(objectToSync.rotation, targetRotation, Time.deltaTime * lerpSpeed);
                objectToSync.localScale = Vector3.Lerp(objectToSync.localScale, targetScale, Time.deltaTime * lerpSpeed);

                // Actualizo mi "último estado conocido" para que coincida con el movimiento interpolado.
                // Esto es crucial para no pensar que este movimiento es un cambio local.
                UpdateLastTransform();

                // Actualiza la UI para que refleje la escala de la red.
                if (objectController != null)
                {
                    objectController.UpdateSliderFromNetwork(objectToSync.localScale.x);
                }
            }
        }
    }

    // Revisa si el transform ha cambiado desde el último estado guardado. NO tiene efectos secundarios.
    private bool HasTransformChanged()
    {
        return Vector3.Distance(objectToSync.position, lastPosition) > 0.01f ||
               Quaternion.Angle(objectToSync.rotation, lastRotation) > 0.1f ||
               Vector3.Distance(objectToSync.localScale, lastScale) > 0.01f;
    }

    // Actualiza el último estado conocido al estado actual del transform.
    private void UpdateLastTransform()
    {
        lastPosition = objectToSync.position;
        lastRotation = objectToSync.rotation;
        lastScale = objectToSync.localScale;
    }

    private void InitializeClient()
    {
        if (isRunning) StopClient();

        try
        {
            // --- DNS Resolution ---
            IPAddress serverAddress = null;
            try
            {
                // Try to parse as a direct IP first
                if (!IPAddress.TryParse(serverIP, out serverAddress))
                {
                    // If parsing fails, it's likely a hostname, so resolve it
                    IPHostEntry hostEntry = Dns.GetHostEntry(serverIP);
                    foreach (var ip in hostEntry.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork) // Find the first IPv4 address
                        {
                            serverAddress = ip;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UDPCubeClient] DNS resolution failed: {e.Message}");
                isRunning = false;
                return;
            }

            if (serverAddress == null)
            {
                Debug.LogError($"[UDPCubeClient] Could not resolve an IPv4 address for server: {serverIP}");
                isRunning = false;
                return;
            }
            // --- End of DNS Resolution ---

            udpClient = new UdpClient();
            serverEP = new IPEndPoint(serverAddress, serverPort);
            udpClient.Connect(serverEP);

            isRunning = true;
            listenerThread = new Thread(ListenForServerMessages);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            Debug.Log($"[UDPCubeClient] Client started, connecting to {serverIP}:{serverPort} (Resolved to {serverAddress})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDPCubeClient] Failed to initialize: {e.Message}");
            isRunning = false;
        }
    }

    public void CreateRoom()
    {
        InitializeClient();
        var message = new CreateRoomRequest { action = "create_room" };
        string json = JsonUtility.ToJson(message);
        SendData(json);
        Debug.Log("[UDPCubeClient] Requested to create a new room.");
    }

    public void JoinRoom(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            Debug.LogError("[UDPCubeClient] Room code cannot be empty.");
            return;
        }
        InitializeClient();
        var message = new JoinRoomRequest { action = "join_room", room_code = roomCode.ToUpper() };
        string json = JsonUtility.ToJson(message);
        SendData(json);
        Debug.Log($"[UDPCubeClient] Requested to join room: {roomCode}");
    }

    private void ListenForServerMessages()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEP);
                string json = Encoding.UTF8.GetString(data);

                // Since JSON parsing must happen on the main thread for Unity's JsonUtility,
                // we'll just queue the processing.
                mainThreadActions.Enqueue(() => HandleServerMessage(json));
            }
            catch (SocketException)
            {
                // This can happen when the client is closed, so we check the isRunning flag.
                if (isRunning)
                    Debug.LogWarning("[UDPCubeClient] A socket error occurred.");
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogError($"[UDPCubeClient] Error in listener thread: {e.Message}");
            }
        }
    }
    
    private void HandleServerMessage(string json)
    {
        try
        {
            var baseMessage = JsonUtility.FromJson<BaseMessage>(json);

            switch (baseMessage.action)
            {
                case "room_created":
                    var roomCreatedMsg = JsonUtility.FromJson<RoomCreatedResponse>(json);
                    CurrentRoomCode = roomCreatedMsg.room_code;
                    IsInRoom = true;
                    Debug.Log($"[UDPCubeClient] Successfully created and joined room: {CurrentRoomCode}");
                    break;
                case "joined_room":
                    var joinedRoomMsg = JsonUtility.FromJson<JoinedRoomResponse>(json);
                    CurrentRoomCode = joinedRoomMsg.room_code;
                    IsInRoom = true;
                     Debug.Log($"[UDPCubeClient] Successfully joined room: {CurrentRoomCode}");
                    break;
                case "send_transform":
                    var transformMsg = JsonUtility.FromJson<TransformMessage>(json);
                    // Apply received transform data
                    targetPosition = transformMsg.pos;
                    targetRotation = transformMsg.rot;
                    targetScale = transformMsg.scale;
                    hasReceivedData = true;
                    break;
                case "error":
                     var errorMsg = JsonUtility.FromJson<ErrorResponse>(json);
                     Debug.LogError($"[UDPCubeClient] Server error: {errorMsg.message}");
                     StopClient();
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDPCubeClient] Could not parse server message: {json}. Error: {e.Message}");
        }
    }

    private void SendTransform()
    {
        var message = new TransformMessage
        {
            action = "send_transform",
            pos = objectToSync.position,
            rot = objectToSync.rotation,
            scale = objectToSync.localScale
        };
        string json = JsonUtility.ToJson(message);
        SendData(json);
    }
    
    private void SendData(string json)
    {
        if (!isRunning) return;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            udpClient.Send(data, data.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDPCubeClient] Failed to send data: {e.Message}");
        }
    }

    public void StartLocalControl()
    {
        IsLocallyControlled = true;
    }

    public void StopLocalControl(float cooldown = 0.2f)
    {
        IsLocallyControlled = false;
        localControlCooldownEnd = Time.time + cooldown;
    }

    private void OnApplicationQuit()
    {
        StopClient();
    }

    private void OnDisable()
    {
        StopClient();
    }

    public void StopClient()
    {
        IsInRoom = false;
        if (isRunning)
        {
            isRunning = false;
            listenerThread?.Join(); // Wait for the listener to finish
            listenerThread = null;
            udpClient?.Close();
            udpClient = null;
            Debug.Log("[UDPCubeClient] Client stopped and disconnected.");
        }
    }
}

// Helper classes for JSON serialization
[Serializable]
public class BaseMessage
{
    public string action;
}

[Serializable]
public class RoomCreatedResponse
{
    public string action;
    public string room_code;
}

[Serializable]
public class JoinedRoomResponse
{
    public string action;
    public string room_code;
}

[Serializable]
public class TransformMessage
{
    public string action;
    public Vector3 pos;
    public Quaternion rot;
    public Vector3 scale;
}

[Serializable]
public class ErrorResponse
{
    public string action;
    public string message;
}

[Serializable]
public class CreateRoomRequest
{
    public string action;
}

[Serializable]
public class JoinRoomRequest
{
    public string action;
    public string room_code;
}
