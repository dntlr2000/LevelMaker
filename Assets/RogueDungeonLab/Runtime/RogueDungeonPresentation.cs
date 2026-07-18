using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueDungeonLab
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed partial class LabOrbitCamera : MonoBehaviour
    {
        private const float MinimumPitch = -89f;
        private const float MaximumPitch = 89f;
        public Vector3 target=Vector3.zero;[Range(-180,180)]public float yaw=35f;[Range(MinimumPitch,MaximumPitch)]public float pitch=55f;[Min(3)]public float distance=55f;public float orbitSensitivity=0.18f,panSensitivity=0.9f,zoomSensitivity=7f,minimumDistance=8f,maximumDistance=180f;
        private RogueDungeonGenerator _generator;
        private void OnEnable(){Attach();ApplyTransform();}private void Start(){Attach();if(_generator!=null)FocusBounds(_generator.GeneratedBounds);}private void OnDisable(){if(_generator!=null)_generator.GenerationCompleted-=OnGenerated;}
        // 마우스 회전·이동·줌과 자유 시점 키보드 이동 또는 플레이어 추적을 매 프레임 반영합니다.
        private void LateUpdate()
        {
            UpdateFollowTarget();
            Vector2 pos,delta;float scroll;bool orbit,pan;
            if(ReadInput(out pos,out delta,out scroll,out orbit,out pan)&&!RuntimeLabHUD.IsPointerInside(pos))
            {
                if(orbit){Vector3 freeCameraPosition=transform.position;yaw+=delta.x*orbitSensitivity;pitch=Mathf.Clamp(pitch-delta.y*orbitSensitivity,MinimumPitch,MaximumPitch);if(!IsFollowing)RebuildFreeTarget(freeCameraPosition);}
                if(pan&&!IsFollowing){Quaternion r=Quaternion.Euler(0,yaw,0);float s=panSensitivity*Mathf.Max(0.02f,distance*0.0025f);target+=(-(r*Vector3.right)*delta.x-(r*Vector3.forward)*delta.y)*s;}
                if(Mathf.Abs(scroll)>0.0001f)distance=Mathf.Clamp(distance-scroll*zoomSensitivity,minimumDistance,maximumDistance);
            }
            ApplyFreeCameraKeyboardMove();
            UpdateFollowTarget();
            ApplyTransform();
        }
        public void FocusBounds(Bounds b){target=new Vector3(b.center.x,0,b.center.z);distance=Mathf.Clamp(Mathf.Max(b.extents.x,b.extents.z)*1.7f,minimumDistance,maximumDistance);ApplyTransform();}
        // 현재 장면의 생성기를 찾아 생성 완료 이벤트 구독을 갱신합니다.
        private void Attach(){RogueDungeonGenerator found=FindAnyObjectByType<RogueDungeonGenerator>();if(found==_generator)return;if(_generator!=null)_generator.GenerationCompleted-=OnGenerated;_generator=found;if(_generator!=null)_generator.GenerationCompleted+=OnGenerated;}
        // 던전 재생성 후 자유 시점은 전체 던전을 맞추고, 추적 시점은 플레이어 중심을 유지합니다.
        private void OnGenerated(GenerationReport r){if(IsFollowing)UpdateFollowTarget();else FocusBounds(r.worldBounds);}
        private void ApplyTransform(){Quaternion r=Quaternion.Euler(pitch,yaw,0);transform.position=target-r*Vector3.forward*distance;transform.rotation=r;}
        private static bool ReadInput(out Vector2 pos,out Vector2 delta,out float scroll,out bool orbit,out bool pan)
        {
#if ENABLE_INPUT_SYSTEM
            if(Mouse.current==null){pos=delta=Vector2.zero;scroll=0;orbit=pan=false;return false;}pos=Mouse.current.position.ReadValue();delta=Mouse.current.delta.ReadValue();scroll=Mouse.current.scroll.ReadValue().y/120f;orbit=Mouse.current.rightButton.isPressed;pan=Mouse.current.middleButton.isPressed;return true;
#else
            pos=Input.mousePosition;delta=new Vector2(Input.GetAxisRaw("Mouse X")*12f,Input.GetAxisRaw("Mouse Y")*12f);scroll=Input.mouseScrollDelta.y;orbit=Input.GetMouseButton(1);pan=Input.GetMouseButton(2);return true;
#endif
        }
    }

    [DisallowMultipleComponent]
    public sealed partial class RuntimeLabHUD : MonoBehaviour
    {
        private const float LiveRegenerationInterval = 0.08f;
        private static readonly string[] Tabs = { "스테이지 설정", "탐험", "드랍 통계" };
        private RogueDungeonGenerator _generator;
        private DropValidationService _service;
        private RogueDungeonSettings _boundSettings;
        private Vector2 _scroll;
        private int _selectedTab;
        private string _seedText = string.Empty;
        private bool _liveRegenerationPending;
        private float _nextLiveRegenerationTime;
        private GUIStyle _title;
        private GUIStyle _muted;
        private GUIStyle _header;
        private GUIStyle _warning;

        // 화면 좌하단 좌표계의 포인터가 반응형 HUD 패널 내부에 있는지 확인합니다.
        public static bool IsPointerInside(Vector2 bottomLeft)
        {
            Rect screenRect = CalculatePanelScreenRect(Screen.width, Screen.height);
            Vector2 guiPosition = new Vector2(bottomLeft.x, Screen.height - bottomLeft.y);
            return screenRect.Contains(guiPosition);
        }

        // HUD가 활성화될 때 장면 서비스와 런타임 설정을 연결합니다.
        private void OnEnable()
        {
            FindServices();
        }

        // 장면 재구성이나 도메인 전환으로 끊어진 HUD 참조를 다시 찾습니다.
        private void Update()
        {
            if (_generator == null || _service == null) FindServices();
            ProcessLiveRegeneration();
        }

        // 해상도와 화면 비율에 맞춘 스케일, 탭과 스크롤 영역으로 런타임 HUD를 그립니다.
        private void OnGUI()
        {
            Styles();
            FindServices();
            float guiScale = CalculateGuiScale(Screen.width, Screen.height);
            Rect panelRect = CalculateLogicalPanelRect(Screen.width, Screen.height, guiScale);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

            GUILayout.BeginArea(panelRect, GUI.skin.window);
            GUILayout.Label("Rogue Dungeon Lab", _title);
            int nextTab = GUILayout.Toolbar(_selectedTab, Tabs, GUILayout.Height(28f));
            if (nextTab != _selectedTab)
            {
                _selectedTab = nextTab;
                _scroll = Vector2.zero;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, false, true);
            if (_selectedTab == 0) DrawStageSettingsTab();
            else if (_selectedTab == 1) DrawExplorationTab();
            else DrawDropStatisticsTab();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        // 기준 해상도 대비 HUD 배율을 계산하고 너무 작거나 커지지 않도록 제한합니다.
        private static float CalculateGuiScale(int screenWidth, int screenHeight)
        {
            float widthScale = Mathf.Max(1, screenWidth) / 1280f;
            float heightScale = Mathf.Max(1, screenHeight) / 720f;
            return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.75f, 1.5f);
        }

        // 실제 픽셀 해상도를 GUI 배율로 환산해 화면을 벗어나지 않는 논리 패널 영역을 계산합니다.
        private static Rect CalculateLogicalPanelRect(int screenWidth, int screenHeight, float guiScale)
        {
            float logicalWidth = Mathf.Max(1, screenWidth) / Mathf.Max(0.01f, guiScale);
            float logicalHeight = Mathf.Max(1, screenHeight) / Mathf.Max(0.01f, guiScale);
            float margin = Mathf.Clamp(Mathf.Min(logicalWidth, logicalHeight) * 0.015f, 6f, 14f);
            float availableWidth = Mathf.Max(1f, logicalWidth - margin * 2f);
            float availableHeight = Mathf.Max(1f, logicalHeight - margin * 2f);
            bool portrait = screenHeight > screenWidth;
            float requestedWidth = portrait ? availableWidth : Mathf.Clamp(logicalWidth * 0.38f, 360f, 620f);
            return new Rect(margin, margin, Mathf.Min(requestedWidth, availableWidth), availableHeight);
        }

        // 카메라와 클릭 판정이 사용할 HUD의 실제 화면 픽셀 영역을 계산합니다.
        private static Rect CalculatePanelScreenRect(int screenWidth, int screenHeight)
        {
            float guiScale = CalculateGuiScale(screenWidth, screenHeight);
            Rect logicalRect = CalculateLogicalPanelRect(screenWidth, screenHeight, guiScale);
            return new Rect(logicalRect.x * guiScale, logicalRect.y * guiScale, logicalRect.width * guiScale, logicalRect.height * guiScale);
        }

        // HUD가 표시할 생성기와 드랍 통계 서비스를 현재 장면에서 찾습니다.
        private void FindServices()
        {
            if (_generator == null) _generator = FindAnyObjectByType<RogueDungeonGenerator>();
            if (_service == null)
            {
                _service = DropValidationService.Active;
                if (_service == null) _service = FindAnyObjectByType<DropValidationService>();
            }

            RogueDungeonSettings currentSettings = _generator != null ? _generator.settings : null;
            if (_boundSettings != currentSettings)
            {
                _boundSettings = currentSettings;
                _seedText = currentSettings != null ? currentSettings.seed.ToString() : string.Empty;
            }
        }

        // HUD 제목, 설명, 섹션과 경고에 사용하는 런타임 GUI 스타일을 준비합니다.
        private void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _muted = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _warning = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
        }
    }
}
