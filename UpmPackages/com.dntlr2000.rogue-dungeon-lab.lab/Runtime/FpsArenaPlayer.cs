using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueDungeonLab
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpsArenaPlayer : MonoBehaviour
    {
        public FpsArenaGenerator arena;
        public Camera view;
        public float speed = 6;
        private CharacterController _controller;
        private float _vertical, _pitch;
        private void Awake() { _controller = GetComponent<CharacterController>(); }
        private void OnEnable() { if (arena != null) arena.Generated += OnGenerated; }
        private void Start() { Respawn(); }
        private void OnDisable()
        {
            if (arena != null) arena.Generated -= OnGenerated;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void OnGenerated(FpsArenaLayout layout) { Respawn(); }
        public void Respawn()
        {
            if (arena == null || arena.CurrentLayout == null) return;
            var l = arena.CurrentLayout;
            _controller.enabled = false;
            transform.position = arena.transform.TransformPoint(l.Position(l.Spawns[0]) + Vector3.up * .05f);
            transform.rotation = arena.transform.rotation;
            _controller.enabled = true; _vertical = 0; _pitch = 0;
            if (view != null) view.transform.localRotation = Quaternion.identity;
        }
        private void Update()
        {
            if (arena == null || arena.CurrentLayout == null) return;
            Vector2 move = Vector2.zero, look = Vector2.zero; bool jump = false, sprint = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                if (keyboard.rKey.wasPressedThisFrame) Respawn();
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                    jump = keyboard.spaceKey.wasPressedThisFrame; sprint = keyboard.leftShiftKey.isPressed;
                }
            }
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                if (Cursor.lockState == CursorLockMode.Locked) look = mouse.delta.ReadValue() * .1f;
            }
#endif
            transform.Rotate(0, look.x, 0); _pitch = Mathf.Clamp(_pitch - look.y, -85, 85);
            if (view != null) view.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);
            if (_controller.isGrounded && _vertical < 0) _vertical = -2;
            if (_controller.isGrounded && jump) _vertical = 6;
            _vertical -= 22 * Time.deltaTime;
            Vector3 direction = Vector3.ClampMagnitude(transform.right * move.x + transform.forward * move.y, 1);
            _controller.Move((direction * speed * (sprint ? 1.6f : 1) + Vector3.up * _vertical) * Time.deltaTime);
            if (arena.transform.InverseTransformPoint(transform.position).y < -10) Respawn();
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(12, 12, 570, 52), "FPS 아레나 · 클릭: 시점 잠금 / Esc: 해제\nWASD 이동 · Shift 달리기 · Space 점프 · R 스폰 복귀");
        }
    }
}
