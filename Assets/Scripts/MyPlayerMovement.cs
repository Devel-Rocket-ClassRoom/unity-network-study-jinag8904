using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkStudy.Student
{
    public class MyPlayerMovement : NetworkBehaviour
    {
        enum JumpState { None, Jump }
        private JumpState jumpState = JumpState.None;

        Rigidbody rb;

        [Tooltip("점프 Force")]
        [SerializeField]
        private float m_JumpForce = 10f;

        [Tooltip("초당 이동 속도(월드 유닛).")]
        [SerializeField]
        private float m_MoveSpeed = 5f;

        [Tooltip("초당 회전 속도(도).")]
        [SerializeField]
        private float m_RotateSpeed = 120f;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Debug.Log($"[MyPlayerMovement] 내 플레이어 스폰 {OwnerClientId}");
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float move = 0f;
            float turn = 0f;

            if (keyboard.wKey.isPressed)
            {
                move += 1f;
                if (keyboard.leftShiftKey.isPressed) move += 0.5f;
            }
            if (keyboard.sKey.isPressed)
            {
                move += -1f;
                if (keyboard.leftShiftKey.isPressed) move += 0.5f;
            }
            if (keyboard.dKey.isPressed) turn += 1f;
            if (keyboard.aKey.isPressed) turn += -1f;

            transform.Rotate(0f, turn * m_RotateSpeed, 0f);
            transform.Translate(0f, 0f, move * m_MoveSpeed * Time.deltaTime);

            switch (jumpState)
            {
                case JumpState.None:
                    if (keyboard.spaceKey.wasPressedThisFrame)
                    {
                        rb.AddForce(Vector3.up * m_JumpForce, ForceMode.Impulse);
                        jumpState = JumpState.Jump;
                        Debug.Log("상태: None -> Jump");
                    }
                    break;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (jumpState == JumpState.Jump && collision.gameObject.CompareTag("Ground"))
            {
                jumpState = JumpState.None;
                Debug.Log("상태: Jump -> None");
            }
        }
    }
}
