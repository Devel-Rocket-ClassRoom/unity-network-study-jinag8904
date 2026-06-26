using UnityEngine;
using Unity.Netcode;

public class ConnectionMenu : MonoBehaviour
{
    private NetworkManager m_NetworkManager;

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManager>();
        if (m_NetworkManager == null)
        {
            m_NetworkManager = NetworkManager.Singleton;
        } 
    }

    private void OnEnable()
    {
        if (m_NetworkManager != null)
        {
            m_NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (m_NetworkManager != null)
        {
            m_NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[ConnectionMenu] 클라이언트 접속 {clientId}");

    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"[ConnectionMenu] 클라이언트 끊김 {clientId}");

    }

    private void OnGUI()
    {
        if (m_NetworkManager == null)
        {
            GUILayout.BeginArea(new Rect(10, 10, 320, 60));
            GUILayout.Label("NetworkManager 를 찾을 수 없습니다.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 320, 220));

        // 아직 시작 전(서버도 클라이언트도 아님)이면 시작 버튼을, 그렇지 않으면 상태/종료를 보여준다.
        if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
        {
            DrawStartButtons();
        }
        else
        {
            DrawStatus();
        }

        GUILayout.EndArea();
    }

    private void DrawStartButtons()
    {
        GUILayout.Label("연결 시작 — 모드를 선택하세요");

        // StartHost/StartClient/StartServer 는 각각 bool(성공 여부)을 반환한다.
        // 여기서는 반환값을 실제로 확인해, 시작 실패 시 콘솔에 경고를 남긴다.
        if (GUILayout.Button("Host (서버 + 클라이언트)"))
        {
            if (!m_NetworkManager.StartHost())
            {
                Debug.LogWarning("[ConnectionMenu] StartHost 실패");
            }
        }

        if (GUILayout.Button("Client (서버에 접속)"))
        {
            if (!m_NetworkManager.StartClient())
            {
                Debug.LogWarning("[ConnectionMenu] StartClient 실패");
            }
        }

        if (GUILayout.Button("Server (전용 서버)"))
        {
            if (!m_NetworkManager.StartServer())
            {
                Debug.LogWarning("[ConnectionMenu] StartServer 실패");
            }
        }
    }

    private void DrawStatus()
    {
        // 현재 역할 표시. IsHost 가 우선(host = 서버 + 클라이언트이므로 IsServer/IsClient 둘 다 true).
        string mode =
            m_NetworkManager.IsHost ? "Host"
            : m_NetworkManager.IsServer ? "Server"
            : "Client";

        GUILayout.Label($"모드: {mode}");
        GUILayout.Label($"IsHost: {m_NetworkManager.IsHost}");
        GUILayout.Label($"IsServer: {m_NetworkManager.IsServer}");
        GUILayout.Label($"IsClient: {m_NetworkManager.IsClient}");
        GUILayout.Label($"IsListening: {m_NetworkManager.IsListening}");

        // ConnectedClientsIds 는 IReadOnlyList<ulong>. NGO 2.11 부터 클라이언트에서도 접근 가능하지만,
        // '서버에 붙은 클라이언트 수'라는 의미를 명확히 하려고 서버/호스트일 때만 표시한다(교육적 의도).
        if (m_NetworkManager.IsServer)
        {
            GUILayout.Label($"연결된 클라이언트 수: {m_NetworkManager.ConnectedClientsIds.Count}");
        }

        GUILayout.Space(8);

        if (GUILayout.Button("Shutdown (연결 종료)"))
        {
            // Shutdown 후에는 NGO 가 더 이상 씬 로드를 구동하지 않으므로,
            // 메뉴/로비로 돌아가려면 UnityEngine.SceneManagement.SceneManager 로 직접 로드해야 한다.
            m_NetworkManager.Shutdown();
        }
    }
}
