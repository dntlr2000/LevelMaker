using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueDungeonLab
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController :
        MonoBehaviour,
        IDungeonRunStatePlayer
    {
        public const string TemporaryPlayerName = "__RogueDungeonLab_TemporaryPlayer";

        [Min(0.1f)] public float moveSpeed = 5.5f;
        [Min(1f)] public float sprintMultiplier = 1.8f;
        [Min(0f)] public float jumpHeight = 1.2f;
        public float gravity = -22f;
        [Min(0f)] public float rotationSpeed = 720f;

        [SerializeField] private RogueDungeonGenerator generator;
        [SerializeField] private Camera targetCamera;
        private CharacterController _characterController;
        private float _verticalVelocity;
        private bool _subscribed;

        public static PrototypePlayerController Active { get; private set; }
        public RogueDungeonGenerator Generator
        {
            get { return generator; }
        }
        public RogueDungeonGenerator RunStateGenerator
        {
            get { return generator; }
        }
        public Transform RunStateTransform
        {
            get { return transform; }
        }

        // 임시 플레이어의 필수 CharacterController 참조를 준비합니다.
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        // 활성 인스턴스를 등록하고 생성기 및 카메라 참조를 복구합니다.
        private void OnEnable()
        {
            if (Active != null && Active != this) { enabled = false; return; }
            Active = this;
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (generator == null) generator = FindAnyObjectByType<RogueDungeonGenerator>();
            if (targetCamera == null) targetCamera = Camera.main;
            SubscribeGenerator();
            RegisterRunStatePlayer();
            AttachCamera();
        }

        // 생성 이벤트 구독과 카메라 추적을 해제합니다.
        private void OnDisable()
        {
            UnregisterRunStatePlayer();
            UnsubscribeGenerator();
            ReleaseCamera();
            if (Active == this) Active = null;
        }

        // 매 프레임 키보드 입력을 읽어 이동, 점프와 입구 복귀를 처리합니다.
        private void Update()
        {
            if (_characterController == null || !_characterController.enabled) return;
            Vector2 move;
            bool sprint, jump, respawn;
            ReadInput(out move, out sprint, out jump, out respawn);
            if (respawn || HasFallenBelowDungeon()) TeleportToEntrance();
            MoveCharacter(move, sprint, jump, Time.deltaTime);
        }

        // 플레이 모드에서 입구에 임시 캐릭터를 중복 없이 생성합니다.
        public static PrototypePlayerController Spawn(RogueDungeonGenerator dungeonGenerator)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("임시 플레이어는 Play 모드에서만 생성할 수 있습니다.");
                return null;
            }
            PrototypePlayerController existing = Active != null ? Active : FindAnyObjectByType<PrototypePlayerController>();
            if (existing != null)
            {
                existing.Configure(dungeonGenerator, Camera.main);
                return existing;
            }
            if (dungeonGenerator == null || dungeonGenerator.CurrentLayout == null)
            {
                Debug.LogWarning("먼저 던전을 생성해야 임시 플레이어를 만들 수 있습니다.");
                return null;
            }

            GameObject root = new GameObject(TemporaryPlayerName);
            CharacterController character = root.AddComponent<CharacterController>();
            ConfigureCharacterController(character);
            CreateVisual(root.transform);
            PrototypePlayerController controller = root.AddComponent<PrototypePlayerController>();
            controller.Configure(dungeonGenerator, Camera.main);
            return controller;
        }

        // 현재 활성 임시 플레이어를 제거해 자유 시점으로 돌아갑니다.
        public static void DestroyActive()
        {
            PrototypePlayerController current = Active != null ? Active : FindAnyObjectByType<PrototypePlayerController>();
            if (current != null) Object.Destroy(current.gameObject);
        }

        // 생성기와 카메라를 연결하고 플레이어를 현재 입구로 이동시킵니다.
        public void Configure(RogueDungeonGenerator dungeonGenerator, Camera camera)
        {
            UnregisterRunStatePlayer();
            UnsubscribeGenerator();
            generator = dungeonGenerator;
            targetCamera = camera != null ? camera : Camera.main;
            SubscribeGenerator();
            RegisterRunStatePlayer();
            if (generator == null ||
                !generator.TryRestorePlayerRunState(this))
            {
                TeleportToEntrance();
            }
            AttachCamera();
        }

        // 현재 던전 입구의 월드 위치로 플레이어를 안전하게 순간이동합니다.
        public bool TeleportToEntrance()
        {
            if (generator == null || generator.CurrentLayout == null) return false;
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            DungeonLayout layout = generator.CurrentLayout;
            Vector3 local = layout.CellToLocalPosition(layout.Entrance, generator.CurrentCellSize);
            Vector3 world = generator.transform.TransformPoint(local + Vector3.up * 0.08f);
            bool wasEnabled = _characterController != null && _characterController.enabled;
            if (_characterController != null) _characterController.enabled = false;
            transform.position = world;
            FaceCameraForward();
            if (_characterController != null) _characterController.enabled = wasEnabled;
            _verticalVelocity = 0f;
            return true;
        }

        // 저장된 stage-local pose를 CharacterController 충돌 없이 월드 transform으로 복원합니다.
        public void RestoreStageLocalPose(
            Transform stageTransform,
            DungeonRunPlayerState state)
        {
            if (stageTransform == null ||
                state == null ||
                !state.isPresent)
            {
                return;
            }
            if (_characterController == null)
                _characterController =
                    GetComponent<CharacterController>();
            bool wasEnabled =
                _characterController != null &&
                _characterController.enabled;
            if (_characterController != null)
                _characterController.enabled = false;
            transform.position =
                stageTransform.TransformPoint(
                    state.localPosition);
            transform.rotation =
                stageTransform.rotation *
                Quaternion.Euler(
                    state.localEulerAngles);
            if (_characterController != null)
                _characterController.enabled = wasEnabled;
            _verticalVelocity = 0f;
        }

        // 코어 RunState 계약을 통해 저장된 stage-local pose를 기존 충돌 안전 복원으로 전달합니다.
        public void RestoreRunStatePose(
            Transform stageTransform,
            DungeonRunPlayerState state)
        {
            RestoreStageLocalPose(stageTransform, state);
        }

        // 입력 방향을 카메라 수평축에 맞춰 CharacterController 이동으로 변환합니다.
        private void MoveCharacter(Vector2 move, bool sprint, bool jump, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            Vector3 forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : transform.forward;
            if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 direction = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);

            float gravityAcceleration = gravity > 0f ? -gravity : gravity;
            bool grounded = _characterController.isGrounded;
            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            if (jump && grounded) _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravityAcceleration);
            _verticalVelocity += gravityAcceleration * deltaTime;

            float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);
            _characterController.Move((direction * speed + Vector3.up * _verticalVelocity) * deltaTime);
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, rotationSpeed * deltaTime);
            }
        }

        // WASD, Shift, Space와 R 입력 상태를 읽습니다.
        private static void ReadInput(out Vector2 move, out bool sprint, out bool jump, out bool respawn)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { move = Vector2.zero; sprint = jump = respawn = false; return; }
            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            jump = keyboard.spaceKey.wasPressedThisFrame;
            respawn = keyboard.rKey.wasPressedThisFrame;
#else
            move = Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            jump = Input.GetKeyDown(KeyCode.Space);
            respawn = Input.GetKeyDown(KeyCode.R);
#endif
        }

        // 던전 재생성 후 새 입구로 이동하고 카메라 추적을 복구합니다.
        private void HandleGenerated(GenerationReport report)
        {
            if (generator == null ||
                !generator.TryRestorePlayerRunState(this))
            {
                TeleportToEntrance();
            }
            AttachCamera();
        }

        // 연결된 생성기의 재생성 완료 이벤트를 한 번만 구독합니다.
        private void SubscribeGenerator()
        {
            if (generator == null || _subscribed) return;
            generator.GenerationCompleted += HandleGenerated;
            _subscribed = true;
        }

        // 연결된 생성기의 재생성 완료 이벤트 구독을 해제합니다.
        private void UnsubscribeGenerator()
        {
            if (generator != null && _subscribed) generator.GenerationCompleted -= HandleGenerated;
            _subscribed = false;
        }

        // 현재 Generator에 Sample 플레이어를 런 상태 pose 공급자로 등록합니다.
        private void RegisterRunStatePlayer()
        {
            if (generator != null)
                generator.RegisterRunStatePlayer(this);
        }

        // 비활성화 또는 Generator 교체 전 현재 Sample 플레이어 등록을 해제합니다.
        private void UnregisterRunStatePlayer()
        {
            if (generator != null)
                generator.UnregisterRunStatePlayer(this);
        }

        // 현재 카메라의 LabOrbitCamera가 플레이어를 추적하도록 연결합니다.
        private void AttachCamera()
        {
            LabOrbitCamera orbit = targetCamera != null ? targetCamera.GetComponent<LabOrbitCamera>() : null;
            if (orbit == null) orbit = FindAnyObjectByType<LabOrbitCamera>();
            if (orbit != null) orbit.SetFollowTarget(transform);
        }

        // 이 플레이어를 추적 중인 카메라를 자유 시점으로 되돌립니다.
        private void ReleaseCamera()
        {
            LabOrbitCamera orbit = targetCamera != null ? targetCamera.GetComponent<LabOrbitCamera>() : null;
            if (orbit == null) orbit = FindAnyObjectByType<LabOrbitCamera>();
            if (orbit != null) orbit.ClearFollowTarget(transform);
        }

        // 현재 카메라가 바라보는 수평 방향으로 플레이어의 초기 회전을 맞춥니다.
        private void FaceCameraForward()
        {
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        // 플레이어가 던전 아래로 추락했는지 검사합니다.
        private bool HasFallenBelowDungeon()
        {
            return generator != null && transform.position.y < generator.transform.position.y - 12f;
        }

        // CharacterController의 프로토타입 이동 충돌 크기를 설정합니다.
        private static void ConfigureCharacterController(CharacterController character)
        {
            character.height = 1.8f;
            character.radius = 0.35f;
            character.center = Vector3.up * 0.9f;
            character.stepOffset = 0.35f;
            character.slopeLimit = 50f;
            character.skinWidth = 0.04f;
        }

        // 임시 플레이어의 캡슐 몸체와 진행 방향 표시를 생성합니다.
        private static void CreateVisual(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.ForColor(new Color(0.2f, 0.58f, 1f));
            DisableAndDestroyCollider(body);

            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing";
            facing.transform.SetParent(parent, false);
            facing.transform.localPosition = new Vector3(0f, 1.05f, 0.36f);
            facing.transform.localScale = new Vector3(0.18f, 0.18f, 0.28f);
            facing.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.ForColor(Color.white);
            DisableAndDestroyCollider(facing);
        }

        // 시각용 프리미티브의 중복 물리 충돌체를 즉시 비활성화하고 제거합니다.
        private static void DisableAndDestroyCollider(GameObject value)
        {
            Collider collider = value.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }
}
