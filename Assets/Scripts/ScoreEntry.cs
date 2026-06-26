using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;

public struct ScoreEntry : INetworkSerializable, IEquatable<ScoreEntry>
{
    public ulong ClientId;
    public FixedString32Bytes Name;
    public int Score;

    public bool Equals(ScoreEntry other)
    {
        return ClientId == other.ClientId && Name.Equals(other.Name) && Score == other.Score;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Score);
    }
}
