using UnityEngine;
namespace RogueDungeonLab
{
    // Item is an integration marker/trigger, not an invented ammo/health gameplay system.
    public sealed class FpsArenaContentIdentity : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private FpsArenaContentKind kind;
        [SerializeField] private Vector3Int cell;
        [SerializeField] private string contentKey;
        [SerializeField] private Vector3 plannedSize;
        public string ContentKey { get { return contentKey; } }
        public Vector3 PlannedSize { get { return plannedSize; } }
        public string StableId { get { return stableId; } }
        public FpsArenaContentKind Kind { get { return kind; } }
        public Vector3Int Cell { get { return cell; } }
        public void Initialize(FpsArenaContent record) { stableId = record.id; kind = record.kind; cell = record.cell; contentKey = record.contentKey; plannedSize = record.size; }
    }
}
