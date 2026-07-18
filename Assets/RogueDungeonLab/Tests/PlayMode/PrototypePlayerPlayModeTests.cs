using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class PrototypePlayerPlayModeTests
    {
        private GameObject _generatorObject;
        private GameObject _cameraObject;
        private RogueDungeonSettings _settings;
        private RogueDungeonGenerator _generator;
        private LabOrbitCamera _orbitCamera;
        private Keyboard _keyboard;
        private Mouse _mouse;

        // 각 테스트용 소형 던전, 주 카메라와 가상 키보드·마우스를 준비합니다.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            _settings.stageWidthCells = 20;
            _settings.stageDepthCells = 20;
            _settings.desiredRoomCount = 5;
            _settings.specialGimmickCount = 0;
            _settings.enemyProfile.baseDensity = 0f;
            _settings.destructibleProfile.baseDensity = 0f;
            _settings.propProfile.baseDensity = 0f;
            _settings.generateOnPlay = false;

            _generatorObject = new GameObject("Prototype Player Test Generator");
            _generator = _generatorObject.AddComponent<RogueDungeonGenerator>();
            _generator.settings = _settings;
            _generator.GenerateWithSeed(73125);

            _cameraObject = new GameObject("Prototype Player Test Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();
            _orbitCamera = _cameraObject.AddComponent<LabOrbitCamera>();
            _keyboard = InputSystem.AddDevice<Keyboard>("Prototype Player Test Keyboard");
            _keyboard.MakeCurrent();
            _mouse = InputSystem.AddDevice<Mouse>("Prototype Player Test Mouse");
            yield return null;
        }

        // 충돌체, 캐릭터 흐름, 3D 이동, 확장 피치 회전과 반응형 HUD 영역을 검증합니다.
        [UnityTest]
        public IEnumerator TemporaryPlayer_SpawnsMovesFollowsAndRecovers()
        {
            MeshCollider[] meshColliders = _generator.GetComponentsInChildren<MeshCollider>();
            Assert.That(meshColliders.Length, Is.EqualTo(2));
            for (int i = 0; i < meshColliders.Length; i++)
                Assert.That(meshColliders[i].sharedMesh, Is.Not.Null);

            PrototypePlayerController player = PrototypePlayerController.Spawn(_generator);
            Assert.That(player, Is.Not.Null);
            Assert.That(PrototypePlayerController.Spawn(_generator), Is.SameAs(player));
            Assert.That(Object.FindObjectsByType<PrototypePlayerController>().Length, Is.EqualTo(1));
            Assert.That(player.isActiveAndEnabled, Is.True);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(_orbitCamera.IsFollowing, Is.True);
            Assert.That(_orbitCamera.FollowTarget, Is.EqualTo(player.transform));
            Assert.That(Vector3.Distance(player.transform.position, ExpectedEntrancePosition()), Is.LessThan(0.001f));

            yield return null;
            yield return null;
            Vector3 moveStart = player.transform.position;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(_keyboard));
            Assert.That(_keyboard.wKey.isPressed, Is.True);
            Assert.That(Time.deltaTime, Is.GreaterThan(0f));
            player.SendMessage("Update", SendMessageOptions.RequireReceiver);
            player.SendMessage("Update", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Vector2 horizontalMove = new Vector2(player.transform.position.x - moveStart.x, player.transform.position.z - moveStart.z);
            Assert.That(horizontalMove.magnitude, Is.GreaterThan(0.01f));

            _generator.GenerateWithSeed(90210);
            Assert.That(Vector3.Distance(player.transform.position, ExpectedEntrancePosition()), Is.LessThan(0.001f));
            Assert.That(_orbitCamera.FollowTarget, Is.EqualTo(player.transform));

            CharacterController character = player.GetComponent<CharacterController>();
            character.Move(Vector3.down * 0.2f);
            Assert.That(character.isGrounded, Is.True);
            float jumpStart = player.transform.position.y;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(_keyboard));
            Assert.That(_keyboard.spaceKey.isPressed, Is.True);
            InvokeCharacterMove(player, Vector2.zero, false, true, 0.02f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(player.transform.position.y, Is.GreaterThan(jumpStart + 0.01f));

            PrototypePlayerController.DestroyActive();
            yield return null;
            Assert.That(PrototypePlayerController.Active, Is.Null);
            Assert.That(_orbitCamera.IsFollowing, Is.False);

            Vector3 cameraStart = _orbitCamera.target;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Vector3 cameraMove = _orbitCamera.target - cameraStart;
            Assert.That(cameraMove.magnitude, Is.GreaterThan(0.01f));
            Vector3 expectedForward = _orbitCamera.transform.forward.normalized;
            Assert.That(Vector3.Dot(cameraMove.normalized, expectedForward), Is.GreaterThan(0.999f));
            Assert.That(Mathf.Abs(cameraMove.y), Is.GreaterThan(0.001f));

            float cameraHeight = _orbitCamera.target.y;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_orbitCamera.target.y, Is.GreaterThan(cameraHeight));

            cameraHeight = _orbitCamera.target.y;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_orbitCamera.target.y, Is.LessThan(cameraHeight));

            Vector3 cameraPositionBeforeRotation = _orbitCamera.transform.position;
            float yawBeforeRotation = _orbitCamera.yaw;
            _mouse.MakeCurrent();
            MouseState rotateState = new MouseState { position = new Vector2(1000f, 1000f), delta = new Vector2(40f, -20f) }.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, rotateState);
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(1000f, 1000f) });
            InputSystem.Update();
            Assert.That(_orbitCamera.yaw, Is.Not.EqualTo(yawBeforeRotation));
            Assert.That(Vector3.Distance(_orbitCamera.transform.position, cameraPositionBeforeRotation), Is.LessThan(0.001f));

            _orbitCamera.pitch = 0f;
            Vector3 cameraPositionBeforeHighLook = _orbitCamera.transform.position;
            MouseState highLookState = new MouseState { position = new Vector2(1000f, 1000f), delta = new Vector2(0f, 400f) }.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, highLookState);
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(1000f, 1000f) });
            InputSystem.Update();
            Assert.That(_orbitCamera.pitch, Is.LessThan(-15f));
            Assert.That(_orbitCamera.pitch, Is.GreaterThanOrEqualTo(-89f));
            Assert.That(Vector3.Distance(_orbitCamera.transform.position, cameraPositionBeforeHighLook), Is.LessThan(0.001f));

            AssertPanelFitsScreen(320, 180);
            AssertPanelFitsScreen(360, 800);
            AssertPanelFitsScreen(3840, 1080);
        }

        // 지정 해상도에서 계산한 HUD 패널이 화면의 네 경계를 벗어나지 않는지 확인합니다.
        private static void AssertPanelFitsScreen(int width, int height)
        {
            MethodInfo method = typeof(RuntimeLabHUD).GetMethod("CalculatePanelScreenRect", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Rect panel = (Rect)method.Invoke(null, new object[] { width, height });
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(width + 0.01f));
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(height + 0.01f));
        }

        // 현재 레이아웃의 입구 셀을 임시 캐릭터의 예상 월드 위치로 변환합니다.
        private Vector3 ExpectedEntrancePosition()
        {
            DungeonLayout layout = _generator.CurrentLayout;
            Vector3 local = layout.CellToLocalPosition(layout.Entrance, _settings.cellSize) + Vector3.up * 0.08f;
            return _generator.transform.TransformPoint(local);
        }

        // 프레임 입력 타이밍에 의존하지 않고 실제 비공개 이동 메서드의 점프 분기를 호출합니다.
        private static void InvokeCharacterMove(PrototypePlayerController player, Vector2 move, bool sprint, bool jump, float deltaTime)
        {
            MethodInfo method = typeof(PrototypePlayerController).GetMethod("MoveCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(player, new object[] { move, sprint, jump, deltaTime });
        }

        // 테스트가 만든 입력 장치, 캐릭터, 장면 오브젝트와 설정을 모두 정리합니다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PrototypePlayerController.DestroyActive();
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            if (_generatorObject != null) Object.Destroy(_generatorObject);
            if (_settings != null) Object.Destroy(_settings);
            yield return null;
        }
    }
}
