using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.VisualScripting;
using UnityEngine;

public class RelayBootstrap : MonoBehaviour
{
    public int m_MaxConnection = 4;
    public string m_ConnectionType = "dtls";
    private NetworkManager m_NetworkManager;    // 싱글톤

    private string m_HostJoinCode = string.Empty;   // JoinCode
    private string m_JoinCodeInput = string.Empty;  

    private bool m_IsBusy;  // 중복 호출 방지 플래그

    private string m_Status = "대기 중...";

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManager>();

        if (m_NetworkManager == null)
        {
            m_NetworkManager = NetworkManager.Singleton;
        }
    }

    public async UniTask EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[RelayBootstrap] UnityServices 초기화 완료");
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[RelayBootstrap] 익명 로그인 완료 PlayerId = {AuthenticationService.Instance.PlayerId}");
        }
    }

    private UnityTransport GetUnityTransport()
    {
        var transport = m_NetworkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            throw new InvalidOperationException();
        }

        return transport;
    }

    public async UniTask<string> StartHostWithRelayAsync(int maxConnection)
    {
        await EnsureSignedInAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnection);
        Debug.Log($"[RelayBootstrap] CreateAllocationAsync 완료 ({allocation.AllocationId})");

        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.Log($"[RelayBootstrap] join 코드 발급 ({joinCode})");

        RelayServerData serverData = AllocationUtils.ToRelayServerData(allocation, m_ConnectionType);
        GetUnityTransport().SetRelayServerData(serverData);

        bool started = m_NetworkManager.StartHost();
        if (!started)
        {
            throw new Exception("StartHost() 실패!");
        }

        Debug.Log($"[RelayBootstrap] Relay 호스트 시작 성공");
        return joinCode;
    }

    public async UniTask StartClientWithRelayAsync(string joinCode)
    {
        await EnsureSignedInAsync();

        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        RelayServerData serverData = AllocationUtils.ToRelayServerData(joinAllocation, m_ConnectionType);
        GetUnityTransport().SetRelayServerData(serverData);

        bool started = m_NetworkManager.StartClient();
        if (!started)
        {
            throw new Exception("[RelayBootstrap] StartClient() 실패!");
        }

        Debug.Log($"[RelayBootstrap] Relay 클라이언트 시작 완료");
    }

    private async UniTaskVoid HandleStartHostAsync()
    {
        if (m_IsBusy)
            return;
        m_IsBusy = true;
        m_Status = "Relay 호스트 시작 중...";
        try
        {
            m_HostJoinCode = await StartHostWithRelayAsync(m_MaxConnection);
            m_Status = $"호스트 시작됨. join 코드: {m_HostJoinCode}";
        }
        catch (Exception e)
        {
            m_Status = $"호스트 시작 실패: {e.Message}";
            Debug.LogError($"[RelayBootstrap] 호스트 시작 실패: {e}");
        }
        finally
        {
            m_IsBusy = false;
        }
    }

    private async UniTaskVoid HandleStartClientAsync(string joinCode)
    {
        if (m_IsBusy)
            return;
        m_IsBusy = true;
        m_Status = "Relay 클라이언트 시작 중...";
        try
        {
            await StartClientWithRelayAsync(joinCode);
            m_Status = "클라이언트가 호스트에 접속했습니다.";
        }
        catch (Exception e)
        {
            m_Status = $"클라이언트 시작 실패: {e.Message}";
            Debug.LogError($"[RelayBootstrap] 클라이언트 시작 실패: {e}");
        }
        finally
        {
            m_IsBusy = false;
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 360, 260));

        if (m_NetworkManager == null)
        {
            GUILayout.Label("NetworkManager 를 찾을 수 없습니다.");
            GUILayout.EndArea();
            return;
        }

        if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
        {
            DrawStartUI();
        }
        else
        {
            DrawRunningUI();
        }

        GUILayout.Space(8);
        GUILayout.Label(m_Status);

        GUILayout.EndArea();
    }

    private void DrawStartUI()
    {
        GUILayout.Label("Relay 연결 — 호스트로 시작하거나 코드로 참가");

        GUI.enabled = !m_IsBusy;

        if (GUILayout.Button("Host (Relay)"))
        {
            HandleStartHostAsync().Forget();
        }

        GUILayout.Space(6);
        GUILayout.Label("Join 코드:");
        m_JoinCodeInput = GUILayout.TextField(m_JoinCodeInput ?? string.Empty);

        if (GUILayout.Button("Client (Relay)"))
        {
            HandleStartClientAsync(m_JoinCodeInput).Forget();
        }

        GUI.enabled = true;
    }

    private void DrawRunningUI()
    {
        string mode =
            m_NetworkManager.IsHost ? "Host"
            : m_NetworkManager.IsServer ? "Server"
            : "Client";
        GUILayout.Label($"모드: {mode}");

        if (m_NetworkManager.IsHost && !string.IsNullOrEmpty(m_HostJoinCode))
        {
            GUILayout.Label("이 코드를 클라이언트에게 공유하세요:");
            GUILayout.TextField(m_HostJoinCode);
        }

        if (m_NetworkManager.IsServer)
        {
            GUILayout.Label($"연결된 클라이언트 수: {m_NetworkManager.ConnectedClientsIds.Count}");
        }

        GUILayout.Space(8);
        if (GUILayout.Button("Shutdown (연결 종료)"))
        {
            m_NetworkManager.Shutdown();
            m_HostJoinCode = string.Empty;
            m_Status = "연결을 종료했습니다.";
        }
    }
}
