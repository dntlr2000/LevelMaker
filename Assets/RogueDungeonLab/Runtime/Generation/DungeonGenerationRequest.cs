using System;

namespace RogueDungeonLab
{
    public static class DungeonGeneratorVersions
    {
        public const int LegacyV1 = 1;
        public const int StableV2 = 2;
        // 기존 settings-only 호출의 승인된 결과를 보존하기 위해 명시적 전환 전까지 기본값은 LegacyV1입니다.
        public const int Current = LegacyV1;

        public static bool IsSupported(int version)
        {
            return version == LegacyV1 || version == StableV2;
        }
    }

    [Serializable]
    public sealed class DungeonGenerationRequest
    {
        public DungeonRecipeSnapshot recipeSnapshot = new DungeonRecipeSnapshot();
        public int seed;
        public int generatorVersion = DungeonGeneratorVersions.Current;
        public string catalogPlanningHash = string.Empty;
        public string requestId = string.Empty;
        public DungeonContentCatalogPlanningSnapshot contentCatalogSnapshot;

        public string RecipeHash
        {
            get { return recipeSnapshot != null ? recipeSnapshot.ComputeHash() : string.Empty; }
        }

        // 기존 설정 자산과 시드를 원본 변경 없는 생성 요청으로 변환합니다.
        public static DungeonGenerationRequest Create(
            RogueDungeonSettings settings,
            int seed,
            int generatorVersion = DungeonGeneratorVersions.Current,
            string catalogPlanningHash = "",
            string requestId = "",
            DungeonContentCatalogPlanningSnapshot contentCatalogSnapshot = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            DungeonContentCatalogPlanningSnapshot captured = contentCatalogSnapshot != null
                ? contentCatalogSnapshot.DeepClone()
                : null;
            string resolvedCatalogHash = catalogPlanningHash ?? string.Empty;
            if (captured != null)
            {
                string actualHash = captured.ComputeHash();
                if (!string.IsNullOrEmpty(resolvedCatalogHash) &&
                    !string.Equals(resolvedCatalogHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Catalog planning hash does not match its snapshot.", nameof(catalogPlanningHash));
                resolvedCatalogHash = actualHash;
            }
            return new DungeonGenerationRequest
            {
                recipeSnapshot = DungeonRecipeSnapshot.Capture(settings),
                seed = seed,
                generatorVersion = generatorVersion,
                catalogPlanningHash = resolvedCatalogHash,
                requestId = requestId ?? string.Empty,
                contentCatalogSnapshot = captured
            };
        }

        // mutable Catalog을 순수 planning snapshot과 hash로 동시에 캡처한 StableV2 요청을 만듭니다.
        public static DungeonGenerationRequest CreateStableV2(
            RogueDungeonSettings settings,
            int seed,
            DungeonContentCatalog catalog = null,
            string requestId = "")
        {
            DungeonContentCatalogPlanningSnapshot snapshot = catalog != null
                ? catalog.CapturePlanningSnapshot()
                : null;
            string planningHash = snapshot != null
                ? snapshot.ComputeHash()
                : DungeonBuiltInContentKeys.StableCatalogPlanningHash;
            return Create(
                settings,
                seed,
                DungeonGeneratorVersions.StableV2,
                planningHash,
                requestId,
                snapshot);
        }
    }
}
