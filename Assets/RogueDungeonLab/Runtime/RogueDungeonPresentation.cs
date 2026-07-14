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
        public Vector3 target=Vector3.zero;[Range(-180,180)]public float yaw=35f;[Range(15,85)]public float pitch=55f;[Min(3)]public float distance=55f;public float orbitSensitivity=0.18f,panSensitivity=0.9f,zoomSensitivity=7f,minimumDistance=8f,maximumDistance=180f;
        private RogueDungeonGenerator _generator;
        private void OnEnable(){Attach();ApplyTransform();}private void Start(){Attach();if(_generator!=null)FocusBounds(_generator.GeneratedBounds);}private void OnDisable(){if(_generator!=null)_generator.GenerationCompleted-=OnGenerated;}
        private void LateUpdate(){Vector2 pos,delta;float scroll;bool orbit,pan;if(ReadInput(out pos,out delta,out scroll,out orbit,out pan)&&!RuntimeLabHUD.IsPointerInside(pos)){if(orbit){yaw+=delta.x*orbitSensitivity;pitch=Mathf.Clamp(pitch-delta.y*orbitSensitivity,15,85);}if(pan){Quaternion r=Quaternion.Euler(0,yaw,0);float s=panSensitivity*Mathf.Max(0.02f,distance*0.0025f);target+=(-(r*Vector3.right)*delta.x-(r*Vector3.forward)*delta.y)*s;}if(Mathf.Abs(scroll)>0.0001f)distance=Mathf.Clamp(distance-scroll*zoomSensitivity,minimumDistance,maximumDistance);}ApplyTransform();}
        public void FocusBounds(Bounds b){target=new Vector3(b.center.x,0,b.center.z);distance=Mathf.Clamp(Mathf.Max(b.extents.x,b.extents.z)*1.7f,minimumDistance,maximumDistance);ApplyTransform();}
        // 현재 장면의 생성기를 찾아 생성 완료 이벤트 구독을 갱신합니다.
        private void Attach(){RogueDungeonGenerator found=FindAnyObjectByType<RogueDungeonGenerator>();if(found==_generator)return;if(_generator!=null)_generator.GenerationCompleted-=OnGenerated;_generator=found;if(_generator!=null)_generator.GenerationCompleted+=OnGenerated;}
        private void OnGenerated(GenerationReport r){FocusBounds(r.worldBounds);}private void ApplyTransform(){Quaternion r=Quaternion.Euler(pitch,yaw,0);transform.position=target-r*Vector3.forward*distance;transform.rotation=r;}
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
        private static Rect s_rect=new Rect(12,12,470,680);private RogueDungeonGenerator _generator;private DropValidationService _service;private Vector2 _scroll;private GUIStyle _title,_muted,_header,_warning;
        public static bool IsPointerInside(Vector2 bottomLeft){Vector2 gui=new Vector2(bottomLeft.x,Screen.height-bottomLeft.y);return s_rect.Contains(gui);}
        private void OnEnable(){FindServices();}private void Update(){if(_generator==null||_service==null)FindServices();}
        private void OnGUI()
        {
            Styles();FindServices();s_rect=new Rect(12,12,Mathf.Max(260,Mathf.Min(470,Screen.width-24)),Mathf.Max(220,Mathf.Min(680,Screen.height-24)));GUILayout.BeginArea(s_rect,GUI.skin.window);_scroll=GUILayout.BeginScrollView(_scroll);GUILayout.Label("Rogue Dungeon Lab",_title);GUILayout.Label("좌클릭 파괴 · 우클릭 회전 · 휠 줌 · 중클릭 이동",_muted);GUILayout.Space(5);
            if(_generator==null)GUILayout.Label("Generator가 없습니다.",_warning);else{GUILayout.BeginHorizontal();if(GUILayout.Button("현재 시드 재생성"))_generator.RegenerateActiveSeed();if(GUILayout.Button("새 시드"))_generator.GenerateNewSeed();GUILayout.EndHorizontal();if(_service!=null&&GUILayout.Button("드랍 통계 초기화"))_service.ResetStatistics();}
            GUILayout.Space(8);GUILayout.Label("생성 결과",_header);if(_generator!=null&&_generator.LastReport!=null){GenerationReport r=_generator.LastReport;GUILayout.Label(string.Format("Seed {0} · {1:0.0} ms · 방 {2} · 셀 {3} · 삼각형 {4:N0}",r.activeSeed,r.generationMilliseconds,r.roomCount,r.floorCellCount,r.meshTriangleCount));GUILayout.Label(string.Format("적 {0} · 파괴물 {1} · 지형지물 {2} · 기믹 {3}",r.enemyCount,r.destructibleCount,r.propCount,r.gimmickCount));for(int i=0;i<r.warnings.Count;i++)GUILayout.Label("⚠ "+r.warnings[i],_warning);}else GUILayout.Label("아직 생성되지 않았습니다.",_muted);
            GUILayout.Space(8);GUILayout.Label("빠른 몬테카를로",_header);if(_generator!=null&&_service!=null){GUILayout.BeginHorizontal();if(GUILayout.Button("적 +100"))_service.Simulate(DropSourceKind.Enemy,_generator.GetEffectiveDropTable(DropSourceKind.Enemy),100);if(GUILayout.Button("적 +1000"))_service.Simulate(DropSourceKind.Enemy,_generator.GetEffectiveDropTable(DropSourceKind.Enemy),1000);GUILayout.EndHorizontal();GUILayout.BeginHorizontal();if(GUILayout.Button("파괴물 +100"))_service.Simulate(DropSourceKind.Destructible,_generator.GetEffectiveDropTable(DropSourceKind.Destructible),100);if(GUILayout.Button("파괴물 +1000"))_service.Simulate(DropSourceKind.Destructible,_generator.GetEffectiveDropTable(DropSourceKind.Destructible),1000);GUILayout.EndHorizontal();}
            GUILayout.Space(8);GUILayout.Label("드랍 통계",_header);if(_service!=null){List<DropSourceStatisticsSnapshot> snaps=_service.GetSnapshots();if(snaps.Count==0)GUILayout.Label("대상을 클릭하거나 빠른 표본을 추가하세요.",_muted);for(int s=0;s<snaps.Count;s++){DropSourceStatisticsSnapshot src=snaps[s];GUILayout.Label(string.Format("{0} / {1} — {2:N0}회",src.SourceKind==DropSourceKind.Enemy?"적":"파괴물",src.TableName,src.Attempts));for(int e=0;e<src.Entries.Count;e++){DropEntryStatisticsSnapshot x=src.Entries[e];float d=x.ObservedProbability-x.ExpectedProbability;GUILayout.Label(string.Format("  {0,-15} 기대 {1,6:P1} · 관측 {2,6:P1} · Δ {3:+0.0%;-0.0%;0.0%} · 95% [{4:P1}, {5:P1}]",x.ItemId,x.ExpectedProbability,x.ObservedProbability,d,x.WilsonLow95,x.WilsonHigh95));}}}
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        // HUD가 표시할 생성기와 드랍 통계 서비스를 현재 장면에서 찾습니다.
        private void FindServices(){if(_generator==null)_generator=FindAnyObjectByType<RogueDungeonGenerator>();if(_service==null){_service=DropValidationService.Active;if(_service==null)_service=FindAnyObjectByType<DropValidationService>();}}
        private void Styles(){if(_title!=null)return;_title=new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold};_header=new GUIStyle(GUI.skin.label){fontSize=12,fontStyle=FontStyle.Bold};_muted=new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true};_warning=new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,wordWrap=true};}
    }
}
