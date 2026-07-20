using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueDungeonLab
{
    public enum DropSourceKind { Enemy, Destructible }

    public sealed class DropEntryStatisticsSnapshot
    {
        public string ItemId; public bool IsNoDrop; public int Hits; public int TotalQuantity;
        public float ExpectedProbability; public float ObservedProbability; public float WilsonLow95; public float WilsonHigh95;
    }
    public sealed class DropSourceStatisticsSnapshot
    {
        public DropSourceKind SourceKind; public string TableName; public int Attempts;
        public readonly List<DropEntryStatisticsSnapshot> Entries = new List<DropEntryStatisticsSnapshot>();
    }

    [DisallowMultipleComponent]
    public sealed partial class DropValidationService : MonoBehaviour
    {
        private sealed class EntryAccumulator { public string ItemId; public bool IsNoDrop; public float ExpectedWeight; public int Hits; public int TotalQuantity; }
        private sealed class SourceAccumulator
        {
            public DropSourceKind Kind; public WeightedDropTable Table; public string TableName; public int Attempts; public int DefinitionHash; public float TotalExpectedWeight;
            public readonly Dictionary<string,EntryAccumulator> Entries=new Dictionary<string,EntryAccumulator>(StringComparer.Ordinal);
            public readonly List<string> Order=new List<string>();
        }
        private readonly Dictionary<string,SourceAccumulator> _sources=new Dictionary<string,SourceAccumulator>();
        private System.Random _random=new System.Random(12345);
        public static DropValidationService Active { get; private set; }
        public event Action StatisticsChanged;
        private void OnEnable(){if(Active==null||Active==this)Active=this;}
        private void OnDisable(){if(Active==this)Active=null;}
        public void SetRandomSeed(int seed){_random=new System.Random(seed);}
        public DropRoll RollAndRecord(DropSourceKind kind,WeightedDropTable table){if(table==null)table=kind==DropSourceKind.Enemy?RuntimeDropTables.Enemy:RuntimeDropTables.Destructible;DropRoll roll=table.Roll(_random);RecordInternal(kind,table,roll);Raise();return roll;}
        public void Simulate(DropSourceKind kind,WeightedDropTable table,int count){if(count<=0)return;if(table==null)table=kind==DropSourceKind.Enemy?RuntimeDropTables.Enemy:RuntimeDropTables.Destructible;for(int i=0;i<count;i++)RecordInternal(kind,table,table.Roll(_random));Raise();}
        public void ResetStatistics(){_sources.Clear();Raise();}
        public List<DropSourceStatisticsSnapshot> GetSnapshots()
        {
            List<DropSourceStatisticsSnapshot> result=new List<DropSourceStatisticsSnapshot>();
            foreach(KeyValuePair<string,SourceAccumulator> pair in _sources)
            {
                SourceAccumulator s=pair.Value;SyncDefinition(s);DropSourceStatisticsSnapshot snap=new DropSourceStatisticsSnapshot{SourceKind=s.Kind,TableName=s.TableName,Attempts=s.Attempts};
                for(int i=0;i<s.Order.Count;i++)
                {
                    EntryAccumulator e=s.Entries[s.Order[i]];float expected=s.TotalExpectedWeight>0?e.ExpectedWeight/s.TotalExpectedWeight:0f;float observed=s.Attempts>0?e.Hits/(float)s.Attempts:0f;float low,high;Wilson(e.Hits,s.Attempts,out low,out high);
                    snap.Entries.Add(new DropEntryStatisticsSnapshot{ItemId=e.ItemId,IsNoDrop=e.IsNoDrop,Hits=e.Hits,TotalQuantity=e.TotalQuantity,ExpectedProbability=expected,ObservedProbability=observed,WilsonLow95=low,WilsonHigh95=high});
                }
                result.Add(snap);
            }
            result.Sort(delegate(DropSourceStatisticsSnapshot a,DropSourceStatisticsSnapshot b){int k=a.SourceKind.CompareTo(b.SourceKind);return k!=0?k:string.CompareOrdinal(a.TableName,b.TableName);});return result;
        }
        // 드랍 결과를 테이블별 누적 통계에 기록합니다.
        private void RecordInternal(DropSourceKind kind,WeightedDropTable table,DropRoll roll)
        {
            string key=((int)kind)+":"+table.GetEntityId();SourceAccumulator s;if(!_sources.TryGetValue(key,out s)){s=new SourceAccumulator{Kind=kind,Table=table,TableName=table.name};_sources.Add(key,s);}SyncDefinition(s);s.Attempts++;EntryAccumulator e;if(!s.Entries.TryGetValue(roll.ItemId,out e)){e=new EntryAccumulator{ItemId=roll.ItemId,IsNoDrop=roll.IsNoDrop};s.Entries.Add(roll.ItemId,e);s.Order.Add(roll.ItemId);}e.Hits++;e.TotalQuantity+=Mathf.Max(0,roll.Quantity);
        }
        // 드랍 항목을 먼저 정규화한 뒤 정의 해시와 기대 가중치를 동기화합니다.
        private static void SyncDefinition(SourceAccumulator s)
        {
            if(s.Table==null){s.TotalExpectedWeight=0;return;}for(int i=0;i<s.Table.entries.Count;i++){DropEntry d=s.Table.entries[i];if(d!=null)d.ClampValues();}int hash=DefinitionHash(s.Table);if(s.DefinitionHash!=0&&s.DefinitionHash!=hash){s.Attempts=0;s.Entries.Clear();s.Order.Clear();}s.DefinitionHash=hash;s.TotalExpectedWeight=0;
            foreach(KeyValuePair<string,EntryAccumulator> p in s.Entries){p.Value.ExpectedWeight=0;p.Value.IsNoDrop=false;}
            for(int i=0;i<s.Table.entries.Count;i++){DropEntry d=s.Table.entries[i];if(d==null)continue;EntryAccumulator e;if(!s.Entries.TryGetValue(d.itemId,out e)){e=new EntryAccumulator{ItemId=d.itemId};s.Entries.Add(d.itemId,e);s.Order.Add(d.itemId);}e.IsNoDrop|=d.representsNoDrop;e.ExpectedWeight+=Mathf.Max(0,d.weight);s.TotalExpectedWeight+=Mathf.Max(0,d.weight);}
        }
        private static int DefinitionHash(WeightedDropTable table){unchecked{int h=17;for(int i=0;i<table.entries.Count;i++){DropEntry e=table.entries[i];if(e==null){h*=31;continue;}h=h*31+(e.itemId!=null?StringComparer.Ordinal.GetHashCode(e.itemId):0);h=h*31+e.weight.GetHashCode();h=h*31+e.minQuantity;h=h*31+e.maxQuantity;h=h*31+(e.representsNoDrop?1:0);}return h==0?1:h;}}
        private static void Wilson(int hits,int attempts,out float low,out float high){if(attempts<=0){low=0;high=1;return;}const double z=1.959963984540054;double n=attempts,p=hits/n,z2=z*z,den=1+z2/n,center=(p+z2/(2*n))/den,margin=z*Math.Sqrt((p*(1-p)+z2/(4*n))/n)/den;low=(float)Math.Max(0,center-margin);high=(float)Math.Min(1,center+margin);}
        private void Raise(){Action h=StatisticsChanged;if(h!=null)h();}
    }

    [DisallowMultipleComponent]
    public sealed partial class DestructibleDropTarget : MonoBehaviour
    {
        [SerializeField] private string targetId;[SerializeField] private DropSourceKind sourceKind;[SerializeField] private WeightedDropTable dropTable;[SerializeField] private bool spawnMarker=true;
        private bool _destroyed,_hovered;private Vector3 _baseScale;
        public string TargetId{get{return targetId;}}public DropSourceKind SourceKind{get{return sourceKind;}}public WeightedDropTable DropTable{get{return dropTable;}}
        public void Configure(string id,DropSourceKind kind,WeightedDropTable table,bool marker){targetId=id;sourceKind=kind;dropTable=table;spawnMarker=marker;_baseScale=transform.localScale;}
        // Prefab에 작성된 ID·드랍 테이블·마커 선택은 유지하고 비어 있는 값과 실제 범주만 보강합니다.
        public void ConfigureFallback(string id,DropSourceKind kind,WeightedDropTable table){if(string.IsNullOrWhiteSpace(targetId))targetId=id;sourceKind=kind;if(dropTable==null)dropTable=table;_baseScale=transform.localScale;}
        public void SetHovered(bool value){if(_destroyed||_hovered==value)return;_hovered=value;if(_baseScale==Vector3.zero)_baseScale=transform.localScale;transform.localScale=value?_baseScale*1.1f:_baseScale;}
        // 대상을 한 번만 파괴하고 드랍 추첨과 마커 생성을 처리합니다.
        public bool TryDestroy(Vector3 hitPoint)
        {
            if(_destroyed)return false;SetHovered(false);_destroyed=true;WeightedDropTable table=dropTable!=null?dropTable:(sourceKind==DropSourceKind.Enemy?RuntimeDropTables.Enemy:RuntimeDropTables.Destructible);DropValidationService service=DropValidationService.Active;if(service==null)service=FindAnyObjectByType<DropValidationService>();DropRoll roll=service!=null?service.RollAndRecord(sourceKind,table):table.Roll(new System.Random(unchecked(Environment.TickCount^GetEntityId().GetHashCode())));if(spawnMarker&&!roll.IsNoDrop)DropMarkerBehaviour.Spawn(roll,hitPoint+Vector3.up*0.35f);DungeonSpawnIdentity identity=GetComponentInParent<DungeonSpawnIdentity>();Destroy(identity!=null?identity.gameObject:gameObject);return true;
        }
    }

    public sealed partial class DropMarkerBehaviour : MonoBehaviour
    {
        private Vector3 _base;private float _spawn;private Transform _label;
        public static void Spawn(DropRoll roll,Vector3 position)
        {
            GameObject marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Drop_"+roll.ItemId;marker.transform.position=position;marker.transform.localScale=Vector3.one*0.28f;marker.GetComponent<Renderer>().sharedMaterial=PrototypeMaterials.ForColor(roll.MarkerColor);Collider c=marker.GetComponent<Collider>();if(c!=null)c.enabled=false;
            GameObject label=new GameObject("Label");label.transform.SetParent(marker.transform,false);label.transform.localPosition=Vector3.up*1.7f;TextMesh text=label.AddComponent<TextMesh>();text.text=roll.ItemId+(roll.Quantity>1?" x"+roll.Quantity:string.Empty);text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=0.12f;text.fontSize=48;text.color=Color.white;
            DropMarkerBehaviour b=marker.AddComponent<DropMarkerBehaviour>();b._base=position;b._spawn=Time.time;b._label=label.transform;
        }
        private void Update(){float age=Time.time-_spawn;transform.position=_base+Vector3.up*(Mathf.Sin(age*4f)*0.1f+age*0.08f);transform.Rotate(Vector3.up,70f*Time.deltaTime,Space.World);Camera cam=Camera.main;if(_label!=null&&cam!=null){_label.LookAt(cam.transform);_label.Rotate(0,180,0);}if(age>=4.5f)Destroy(gameObject);}
    }

    [DisallowMultipleComponent]
    public sealed partial class RogueDungeonClickInteractor : MonoBehaviour
    {
        public Camera targetCamera;public LayerMask interactionMask=~0;[Min(1f)]public float maximumDistance=500f;private DestructibleDropTarget _hovered;
        private void Update()
        {
            Camera cam=targetCamera!=null?targetCamera:Camera.main;if(cam==null){SetHovered(null);return;}Vector2 pos;bool pressed;if(!ReadPointer(out pos,out pressed)||RuntimeLabHUD.IsPointerInside(pos)){SetHovered(null);return;}RaycastHit hit;if(!Physics.Raycast(cam.ScreenPointToRay(pos),out hit,maximumDistance,interactionMask,QueryTriggerInteraction.Ignore)){SetHovered(null);return;}DestructibleDropTarget target=hit.collider.GetComponentInParent<DestructibleDropTarget>();SetHovered(target);if(pressed&&target!=null){target.TryDestroy(hit.point);SetHovered(null);}
        }
        private void OnDisable(){SetHovered(null);}private void SetHovered(DestructibleDropTarget target){if(_hovered==target)return;if(_hovered!=null)_hovered.SetHovered(false);_hovered=target;if(_hovered!=null)_hovered.SetHovered(true);}
        private static bool ReadPointer(out Vector2 position,out bool pressed)
        {
#if ENABLE_INPUT_SYSTEM
            if(Mouse.current==null){position=Vector2.zero;pressed=false;return false;}position=Mouse.current.position.ReadValue();pressed=Mouse.current.leftButton.wasPressedThisFrame;return true;
#else
            position=Input.mousePosition;pressed=Input.GetMouseButtonDown(0);return true;
#endif
        }
    }
}
