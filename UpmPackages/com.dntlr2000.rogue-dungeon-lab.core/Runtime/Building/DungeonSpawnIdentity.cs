using UnityEngine;

namespace RogueDungeonLab
{
    [DisallowMultipleComponent]
    public sealed class DungeonSpawnIdentity : MonoBehaviour
    {
        [SerializeField] private string spawnId = string.Empty;
        [SerializeField] private string contentKey = string.Empty;
        [SerializeField] private DungeonSpawnCategory category;
        [SerializeField] private Vector2Int cell;

        public string SpawnId { get { return spawnId; } }
        public string ContentKey { get { return contentKey; } }
        public DungeonSpawnCategory Category { get { return category; } }
        public Vector2Int Cell { get { return cell; } }

        // 생성된 오브젝트를 원본 Blueprint spawn 레코드와 연결합니다.
        public void Configure(DungeonSpawnRecord record)
        {
            if (record == null) return;
            spawnId = record.spawnId ?? string.Empty;
            contentKey = record.contentKey ?? string.Empty;
            category = record.category;
            cell = record.cell;
        }
    }
}
