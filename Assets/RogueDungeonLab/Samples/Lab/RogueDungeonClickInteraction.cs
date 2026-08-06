using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueDungeonLab
{
    [DisallowMultipleComponent]
    public sealed partial class RogueDungeonClickInteractor : MonoBehaviour
    {
        public Camera targetCamera;
        public LayerMask interactionMask = ~0;
        [Min(1f)] public float maximumDistance = 500f;

        private DestructibleDropTarget _hovered;

        // 포인터가 HUD 밖에 있을 때 파괴 대상 hover와 한 번의 클릭 파괴를 처리합니다.
        private void Update()
        {
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                SetHovered(null);
                return;
            }

            Vector2 position;
            bool pressed;
            if (!ReadPointer(out position, out pressed) ||
                RuntimeLabHUD.IsPointerInside(position))
            {
                SetHovered(null);
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(
                    camera.ScreenPointToRay(position),
                    out hit,
                    maximumDistance,
                    interactionMask,
                    QueryTriggerInteraction.Ignore))
            {
                SetHovered(null);
                return;
            }

            DestructibleDropTarget target =
                hit.collider.GetComponentInParent<DestructibleDropTarget>();
            SetHovered(target);
            if (!pressed || target == null) return;
            target.TryDestroy(hit.point);
            SetHovered(null);
        }

        // 컴포넌트 비활성화 시 남아 있는 hover 표현을 해제합니다.
        private void OnDisable()
        {
            SetHovered(null);
        }

        // 이전 대상과 새 대상의 hover 시각 상태를 한 번씩만 갱신합니다.
        private void SetHovered(DestructibleDropTarget target)
        {
            if (_hovered == target) return;
            if (_hovered != null) _hovered.SetHovered(false);
            _hovered = target;
            if (_hovered != null) _hovered.SetHovered(true);
        }

        // 활성 입력 백엔드에서 포인터 위치와 이번 프레임 좌클릭을 읽습니다.
        private static bool ReadPointer(
            out Vector2 position,
            out bool pressed)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
            {
                position = Vector2.zero;
                pressed = false;
                return false;
            }
            position = Mouse.current.position.ReadValue();
            pressed = Mouse.current.leftButton.wasPressedThisFrame;
            return true;
#else
            position = Input.mousePosition;
            pressed = Input.GetMouseButtonDown(0);
            return true;
#endif
        }
    }
}
