using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public int m_MaxPlayer = 4;
    private ISession m_Session; // 성립되어 있는 세션 레퍼런스

    public ISession CurrentSession => m_Session; // 읽기 전용

    public async UniTask EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[SessionManager] UnityServices 초기화 완료");
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[SessionManager] 익명 로그인 완료 PlayerId = {AuthenticationService.Instance.PlayerId}");
        }
    }

    public async UniTask<string> CreateSessionAsync(int maxPlayer)
    {
        await EnsureSignedInAsync();
        var options = new SessionOptions { MaxPlayers = maxPlayer, Type = "Session" }.WithRelayNetwork();
        ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);
        AdoptSession(session);
        Debug.Log($"[SessionManager] 세션과 호스트 만들어짐 / Id = {session.Id}, Code = {session.Code}");

        return session.Code;
    }

    public async UniTask JoinByCodeAsync(string code)
    {
        await EnsureSignedInAsync();
        ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
        AdoptSession(session);
        Debug.Log($"[SessionManager] 세션 참가 완료 / Id = {session.Id}, Code = {session.Code}");
    }

    public async UniTask LeaveAsync()
    {
        if (m_Session == null)
        {
            Debug.Log($"[SessionManager] 나갈 세션 없음.");
            return;
        }

        ISession leaving = m_Session;
        try
        {
            await leaving.LeaveAsync();
        }
        finally
        {
            UnsubscribeSessionEvents(leaving);
            if (ReferenceEquals(m_Session, leaving))
            {
                m_Session = null;
            }
        }
    }

    private void AdoptSession(ISession session)
    {
        if (m_Session != null && !ReferenceEquals(m_Session, session))
        {
            UnsubscribeSessionEvents(m_Session);
        }

        m_Session = session;
        SubscribeSessionEvents(m_Session);
    }

    private void SubscribeSessionEvents(ISession session)
    {
        if (session == null) return;

        session.PlayerJoined += OnPlayerJoined;
        session.Changed += OnSessionChanged;
        session.SessionPropertiesChanged += OnSessionPropertiesChanged;
    }

    private void UnsubscribeSessionEvents(ISession session)
    {
        if (session == null) return;

        session.PlayerJoined -= OnPlayerJoined;
        session.Changed -= OnSessionChanged;
        session.SessionPropertiesChanged -= OnSessionPropertiesChanged;
    }

    private void OnPlayerJoined(string playerId)
    {
        Debug.Log($"[SessionManager] 플레이어 참가 {playerId}");
    }

    private void OnSessionChanged()
    {
        Debug.Log($"[SessionManager] OnSessionChanged()");
    }

    private void OnSessionPropertiesChanged()
    {
        Debug.Log($"[SessionManager] OnSessionPropertiesChanged()");
    }

    public async UniTask QuickJoinAsync(int maxPlayer)
    {
        await EnsureSignedInAsync();

        var quickJoinOptions = new QuickJoinOptions
        {
            Filters = new List<FilterOption>
            {
                new (FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual)
            },
            Timeout = TimeSpan.FromSeconds(5),
            CreateSession = true
        };

        var sessionOptions = new SessionOptions { MaxPlayers = maxPlayer, Type = "Session" }.WithRelayNetwork();
        ISession session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoinOptions, sessionOptions);
        AdoptSession(session);
        Debug.Log($"[SessionManager] 세션 생성 완료 / Id = {session.Id}, Code = {session.Code}");
    }

    [SerializeField]
    private float m_GuiTopOffset = 10f;

    private string m_JoinCodeInput = string.Empty;

    private bool m_IsBusy;

    private string m_Status = "대기 중 — 세션을 만들거나 코드로 참가하세요.";

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, m_GuiTopOffset, 380, 300));

        if (m_Session == null)
        {
            DrawLobbyUI();
        }
        else
        {
            DrawInSessionUI();
        }

        GUILayout.Space(8);
        GUILayout.Label(m_Status);

        GUILayout.EndArea();
    }

    private void DrawLobbyUI()
    {
        GUILayout.Label("세션 — 만들거나, 코드로 참가하거나, Quick Join");

        GUI.enabled = !m_IsBusy;

        if (GUILayout.Button("세션 만들기 (Create)"))
        {
            HandleCreateAsync().Forget();
        }

        GUILayout.Space(6);
        GUILayout.Label("Join 코드:");
        m_JoinCodeInput = GUILayout.TextField(m_JoinCodeInput ?? string.Empty);

        if (GUILayout.Button("코드로 참가 (Join)"))
        {
            HandleJoinAsync(m_JoinCodeInput).Forget();
        }

        GUILayout.Space(6);
        if (GUILayout.Button("Quick Join (빈 방 찾기/없으면 생성)"))
        {
            HandleQuickJoinAsync().Forget();
        }

        GUI.enabled = true;
    }

    private void DrawInSessionUI()
    {
        GUILayout.Label($"세션 Id: {m_Session.Id}");

        GUILayout.Label("이 코드를 공유하세요:");
        GUILayout.TextField(m_Session.Code ?? string.Empty);

        GUILayout.Space(8);
        GUI.enabled = !m_IsBusy;
        if (GUILayout.Button("세션 나가기 (Leave)"))
        {
            HandleLeaveAsync().Forget();
        }
        GUI.enabled = true;
    }


    private async UniTaskVoid HandleCreateAsync()
    {
        if (m_IsBusy)
            return;
        m_IsBusy = true;
        m_Status = "세션 생성 중...";
        try
        {
            string code = await CreateSessionAsync(m_MaxPlayer);
            m_Status = $"세션 생성됨. 공유 코드: {code}";
        }
        catch (Exception e)
        {
            m_Status = $"세션 생성 실패: {e.Message}";
            Debug.LogError($"[SessionManager] 세션 생성 실패: {e}");
        }
        finally
        {
            m_IsBusy = false;
        }
    }

    private async UniTaskVoid HandleJoinAsync(string code)
    {
        if (m_IsBusy)
            return;
        m_IsBusy = true;
        m_Status = "세션 참가 중...";
        try
        {
            await JoinByCodeAsync(code);
            m_Status = "세션에 참가했습니다.";
        }
        catch (Exception e)
        {
            m_Status = $"세션 참가 실패: {e.Message}";
            Debug.LogError($"[SessionManager] 세션 참가 실패: {e}");
        }
        finally
        {
            m_IsBusy = false;
        }
    }

    private async UniTaskVoid HandleQuickJoinAsync()
    {
        if (m_IsBusy)
            return;
        m_IsBusy = true;
        m_Status = "Quick Join 중...";
        try
        {
            await QuickJoinAsync(m_MaxPlayer);
            m_Status = "세션에 매칭되었습니다.";
        }
        catch (Exception e)
        {
            m_Status = $"Quick Join 실패: {e.Message}";
            Debug.LogError($"[SessionManager] Quick Join 실패: {e}");
        }
        finally
        {
            m_IsBusy = false;
        }
    }

    private async UniTaskVoid HandleLeaveAsync()
    {
        if (m_IsBusy)
            return;
        m_IsBusy = true;
        m_Status = "세션 나가는 중...";
        try
        {
            await LeaveAsync();
            m_Status = "세션에서 나갔습니다.";
        }
        catch (Exception e)
        {
            m_Status = $"세션 나가기 실패: {e.Message}";
            Debug.LogError($"[SessionManager] 세션 나가기 실패: {e}");
        }
        finally
        {
            m_IsBusy = false;
        }
    }

}
