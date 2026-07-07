using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace NetworkStudy.Gameplay
{
    public class CoinArenaManager : NetworkBehaviour
    {
        public enum GameState : byte
        {
            WaitingToStart = 0,
            Playing = 1,
            Finished = 2,
        }

        //public struct ScoreEntry : INetworkSerializable, IEquatable<ScoreEntry>
        //{
        //    public ulong ClientId;
        //    public FixedString32Bytes Name;
        //    public int Score;

        //    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        //        where T : IReaderWriter
        //    {
        //        serializer.SerializeValue(ref ClientId);
        //        serializer.SerializeValue(ref Name);
        //        serializer.SerializeValue(ref Score);
        //    }

        //    public bool Equals(ScoreEntry other)
        //    {
        //        return ClientId == other.ClientId && Name.Equals(other.Name) && Score == other.Score;
        //    }
        //}

        [SerializeField]
        private GameObject m_CoinPrefab;

        [SerializeField]
        private float m_RoundDuration = 60f;

        [SerializeField]
        private float m_CoinSpawnInterval = 2f;

        [SerializeField]
        private int m_MaxCoins = 10;

        [SerializeField]
        private float m_SpawnAreaRadius = 8f;

        private readonly NetworkVariable<float> m_TimeRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone, // 읽기 권한
            NetworkVariableWritePermission.Server   // 쓰기 권한
        );

        private readonly NetworkVariable<GameState> m_State = new NetworkVariable<GameState>(
            GameState.WaitingToStart,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private readonly NetworkVariable<FixedString32Bytes> m_WinnerName = new NetworkVariable<FixedString32Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkList<ScoreEntry> m_Scoreboard;

        public float TimeRemaining => m_TimeRemaining.Value;

        public GameState State => m_State.Value;

        public string WinnerName => m_WinnerName.Value.ToString();

        private readonly List<NetworkObject> m_ActiveCoins = new List<NetworkObject>();

        private CancellationTokenSource m_Cts;

        private void Awake()
        {
            m_Scoreboard = new NetworkList<ScoreEntry>();
        }

        public override void OnNetworkSpawn()
        {
            m_State.OnValueChanged += HandleStateChanged;
            m_WinnerName.OnValueChanged += HandleWinnerChanged;

            HandleStateChanged(m_State.Value, m_State.Value);
            
            if (IsServer)
            {
                foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                {
                    RegisterClient(clientId);
                }

                NetworkManager.OnClientConnectedCallback += RegisterClient;
                NetworkManager.OnClientDisconnectCallback += UnregisterClient;

                m_Cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

                StartRound();
                SpawnCoinsLoopAsync(m_Cts.Token).Forget();
                RoundTimerLoopAsync(m_Cts.Token).Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            m_State.OnValueChanged -= HandleStateChanged;
            m_WinnerName.OnValueChanged -= HandleWinnerChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= RegisterClient;
                NetworkManager.OnClientDisconnectCallback -= UnregisterClient;
                m_Cts?.Cancel();
                m_Cts?.Dispose();
                m_Cts = null;

                DespawnAllCoins();
            }
        }

        public void ServerAwardPoint(ulong clientId, int amount) 
        {
            if (!IsServer)
            {
                return;
            }

            for (int i = 0; i < m_Scoreboard.Count; i++)
            {
                var entry = m_Scoreboard[i];
                if (entry.ClientId == clientId)
                {
                    entry.Score += amount;
                    m_Scoreboard[i] = entry;
                    return; 
                }
            }

            m_Scoreboard.Add(new ScoreEntry
            {
                ClientId = clientId,
                Name = new FixedString32Bytes($"Player {clientId}"),
                Score = amount
            });
        }

        private void RegisterClient(ulong clientId)
        {
            for (int i = 0; i < m_Scoreboard.Count; i++)    // 이미 등록되어 있으면 스킵
            {
                var entry = m_Scoreboard[i];
                if (entry.ClientId == clientId)
                {
                    return;
                }
            }

            m_Scoreboard.Add(new ScoreEntry
            {
                ClientId = clientId,
                Name = new FixedString32Bytes($"Player {clientId}"),
                Score = 0
            });
        }

        private void UnregisterClient(ulong clientId)
        {
            for (int i = m_Scoreboard.Count - 1; i >= 0; i--)
            {
                if (m_Scoreboard[i].ClientId == clientId)
                {
                    m_Scoreboard.RemoveAt(i);
                    return;
                }
            }
        }

        private void StartRound() // 초기화
        {
            m_WinnerName.Value = default;
            m_TimeRemaining.Value = m_RoundDuration;
            m_State.Value = GameState.Playing;
            Debug.Log($"서버: 라운드 시작");
        }

        private void EndRound()
        {
            DespawnAllCoins();
            ResolveWinner();

            m_State.Value = GameState.Finished;
            Debug.Log($"서버: 라운드 종료, 승자: {m_WinnerName.Value}");
        }

        private void ResolveWinner()
        {
            int bestScore = 0;
            FixedString32Bytes winnerName = default;
            bool found = false;

            for (int i = 0; i < m_Scoreboard.Count; i++)
            {
                if (m_Scoreboard[i].Score > bestScore)
                {
                    bestScore = m_Scoreboard[i].Score;
                    winnerName = m_Scoreboard[i].Name;
                    found = true;
                }
            }

            m_WinnerName.Value = found ? winnerName : new FixedString32Bytes("없음");
        }

        private async UniTaskVoid SpawnCoinsLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && m_State.Value == GameState.Playing)
            {
                TrySpawnCoin();
                await UniTask.Delay(TimeSpan.FromSeconds(m_CoinSpawnInterval), cancellationToken: ct);
            }
        }

        private async UniTaskVoid RoundTimerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && m_State.Value == GameState.Playing)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);

                m_TimeRemaining.Value = Mathf.Max(0f, m_TimeRemaining.Value - 1f);
                if (m_TimeRemaining.Value <= 0f)
                {
                    EndRound();
                    return;
                }
            }
        }

        private void TrySpawnCoin()
        {
            if (m_State.Value != GameState.Playing) return;

            m_ActiveCoins.RemoveAll(c => c == null || !c.IsSpawned);

            if (m_ActiveCoins.Count >= m_MaxCoins) return;

            Vector3 pos = new Vector3(
                UnityEngine.Random.Range(-m_SpawnAreaRadius, m_SpawnAreaRadius),
                0.5f,
                UnityEngine.Random.Range(-m_SpawnAreaRadius, m_SpawnAreaRadius)
            );

            GameObject instance = Instantiate(m_CoinPrefab, pos, Quaternion.identity);  // 서버에만 생성된 상태 -> 모든 클라이언트에 동기화 필요

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();   // NetworkObject 컴포넌트가 붙어 있는 오브젝트만 동기화 가능
            networkObject.Spawn();
            m_ActiveCoins.Add(networkObject);
        }

        private void DespawnAllCoins()
        {
            for (int i = 0; i < m_ActiveCoins.Count; i++)
            {
                if (m_ActiveCoins[i] != null && m_ActiveCoins[i].IsSpawned)
                {
                    m_ActiveCoins[i].Despawn();
                }
            }

            m_ActiveCoins.Clear();
        }

        private void HandleStateChanged(GameState previousValue, GameState newValue)
        {
            Debug.Log($"게임 상태: {previousValue} -> {newValue}");
        }

        private void HandleWinnerChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
        {
            Debug.Log($"승자 확정: {newValue}");
        }

        private void OnGUI()
        {
            if (!IsSpawned)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 300));
            GUILayout.Label($"상태: {m_State.Value}");

            if (m_State.Value == GameState.Playing)
            {
                GUILayout.Label($"남은 시간: {Mathf.CeilToInt(m_TimeRemaining.Value)}s");
            }
            else if (m_State.Value == GameState.Finished)
            {
                GUILayout.Label($"승자: {m_WinnerName.Value}");
            }

            GUILayout.Space(6);
            GUILayout.Label("── 점수판 ──");
            for (int i = 0; i < m_Scoreboard.Count; i++)
            {
                ScoreEntry entry = m_Scoreboard[i];
                GUILayout.Label($"{entry.Name}: {entry.Score}");
            }

            GUILayout.EndArea();
        }
    }
}