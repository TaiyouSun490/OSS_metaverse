using UnityEngine;

namespace Taiyo.Metaverse
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopLocomotion : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0f)] private float lookSensitivity = 2f;
        [SerializeField, Min(0f)] private float gravity = 20f;
        [SerializeField] private bool lockCursor = true;

        private CharacterController controller;
        private float pitch;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
        }

        private void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            var yaw = Input.GetAxisRaw("Mouse X") * lookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * lookSensitivity, -89f, 89f);
            transform.Rotate(0f, yaw, 0f, Space.World);
            if (viewCamera != null)
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
            verticalSpeed = controller.isGrounded ? -1f : verticalSpeed - gravity * Time.deltaTime;
            var velocity = transform.TransformDirection(input) * moveSpeed + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
