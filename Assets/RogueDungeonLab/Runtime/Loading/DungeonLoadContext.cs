using System;
using UnityEngine;

namespace RogueDungeonLab
{
    public sealed class DungeonLoadContext
    {
        public DungeonStageDefinition Definition { get; private set; }
        public Transform Parent { get; private set; }
        public RogueDungeonSettings RuntimeSettings { get; set; }
        public int? ExplicitSeed { get; set; }
        public int? RunSeed { get; set; }
        public Func<int> RandomSeedProvider { get; set; }
        public string RequestId { get; set; }
        public IDungeonContentResolver ContentResolver { get; set; }
        public DungeonMissingContentPolicy? MissingContentPolicyOverride { get; set; }

        // StageDefinition과 생성 결과를 소유할 부모를 하나의 런타임 로드 요청으로 묶습니다.
        public DungeonLoadContext(
            DungeonStageDefinition definition,
            Transform parent,
            RogueDungeonSettings runtimeSettings = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            RuntimeSettings = runtimeSettings;
            RequestId = string.Empty;
        }
    }

    public static class DungeonStageSeedResolver
    {
        // SavedBlueprint는 저장 시드를 유지하고 Procedural은 explicit·run·fixed·random 우선순위로 시드를 결정합니다.
        public static int Resolve(DungeonLoadContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            DungeonStageDefinition definition = context.Definition;
            if (definition.sourceMode == DungeonStageSourceMode.SavedBlueprint)
            {
                if (definition.savedBlueprint == null || definition.savedBlueprint.blueprint == null)
                    throw CreateSeedException(DungeonStageDefinitionValidationCodes.MissingSavedBlueprint, "Saved Blueprint is missing.");
                return definition.savedBlueprint.blueprint.seed;
            }

            if (context.ExplicitSeed.HasValue) return context.ExplicitSeed.Value;
            if (definition.seedPolicy == DungeonStageSeedPolicy.RunSeed)
            {
                if (!context.RunSeed.HasValue)
                    throw CreateSeedException(DungeonStageDefinitionValidationCodes.MissingRunSeed, "RunSeed policy requires DungeonLoadContext.RunSeed.");
                return context.RunSeed.Value;
            }
            if (definition.seedPolicy == DungeonStageSeedPolicy.FixedSeed) return definition.fixedSeed;
            if (definition.seedPolicy == DungeonStageSeedPolicy.RandomPerLoad)
            {
                return context.RandomSeedProvider != null ? context.RandomSeedProvider() : CreateRandomSeed();
            }
            throw CreateSeedException(DungeonStageDefinitionValidationCodes.InvalidSeedPolicy, "Unsupported seed policy.");
        }

        // 고정이나 run seed가 없는 새 런에 사용할 임의의 32비트 시드를 만듭니다.
        public static int CreateRandomSeed()
        {
            return unchecked(Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode());
        }

        // 시드 해결 오류를 안정적인 StageDefinition 검증 코드와 함께 만듭니다.
        private static DungeonStageLoadException CreateSeedException(string code, string message)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            report.Add(code, DungeonValidationSeverity.Error, message);
            return new DungeonStageLoadException(message, report);
        }
    }
}
