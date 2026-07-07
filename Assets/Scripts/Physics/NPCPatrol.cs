using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class NPCPatrol : NetworkBehaviour
{
    private Animator m_Animator;
    private int m_HashIsMoving = Animator.StringToHash("IsMoving");
    private NavMeshAgent m_Agent;

    private float m_PatrolRadius = 5f;
    private float m_ArriveDistance = 1f;

    public override void OnNetworkSpawn()
    {
        m_Animator = GetComponent<Animator>();
        m_Agent = GetComponent<NavMeshAgent>();
        m_Agent.enabled = IsServer; // 서버만 NavMeshAgent를 활성화

        if (IsServer)
        {
            PickNewDestination();
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (m_Agent != null || !m_Agent.enabled) return;

        if (m_Agent.remainingDistance <= m_ArriveDistance)
        {
            PickNewDestination();
        }


    }

    private void PickNewDestination()
    {
        var random = Random.insideUnitCircle * m_PatrolRadius;
        random = transform.position + new Vector3(random.x, 0f, random.y);

        if (NavMesh.SamplePosition(random, out NavMeshHit hit, m_PatrolRadius, NavMesh.AllAreas))
        {
            m_Agent.SetDestination(hit.position);
        }
    }
}
