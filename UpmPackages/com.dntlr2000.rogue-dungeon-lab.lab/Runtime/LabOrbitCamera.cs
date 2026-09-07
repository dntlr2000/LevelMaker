using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueDungeonLab
{
    // 자유 시점 키보드 이동과 임시 플레이어 추적 기능을 제공합니다.
    public sealed partial class LabOrbitCamera : MonoBehaviour
    {
        [Header("Keyboard Movement")]
        [Min(0f)] public float keyboardMoveSpeed = 14f;
        [Min(1f)] public float keyboardBoostMultiplier = 2.5f;
        [Header("Player Follow")]
        [Min(0f)] public float followHeight = 1.1f;
        [Min(3f)] public float followDistance = 12f;

        private Transform _followTarget;

        public bool IsFollowing { get { return _followTarget != null; } }
        public Transform FollowTarget { get { return _followTarget; } }

        // 카메라가 지정 Transform을 중심으로 회전하도록 추적 모드를 시작합니다.
        public void SetFollowTarget(Transform value)
        {
            _followTarget = value;
            if (_followTarget == null) return;
            target = _followTarget.position + Vector3.up * followHeight;
            distance = Mathf.Clamp(followDistance, minimumDistance, maximumDistance);
            ApplyTransform();
        }

        // 지정 대상이 현재 추적 대상일 때 자유 시점으로 돌아갑니다.
        public void ClearFollowTarget(Transform expectedTarget = null)
        {
            if (expectedTarget != null && _followTarget != expectedTarget) return;
            _followTarget = null;
        }

        // 추적 중인 대상의 최신 위치를 카메라 중심점에 반영합니다.
        private void UpdateFollowTarget()
        {
            if (_followTarget != null) target = _followTarget.position + Vector3.up * followHeight;
        }

        // 자유 시점 회전 후 현재 카메라 위치가 유지되도록 새 시선 방향 앞에 회전 중심을 다시 배치합니다.
        private void RebuildFreeTarget(Vector3 cameraPosition)
        {
            Quaternion viewRotation = Quaternion.Euler(pitch, yaw, 0f);
            target = cameraPosition + viewRotation * Vector3.forward * distance;
        }

        // 자유 시점에서 WASD와 상승·하강 입력을 카메라의 3차원 이동으로 적용합니다.
        private void ApplyFreeCameraKeyboardMove()
        {
            if (IsFollowing) return;
            Vector2 move;
            float elevation;
            bool boost;
            if (!ReadKeyboardMove(out move, out elevation, out boost) || (move.sqrMagnitude <= 0.0001f && Mathf.Abs(elevation) <= 0.0001f)) return;
            Quaternion viewRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 forward = viewRotation * Vector3.forward;
            Vector3 right = viewRotation * Vector3.right;
            Vector3 direction = right * move.x + forward * move.y + Vector3.up * elevation;
            float speed = keyboardMoveSpeed * (boost ? keyboardBoostMultiplier : 1f);
            target += direction.normalized * speed * Time.deltaTime;
        }

        // 새 Input System 또는 레거시 입력에서 WASD, Space, Ctrl과 Shift 상태를 읽습니다.
        private static bool ReadKeyboardMove(out Vector2 move, out float elevation, out bool boost)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { move = Vector2.zero; elevation = 0f; boost = false; return false; }
            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            elevation = (keyboard.spaceKey.isPressed ? 1f : 0f) - (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ? 1f : 0f);
            boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            return true;
#else
            move = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            elevation = (Input.GetKey(KeyCode.Space) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 1f : 0f);
            boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return true;
#endif
        }
    }
}
