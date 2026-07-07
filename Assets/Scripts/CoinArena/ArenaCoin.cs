using Unity.Netcode;
using UnityEngine;

namespace NetworkStudy.Gameplay
{
    public class ArenaCoin : NetworkBehaviour
    {
        [SerializeField]
        private float m_PickupRadius = 1.5f;

        [SerializeField]
        private int m_ScoreValue = 1;

        private bool m_Collected;

        [Rpc(SendTo.Server)]    // 코인 획득을 서버에 요청하는 형태
        public void RequestPickupRpc(RpcParams rpcParams = default)
        {
            if (m_Collected || !IsSpawned) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;    // sender의 clientId를 rpcParans에서 가져온다.
            NetworkObject playerObj = NetworkManager.SpawnManager.GetPlayerNetworkObject(senderClientId);   // sender의 playerObj를 가져온다. (거리 계산 용도)

            if (playerObj == null)
            {
                Debug.LogWarning($"플레이어 오브젝트 탐색 실패: {senderClientId}");
                return;
            }

            float distance = Vector3.Distance(playerObj.transform.position, transform.position);

            if (distance > m_PickupRadius)
            {
                Debug.LogWarning($"줍기 거리 X: {distance}");
                return;
            }

            m_Collected = true;

            CoinArenaManager manager = FindAnyObjectByType<CoinArenaManager>(); // 쓰지마
            manager.ServerAwardPoint(senderClientId, m_ScoreValue);

            NetworkObject.Despawn(); // 코인 오브젝트 제거
        }
    }
}