using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PhysicsKicker : NetworkBehaviour
{
    public float m_KickRadius = 3f;
    public float m_KickForce = 3f;
    public float m_KickUpwardForce = 0.4f;

    private void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            RequestKickRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestKickRpc()
    {
        var colliders = Physics.OverlapSphere(transform.position, m_KickRadius);
        foreach (var collider in colliders)
        {
            var body = collider.attachedRigidbody;
            if (body == null || body.isKinematic) continue; // rigidbody가 없거나 isKinematic이면 패스

            var forceDir = transform.forward;
            forceDir.y += m_KickUpwardForce;
            forceDir.Normalize();

            body.AddForce(forceDir * m_KickForce, ForceMode.Impulse);
        }
    }
}
