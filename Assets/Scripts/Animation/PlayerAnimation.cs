using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAnimation : NetworkBehaviour
{
    private Animator m_Animator;
    private NetworkAnimator m_NetworkAnimator;

    private static readonly int m_HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int m_HashTaunt = Animator.StringToHash("Taunt");

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
        m_NetworkAnimator = GetComponent<NetworkAnimator>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)   return;
        
        float move = 0f;
        float turn = 0f;

        if (keyboard.wKey.isPressed) move += 1f;
        if (keyboard.sKey.isPressed) move += -1f;
        if (keyboard.dKey.isPressed) turn += 1f;
        if (keyboard.aKey.isPressed) turn += -1f;

        m_Animator.SetBool(m_HashIsMoving, move != 0f);

        if (keyboard.tKey.wasPressedThisFrame)
        {
            //m_Animator.SetTrigger(m_HashTaunt); // 내 거만 발생됨
            m_NetworkAnimator.SetTrigger(m_HashTaunt); // 네트워크를 통해 모든 클라이언트에 발생됨 (bool, float, int는 SetParameter로 가능)
        }
    }
}
