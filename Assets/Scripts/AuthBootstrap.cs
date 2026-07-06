using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

public class AuthBootstrap : MonoBehaviour
{
    public bool m_SignInOnstart = true;

    public string m_EnvironmentName = "production";
    public string m_Profile = string.Empty;

    private bool m_IsBusy;

    private string m_Status = "대기 중...";

    public bool IsSignedIn =>
        UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsSignedIn;

    public string PlayerId
    {
        get
        {
            if (IsSignedIn)
            {
                return AuthenticationService.Instance.PlayerId;
            }
            return string.Empty;
        }
    }

    public bool SessionTokenExists =>
    UnityServices.State == ServicesInitializationState.Initialized &&
    AuthenticationService.Instance.SessionTokenExists;

    private bool m_EventsRegistered;

    private void RegisterEvents()
    {
        if (m_EventsRegistered) return;

        //AuthenticationService.Instance.SignedIn += ;
        //AuthenticationService.Instance.SignedOut += ;
        //AuthenticationService.Instance.SignInFailed += ;
        //AuthenticationService.Instance.Expired += ;

        m_EventsRegistered = true;
    }

    private void OnDestroy()
    {
        if (!m_EventsRegistered) return;

        //AuthenticationService.Instance.SignedIn -= ;
        //AuthenticationService.Instance.SignedOut -= ;
        //AuthenticationService.Instance.SignInFailed -= ;
        //AuthenticationService.Instance.Expired -= ;

        m_EventsRegistered = false;
    }

    private void Start()
    {
        if (m_SignInOnstart)    // 자동 로그인
        {
            HandleSignInAsync(m_Profile).Forget();
        }
    }

    private async UniTaskVoid HandleSignInAsync(string profile)
    {
        if (m_IsBusy) return;

        m_IsBusy = true;
        m_Status = "초기화 + 익명 로그인 중...";

        try
        {
            await InitializeAndSignInAsync(profile);
            m_Status = $"로그인 됨. PlayerId = {PlayerId}";
        }
        catch (System.Exception ex)
        {
            m_Status = $"로그인 실패. {ex.Message}";
        }
        finally
        {
            m_IsBusy = false;
        }
    }

    public async UniTask InitializeAndSignInAsync(string profile = null)
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            if (!string.IsNullOrWhiteSpace(m_EnvironmentName))
            {
                options.SetEnvironmentName(m_EnvironmentName);
            }
            if (!string.IsNullOrWhiteSpace(profile))
            {
                options.SetProfile(profile);
            }

            await UnityServices.InitializeAsync(options);
            Debug.Log($"[AuthBootstrap] UnityServices 초기화 완료 / env = {m_EnvironmentName}, profile = {m_Profile}");
        }

        RegisterEvents();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[AuthBootstrap] 익명 로그인 완료 / playerId = {PlayerId}");
            }
            catch (AuthenticationException ex)  // 인증 실패
            {
                Debug.LogError($"[AuthBootstrap] {ex.Message}");
                throw;
            }
            catch (RequestFailedException ex)   // 나머지
            {
                Debug.LogError($"[AuthBootstrap] {ex.Message}");
                throw;
            }
        }
    }

    public void SignOut(bool clearCredentials = false)  // false: SignOut만, true: 권한 삭제
    {
        if (!IsSignedIn) return;

        AuthenticationService.Instance.SignOut(clearCredentials);
        Debug.Log($"[AuthBootstrap] SignOut 완료");
    }

    public void ClearSessionToken()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) return;

        // 두 가지 상황
        if (IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
        }

        AuthenticationService.Instance.ClearSessionToken();
        Debug.Log($"[AuthBootstrap] ClearSessionToken 완료");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 380, 240));

        GUILayout.Label("Authentication (익명) — 상태");

        bool initialized = UnityServices.State == ServicesInitializationState.Initialized;
        GUILayout.Label($"초기화됨: {initialized}");
        GUILayout.Label($"IsSignedIn: {IsSignedIn}");
        GUILayout.Label($"PlayerId: {(string.IsNullOrEmpty(PlayerId) ? "(없음)" : PlayerId)}");
        GUILayout.Label($"SessionTokenExists: {(initialized ? SessionTokenExists.ToString() : "(미초기화)")}");

        GUILayout.Space(8);

        GUI.enabled = !m_IsBusy;

        if (GUILayout.Button("Sign In (init + 익명 로그인)"))
        {
            HandleSignInAsync(string.IsNullOrWhiteSpace(m_Profile) ? null : m_Profile).Forget();
        }

        if (GUILayout.Button("Sign Out"))
        {
            SignOut();
        }

        if (GUILayout.Button("New Player (로그아웃 + 토큰 삭제 → 새 PlayerId)"))
        {
            ClearSessionToken();
        }

        GUI.enabled = true;

        GUILayout.Space(8);
        GUILayout.Label(m_Status);

        GUILayout.EndArea();
    }
}