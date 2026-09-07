using System;

namespace RogueDungeonLab
{
    public sealed class DungeonBlueprintPostprocessContext
    {
        public string StageId { get; private set; }
        public int Seed { get; private set; }
        public int GeneratorVersion { get; private set; }
        public string RequestId { get; private set; }
        public string RecipeHash { get; private set; }
        public string CatalogPlanningHash { get; private set; }

        // 결정적 후처리에 필요한 불변 생성 식별자만 전달합니다.
        internal DungeonBlueprintPostprocessContext(
            string stageId,
            int seed,
            int generatorVersion,
            string requestId,
            string recipeHash,
            string catalogPlanningHash)
        {
            StageId = stageId ?? string.Empty;
            Seed = seed;
            GeneratorVersion = generatorVersion;
            RequestId = requestId ?? string.Empty;
            RecipeHash = recipeHash ?? string.Empty;
            CatalogPlanningHash = catalogPlanningHash ?? string.Empty;
        }
    }

    public interface IDungeonBlueprintPostprocessor
    {
        // 절차 생성 직후의 복사본에 결정적 변경을 적용하고 검증·Build할 Blueprint를 반환합니다.
        DungeonBlueprint Process(
            DungeonBlueprint source,
            DungeonBlueprintPostprocessContext context);
    }
}
