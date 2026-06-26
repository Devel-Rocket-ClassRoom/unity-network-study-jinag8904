using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class ScoreBoard : NetworkBehaviour
{
    private NetworkList<ScoreEntry> entries;

    private void Awake()
    {
        entries = new();
    }

    public override void OnNetworkSpawn()
    {
        entries.OnListChanged += OnEntriesChanged;
    }

    public override void OnNetworkDespawn()
    {
        entries.OnListChanged -= OnEntriesChanged;
    }

    private void OnClientConnected(ulong clientId)
    {
        AddEntryForClient(clientId);
    }

    private void AddEntryForClient(ulong clientId)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].ClientId == clientId) return;

        var entry = new ScoreEntry
        {
            ClientId = clientId,
            Name = new FixedString32Bytes($"Player {clientId}"),
            Score = 0
        };

        entries.Add(entry);
    }

    private void OnEntriesChanged(NetworkListEvent<ScoreEntry> changedEvent)
    {
        Debug.Log($"[ScoreBoard] {changedEvent.Type} -> {entries.Count}");
        RedrawAll();
    }

    private void RedrawAll()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Debug.Log($"{entries[i].ClientId} / {entries[i].Name} / {entries[i].Score}");
        }
    }
}
