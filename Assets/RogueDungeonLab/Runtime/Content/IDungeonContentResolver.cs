using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public interface IDungeonContentResolver
    {
        // Blueprint spawn 하나를 재사용 가능한 Prefab 또는 factory 해석 결과로 변환합니다.
        bool TryResolve(DungeonSpawnRecord record, out DungeonContentResolution resolution);
    }

    public sealed class DungeonContentResolution
    {
        private readonly Func<Transform, GameObject> _factory;

        public DungeonSpawnCategory Category { get; private set; }
        public GameObject Prefab { get; private set; }
        public Func<Transform, GameObject> Factory { get { return _factory; } }
        public WeightedDropTable DropTable { get; private set; }
        public string GameplayId { get; private set; }
        public bool CanCreate { get { return Prefab != null || _factory != null; } }

        private DungeonContentResolution(
            DungeonSpawnCategory category,
            GameObject prefab,
            Func<Transform, GameObject> factory,
            WeightedDropTable dropTable,
            string gameplayId)
        {
            Category = category;
            Prefab = prefab;
            _factory = factory;
            DropTable = dropTable;
            GameplayId = gameplayId ?? string.Empty;
        }

        // 직접 Prefab 참조를 캐시하는 해석 결과를 만듭니다.
        public static DungeonContentResolution FromPrefab(
            DungeonSpawnCategory category,
            GameObject prefab,
            WeightedDropTable dropTable = null,
            string gameplayId = "")
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            return new DungeonContentResolution(category, prefab, null, dropTable, gameplayId);
        }

        // 풀이나 프로젝트별 생성 경로를 호출할 factory 해석 결과를 만듭니다.
        public static DungeonContentResolution FromFactory(
            DungeonSpawnCategory category,
            Func<Transform, GameObject> factory,
            WeightedDropTable dropTable = null,
            string gameplayId = "")
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return new DungeonContentResolution(category, null, factory, dropTable, gameplayId);
        }

        // 부모 아래에 Prefab을 복제하거나 캐시된 factory를 한 번 호출합니다.
        public GameObject CreateInstance(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (_factory != null) return _factory(parent);
            return Prefab != null ? UnityEngine.Object.Instantiate(Prefab, parent, false) : null;
        }
    }

    public sealed class DungeonPrefabContentResolver : IDungeonContentResolver
    {
        private readonly Dictionary<string, DungeonContentResolution> _resolutions;

        public DungeonContentCatalog Catalog { get; private set; }

        // 카탈로그의 직접 Prefab 참조를 ordinal key lookup으로 한 번 캐시합니다.
        public DungeonPrefabContentResolver(DungeonContentCatalog catalog)
        {
            Catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _resolutions = new Dictionary<string, DungeonContentResolution>(StringComparer.Ordinal);

            if (catalog.entries == null) return;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.contentKey) || entry.prefab == null) continue;
                if (_resolutions.ContainsKey(entry.contentKey))
                {
                    throw new ArgumentException(
                        "Dungeon content catalog contains a duplicate key: " + entry.contentKey,
                        nameof(catalog));
                }

                _resolutions.Add(
                    entry.contentKey,
                    DungeonContentResolution.FromPrefab(
                        entry.category,
                        entry.prefab,
                        entry.dropTable,
                        entry.gameplayId));
            }
        }

        // spawn의 최종 contentKey를 조회하며 category 일치 여부는 SceneBuilder가 검증합니다.
        public bool TryResolve(DungeonSpawnRecord record, out DungeonContentResolution resolution)
        {
            resolution = null;
            return record != null &&
                   !string.IsNullOrWhiteSpace(record.contentKey) &&
                   _resolutions.TryGetValue(record.contentKey, out resolution);
        }
    }
}
