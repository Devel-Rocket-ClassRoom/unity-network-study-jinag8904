using Unity.Netcode;
using UnityEngine;

public class PlayerApperance : NetworkBehaviour
{
    private readonly NetworkVariable<int> m_CharacterIndex = new();
    public GameObject[] m_Variants;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            m_CharacterIndex.Value = (int)OwnerClientId % m_Variants.Length;
        }

        m_CharacterIndex.OnValueChanged += OnIndexChanged;
        ApplyApperance(m_CharacterIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        m_CharacterIndex.OnValueChanged -= OnIndexChanged;
    }

    private void OnIndexChanged(int prev, int current)
    {
        ApplyApperance(m_CharacterIndex.Value);
    }

    private void ApplyApperance(int index)
    {
        for (int i = 0; i < m_Variants.Length; i++)
        {
            m_Variants[i].SetActive(i == index);
        }
    }
}
