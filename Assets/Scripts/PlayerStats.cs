using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStats : NetworkBehaviour
{
    public int m_ScorePerPress = 1;
    public int m_StartHealth = 100;

    private readonly NetworkVariable<int> m_Score = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<int> m_HP = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<FixedString32Bytes> m_DisplayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
        );

    private void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            AddScoreRpc();
        }

        if (keyboard.hKey.wasPressedThisFrame)
        {
            m_HP.Value -= 5;
        }
    }

    public override void OnNetworkSpawn()
    {
        m_Score.OnValueChanged += HandleScoreChanged;
        m_HP.OnValueChanged += HandleHealthChanged;
        m_DisplayName.OnValueChanged += HandleNameChanged;

        ApplyScore(m_Score.Value);
        ApplyHealth(m_HP.Value);
        ApplyName(m_DisplayName.Value);

        if (IsServer)
        {
            m_HP.Value = m_StartHealth;
        }

        if (IsOwner && m_DisplayName.Value.Length == 0)
        {
            m_DisplayName.Value = new FixedString32Bytes($"Player {OwnerClientId}");
        }

        if (!IsServer)
        {
            RequestCurrentScoreRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        m_Score.OnValueChanged -= HandleScoreChanged;
        m_HP.OnValueChanged -= HandleHealthChanged;
        m_DisplayName.OnValueChanged -= HandleNameChanged;
    }

    private void HandleScoreChanged(int prev, int current)
    {
        ApplyScore(current);
        Debug.Log($"[PlayerStats] {OwnerClientId} 점수 변경 {prev} -> {current}");
    }

    private void HandleHealthChanged(int prev, int current)
    {
        ApplyHealth(current);
        Debug.Log($"[PlayerStats] {OwnerClientId} HP 변경 {prev} -> {current}");
    }

    private void HandleNameChanged(FixedString32Bytes prev, FixedString32Bytes current)
    {
        ApplyName(current);
        Debug.Log($"[PlayerStats] {OwnerClientId} 이름 변경 {prev} -> {current}");
    }

    private void ApplyScore(int value)
    {

    }

    private void ApplyHealth(int value)
    {

    }

    private void ApplyName(FixedString32Bytes value)
    {

    }

    [Rpc(SendTo.Server)]
    private void AddScoreRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (senderClientId != OwnerClientId) return;

        m_Score.Value += m_ScorePerPress;

        BroadcastScoreRpc(m_Score.Value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastScoreRpc(int newScore)
    {
        m_Score.Value = newScore;
        ApplyScore(m_Score.Value);
    }

    [Rpc(SendTo.Server)]
    private void RequestCurrentScoreRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        SendCurrentScoreRpc(m_Score.Value, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server)]
    private void SendCurrentScoreRpc(int currentScore, RpcParams rpcParams)
    {
        if (!IsServer) return;
        m_Score.Value = currentScore; 
    }
}
