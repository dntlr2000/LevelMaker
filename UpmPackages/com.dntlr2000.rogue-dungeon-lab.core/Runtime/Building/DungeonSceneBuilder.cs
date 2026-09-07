using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonSceneBuildValidationCodes
    {
        public const string MissingContent = "RDL-BUILD-001";
        public const string CategoryMismatch = "RDL-BUILD-002";
        public const string InvalidResolution = "RDL-BUILD-003";
        public const string FactoryReturnedNull = "RDL-BUILD-004";
    }

    public sealed class DungeonSceneBuildException : InvalidOperationException
    {
        public DungeonValidationReport ValidationReport { get; private set; }

        public DungeonSceneBuildException(string message, DungeonValidationReport validationReport)
            : base(message)
        {
            ValidationReport = validationReport ?? new DungeonValidationReport();
        }
    }

    public struct DungeonSceneBuildOptions
    {
        public RogueDungeonSettings RuntimeSettings { get; private set; }
        public IDungeonContentResolver ContentResolver { get; private set; }
        public DungeonMissingContentPolicy MissingContentPolicy { get; private set; }

        public DungeonSceneBuildOptions(
            RogueDungeonSettings runtimeSettings,
            IDungeonContentResolver contentResolver,
            DungeonMissingContentPolicy missingContentPolicy)
        {
            RuntimeSettings = runtimeSettings;
            ContentResolver = contentResolver;
            MissingContentPolicy = missingContentPolicy;
        }
    }

    public struct DungeonSceneBuildResult
    {
        public int MeshTriangleCount;
        public ContentSpawnCounts ContentCounts;
        public int ResolvedContentCount;
        public int BuiltInFallbackCount;
        public int SkippedContentCount;
        public DungeonValidationReport ValidationReport;
    }

    public static class DungeonSceneBuilder
    {
        // 별도 런타임 설정 없이 기본 드랍 테이블과 built-in fallback으로 Blueprint를 구축합니다.
        public static DungeonSceneBuildResult Build(Transform parent, DungeonBlueprint blueprint)
        {
            return BuildInternal(
                parent,
                blueprint,
                new DungeonSceneBuildOptions(null, null, DungeonMissingContentPolicy.BuiltInFallback));
        }

        // 기존 settings 기반 호출을 유지하면서 built-in fallback 구축 경로로 전달합니다.
        public static DungeonSceneBuildResult Build(
            Transform parent,
            DungeonBlueprint blueprint,
            RogueDungeonSettings runtimeSettings)
        {
            if (runtimeSettings == null) throw new ArgumentNullException(nameof(runtimeSettings));
            return BuildInternal(
                parent,
                blueprint,
                new DungeonSceneBuildOptions(runtimeSettings, null, DungeonMissingContentPolicy.BuiltInFallback));
        }

        // resolver와 누락 정책을 포함한 옵션으로 검증된 Blueprint를 구축합니다.
        public static DungeonSceneBuildResult Build(
            Transform parent,
            DungeonBlueprint blueprint,
            DungeonSceneBuildOptions options)
        {
            return BuildInternal(parent, blueprint, options);
        }

        // 콘텐츠 해석을 먼저 검증한 뒤 공통 Blueprint 메시와 콘텐츠를 생성합니다.
        private static DungeonSceneBuildResult BuildInternal(
            Transform parent,
            DungeonBlueprint blueprint,
            DungeonSceneBuildOptions options)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (!Enum.IsDefined(typeof(DungeonMissingContentPolicy), options.MissingContentPolicy))
                throw new ArgumentOutOfRangeException(nameof(options), "Missing content policy is invalid.");

            DungeonContentBuildPlan contentPlan = DungeonContentSceneBuilder.CreatePlan(
                blueprint.spawns,
                options.ContentResolver,
                options.MissingContentPolicy);
            ThrowIfInvalid(contentPlan.ValidationReport);

            int meshTriangleCount = DungeonMeshBuilder.Build(parent, blueprint);
            ContentSpawnCounts contentCounts = DungeonContentSceneBuilder.Build(
                parent,
                contentPlan,
                options.RuntimeSettings);
            return new DungeonSceneBuildResult
            {
                MeshTriangleCount = meshTriangleCount,
                ContentCounts = contentCounts,
                ResolvedContentCount = contentPlan.ResolvedContentCount,
                BuiltInFallbackCount = contentPlan.BuiltInFallbackCount,
                SkippedContentCount = contentPlan.SkippedContentCount,
                ValidationReport = contentPlan.ValidationReport
            };
        }

        // 사전 해석 오류를 생성 부작용 전에 코드 기반 예외로 변환합니다.
        private static void ThrowIfInvalid(DungeonValidationReport report)
        {
            if (report != null && report.IsValid) return;
            throw new DungeonSceneBuildException("Dungeon content resolution is invalid.", report);
        }
    }

    internal enum DungeonContentBuildMode
    {
        Resolved,
        BuiltIn,
        GenericFallback,
        Skip
    }

    internal sealed class DungeonContentBuildInstruction
    {
        public DungeonSpawnRecord Record;
        public DungeonContentBuildMode Mode;
        public DungeonContentResolution Resolution;
    }

    internal sealed class DungeonContentBuildPlan
    {
        public readonly List<DungeonContentBuildInstruction> Instructions =
            new List<DungeonContentBuildInstruction>();
        public readonly DungeonValidationReport ValidationReport = new DungeonValidationReport();
        public int ResolvedContentCount;
        public int BuiltInFallbackCount;
        public int SkippedContentCount;
    }

    internal static class DungeonContentSceneBuilder
    {
        // 기존 wrapper가 확정 spawn 레코드를 catalog 없이 같은 built-in 경로로 구축하게 합니다.
        public static ContentSpawnCounts Build(
            Transform parent,
            List<DungeonSpawnRecord> source,
            RogueDungeonSettings settings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            DungeonContentBuildPlan plan = CreatePlan(
                source,
                null,
                DungeonMissingContentPolicy.BuiltInFallback);
            if (!plan.ValidationReport.IsValid)
                throw new DungeonSceneBuildException("Dungeon content resolution is invalid.", plan.ValidationReport);
            return Build(parent, plan, settings);
        }

        // 레코드를 stable 순서로 정렬하고 resolver, 정확한 built-in, 누락 정책 순으로 해석합니다.
        internal static DungeonContentBuildPlan CreatePlan(
            List<DungeonSpawnRecord> source,
            IDungeonContentResolver resolver,
            DungeonMissingContentPolicy missingPolicy)
        {
            List<DungeonSpawnRecord> records = source != null
                ? new List<DungeonSpawnRecord>(source)
                : new List<DungeonSpawnRecord>();
            records.Sort(CompareRecords);

            DungeonContentBuildPlan plan = new DungeonContentBuildPlan();
            for (int i = 0; i < records.Count; i++)
            {
                DungeonSpawnRecord record = records[i];
                if (record == null) continue;

                DungeonContentResolution resolution;
                if (resolver != null && resolver.TryResolve(record, out resolution))
                {
                    if (resolution == null || !resolution.CanCreate)
                    {
                        plan.ValidationReport.Add(
                            DungeonSceneBuildValidationCodes.InvalidResolution,
                            DungeonValidationSeverity.Error,
                            "Resolver returned an unusable result for content key: " + record.contentKey,
                            record.cell,
                            record.spawnId);
                    }
                    else if (resolution.Category != record.category)
                    {
                        plan.ValidationReport.Add(
                            DungeonSceneBuildValidationCodes.CategoryMismatch,
                            DungeonValidationSeverity.Error,
                            "Resolved content category does not match spawn category for key: " + record.contentKey,
                            record.cell,
                            record.spawnId);
                    }
                    else
                    {
                        plan.Instructions.Add(new DungeonContentBuildInstruction
                        {
                            Record = record,
                            Mode = DungeonContentBuildMode.Resolved,
                            Resolution = resolution
                        });
                        plan.ResolvedContentCount++;
                    }
                    continue;
                }

                DungeonSpawnCategory builtInCategory;
                if (TryGetBuiltInCategory(record.contentKey, out builtInCategory))
                {
                    if (builtInCategory != record.category)
                    {
                        plan.ValidationReport.Add(
                            DungeonSceneBuildValidationCodes.CategoryMismatch,
                            DungeonValidationSeverity.Error,
                            "Built-in content category does not match spawn category for key: " + record.contentKey,
                            record.cell,
                            record.spawnId);
                    }
                    else
                    {
                        plan.Instructions.Add(new DungeonContentBuildInstruction
                        {
                            Record = record,
                            Mode = DungeonContentBuildMode.BuiltIn
                        });
                        plan.BuiltInFallbackCount++;
                    }
                    continue;
                }

                if (missingPolicy == DungeonMissingContentPolicy.BuiltInFallback)
                {
                    plan.ValidationReport.Add(
                        DungeonSceneBuildValidationCodes.MissingContent,
                        DungeonValidationSeverity.Warning,
                        "Content key was not resolved; a category fallback will be used: " + record.contentKey,
                        record.cell,
                        record.spawnId);
                    plan.Instructions.Add(new DungeonContentBuildInstruction
                    {
                        Record = record,
                        Mode = DungeonContentBuildMode.GenericFallback
                    });
                    plan.BuiltInFallbackCount++;
                }
                else if (missingPolicy == DungeonMissingContentPolicy.Skip)
                {
                    plan.ValidationReport.Add(
                        DungeonSceneBuildValidationCodes.MissingContent,
                        DungeonValidationSeverity.Warning,
                        "Content key was not resolved and will be skipped: " + record.contentKey,
                        record.cell,
                        record.spawnId);
                    plan.Instructions.Add(new DungeonContentBuildInstruction
                    {
                        Record = record,
                        Mode = DungeonContentBuildMode.Skip
                    });
                    plan.SkippedContentCount++;
                }
                else
                {
                    plan.ValidationReport.Add(
                        DungeonSceneBuildValidationCodes.MissingContent,
                        DungeonValidationSeverity.Error,
                        "Content key was not resolved: " + record.contentKey,
                        record.cell,
                        record.spawnId);
                }
            }
            return plan;
        }

        // 사전 해석된 plan을 기존 category root와 stable hierarchy 순서로 인스턴스화합니다.
        internal static ContentSpawnCounts Build(
            Transform parent,
            DungeonContentBuildPlan plan,
            RogueDungeonSettings settings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            Transform contents = NewRoot("Contents", parent);
            Transform markers = NewRoot("Stage Markers", contents);
            Transform gimmicks = NewRoot("Special Gimmicks", contents);
            Transform enemies = NewRoot("Enemies", contents);
            Transform destructibles = NewRoot("Destructibles", contents);
            Transform props = NewRoot("Terrain Props", contents);

            ContentSpawnCounts counts = new ContentSpawnCounts();
            for (int i = 0; i < plan.Instructions.Count; i++)
            {
                DungeonContentBuildInstruction instruction = plan.Instructions[i];
                DungeonSpawnRecord record = instruction.Record;
                if (record == null || instruction.Mode == DungeonContentBuildMode.Skip) continue;

                Transform categoryRoot = CategoryRoot(
                    record.category,
                    markers,
                    gimmicks,
                    enemies,
                    destructibles,
                    props);
                GameObject instance;
                if (instruction.Mode == DungeonContentBuildMode.Resolved)
                {
                    instance = instruction.Resolution.CreateInstance(categoryRoot);
                    if (instance == null)
                    {
                        plan.ValidationReport.Add(
                            DungeonSceneBuildValidationCodes.FactoryReturnedNull,
                            DungeonValidationSeverity.Error,
                            "Resolved content factory returned null for key: " + record.contentKey,
                            record.cell,
                            record.spawnId);
                        throw new DungeonSceneBuildException(
                            "Dungeon content factory returned null.",
                            plan.ValidationReport);
                    }
                    PrepareInstance(instance, record, categoryRoot);
                    EnsureDropTarget(
                        instance,
                        record,
                        settings,
                        instruction.Resolution.DropTable,
                        instruction.Resolution.GameplayId);
                }
                else
                {
                    instance = CreateBuiltIn(
                        record,
                        categoryRoot,
                        settings,
                        instruction.Mode == DungeonContentBuildMode.GenericFallback);
                }

                if (instance != null) IncrementCount(ref counts, record.category);
            }
            return counts;
        }

        // 정확한 built-in key 또는 category generic fallback에 맞는 기존 primitive 표현을 만듭니다.
        private static GameObject CreateBuiltIn(
            DungeonSpawnRecord record,
            Transform parent,
            RogueDungeonSettings settings,
            bool genericFallback)
        {
            switch (record.category)
            {
                case DungeonSpawnCategory.Marker:
                    return CreateMarker(record, parent, genericFallback);
                case DungeonSpawnCategory.Gimmick:
                    return CreateGimmick(record, parent);
                case DungeonSpawnCategory.Enemy:
                    return CreateEnemy(record, parent, settings);
                case DungeonSpawnCategory.Destructible:
                    return CreateDestructible(record, parent, settings);
                case DungeonSpawnCategory.Prop:
                    return CreateProp(record, parent, genericFallback);
                default:
                    return null;
            }
        }

        // marker key에 맞는 색상 또는 누락 marker용 generic 색상의 collider 없는 원기둥을 생성합니다.
        private static GameObject CreateMarker(
            DungeonSpawnRecord record,
            Transform parent,
            bool genericFallback)
        {
            Material material = genericFallback
                ? PrototypeMaterials.Gimmick
                : record.contentKey == DungeonBuiltInContentKeys.EntranceMarker
                    ? PrototypeMaterials.Entrance
                    : PrototypeMaterials.Exit;
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            PrepareInstance(instance, record, parent);
            instance.GetComponent<Renderer>().sharedMaterial = material;
            DisableCollider(instance);
            return instance;
        }

        // 두 primitive 자식으로 구성된 기존 특별 기믹 표현을 생성합니다.
        private static GameObject CreateGimmick(DungeonSpawnRecord record, Transform parent)
        {
            GameObject root = new GameObject(ResolveName(record, "SpecialGimmick"));
            PrepareInstance(root, record, parent);

            GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObject.transform.SetParent(root.transform, false);
            baseObject.transform.localPosition = Vector3.up * 0.12f;
            baseObject.transform.localScale = new Vector3(0.85f, 0.12f, 0.85f);
            baseObject.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Gimmick;
            DisableCollider(baseObject);

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = Vector3.up * 0.9f;
            core.transform.localScale = Vector3.one * 0.65f;
            core.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Gimmick;
            DisableCollider(core);
            return root;
        }

        // 클릭 파괴와 적 드랍 테이블이 연결된 기존 캡슐 적을 생성합니다.
        private static GameObject CreateEnemy(
            DungeonSpawnRecord record,
            Transform parent,
            RogueDungeonSettings settings)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            PrepareInstance(instance, record, parent);
            instance.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Enemy;
            EnsureDropTarget(instance, record, settings, null, string.Empty);
            return instance;
        }

        // 클릭 파괴와 파괴물 드랍 테이블이 연결된 기존 큐브를 생성합니다.
        private static GameObject CreateDestructible(
            DungeonSpawnRecord record,
            Transform parent,
            RogueDungeonSettings settings)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrepareInstance(instance, record, parent);
            instance.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Breakable;
            EnsureDropTarget(instance, record, settings, null, string.Empty);
            return instance;
        }

        // 정확한 cylinder key가 아니거나 generic fallback이면 cube 지형지물을 생성합니다.
        private static GameObject CreateProp(
            DungeonSpawnRecord record,
            Transform parent,
            bool genericFallback)
        {
            PrimitiveType type = !genericFallback && record.contentKey == DungeonBuiltInContentKeys.PropCylinder
                ? PrimitiveType.Cylinder
                : PrimitiveType.Cube;
            GameObject instance = GameObject.CreatePrimitive(type);
            PrepareInstance(instance, record, parent);
            instance.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Prop;
            DisableCollider(instance);
            return instance;
        }

        // prefab 설정을 우선 보존하면서 누락된 드랍 계약만 catalog·실험실 기본값으로 보강합니다.
        private static void EnsureDropTarget(
            GameObject instance,
            DungeonSpawnRecord record,
            RogueDungeonSettings settings,
            WeightedDropTable resolvedDropTable,
            string gameplayId)
        {
            if (record.category != DungeonSpawnCategory.Enemy &&
                record.category != DungeonSpawnCategory.Destructible) return;

            DestructibleDropTarget target = instance.GetComponentInChildren<DestructibleDropTarget>(true);
            DropSourceKind kind = record.category == DungeonSpawnCategory.Enemy
                ? DropSourceKind.Enemy
                : DropSourceKind.Destructible;
            WeightedDropTable fallbackTable = resolvedDropTable != null
                ? resolvedDropTable
                : kind == DropSourceKind.Enemy
                ? settings != null ? settings.EffectiveEnemyDropTable : RuntimeDropTables.Enemy
                : settings != null ? settings.EffectiveDestructibleDropTable : RuntimeDropTables.Destructible;
            string fallbackId = !string.IsNullOrWhiteSpace(gameplayId) ? gameplayId : instance.name;

            if (target != null)
            {
                target.ConfigureFallback(fallbackId, kind, fallbackTable);
                return;
            }

            target = instance.AddComponent<DestructibleDropTarget>();
            target.Configure(
                fallbackId,
                kind,
                fallbackTable,
                settings == null || settings.spawnDropMarkers);
        }

        // 공통 이름·부모·transform과 stable spawn identity를 인스턴스 root에 적용합니다.
        private static void PrepareInstance(GameObject instance, DungeonSpawnRecord record, Transform parent)
        {
            instance.name = ResolveName(record, "DungeonContent");
            instance.transform.SetParent(parent, false);
            instance.transform.localScale = record.localScale;
            instance.transform.localPosition = record.localPosition;
            instance.transform.localRotation = Quaternion.Euler(
                record.pitchDegrees,
                record.yawDegrees,
                record.rollDegrees);
            DungeonSpawnIdentity identity = instance.GetComponent<DungeonSpawnIdentity>();
            if (identity == null) identity = instance.AddComponent<DungeonSpawnIdentity>();
            identity.Configure(record);
        }

        // contentKey가 알려진 built-in인지와 고정 category를 반환합니다.
        private static bool TryGetBuiltInCategory(
            string contentKey,
            out DungeonSpawnCategory category)
        {
            if (contentKey == DungeonBuiltInContentKeys.EntranceMarker ||
                contentKey == DungeonBuiltInContentKeys.ExitMarker)
            {
                category = DungeonSpawnCategory.Marker;
                return true;
            }
            if (contentKey == DungeonBuiltInContentKeys.Gimmick)
            {
                category = DungeonSpawnCategory.Gimmick;
                return true;
            }
            if (contentKey == DungeonBuiltInContentKeys.Enemy)
            {
                category = DungeonSpawnCategory.Enemy;
                return true;
            }
            if (contentKey == DungeonBuiltInContentKeys.Destructible)
            {
                category = DungeonSpawnCategory.Destructible;
                return true;
            }
            if (contentKey == DungeonBuiltInContentKeys.PropCube ||
                contentKey == DungeonBuiltInContentKeys.PropCylinder)
            {
                category = DungeonSpawnCategory.Prop;
                return true;
            }
            category = default(DungeonSpawnCategory);
            return false;
        }

        // spawn category에 대응하는 기존 hierarchy root를 선택합니다.
        private static Transform CategoryRoot(
            DungeonSpawnCategory category,
            Transform markers,
            Transform gimmicks,
            Transform enemies,
            Transform destructibles,
            Transform props)
        {
            switch (category)
            {
                case DungeonSpawnCategory.Marker: return markers;
                case DungeonSpawnCategory.Gimmick: return gimmicks;
                case DungeonSpawnCategory.Enemy: return enemies;
                case DungeonSpawnCategory.Destructible: return destructibles;
                case DungeonSpawnCategory.Prop: return props;
                default: return props;
            }
        }

        // GenerationReport 호환 개수를 실제로 생성된 gameplay category에만 더합니다.
        private static void IncrementCount(ref ContentSpawnCounts counts, DungeonSpawnCategory category)
        {
            if (category == DungeonSpawnCategory.Gimmick) counts.GimmickCount++;
            else if (category == DungeonSpawnCategory.Enemy) counts.EnemyCount++;
            else if (category == DungeonSpawnCategory.Destructible) counts.DestructibleCount++;
            else if (category == DungeonSpawnCategory.Prop) counts.PropCount++;
        }

        // 비어 있지 않은 instanceName을 우선 사용하고 stable ID나 fallback으로 이름을 보완합니다.
        private static string ResolveName(DungeonSpawnRecord record, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(record.instanceName)) return record.instanceName;
            if (!string.IsNullOrWhiteSpace(record.spawnId)) return record.spawnId;
            return fallback;
        }

        // category, instanceName, spawnId 순으로 hierarchy 생성 순서를 고정합니다.
        private static int CompareRecords(DungeonSpawnRecord left, DungeonSpawnRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = ((int)left.category).CompareTo((int)right.category);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.instanceName, right.instanceName);
            return result != 0 ? result : string.CompareOrdinal(left.spawnId, right.spawnId);
        }

        // primitive collider가 배치나 플레이어 이동을 방해하지 않도록 비활성화합니다.
        private static void DisableCollider(GameObject instance)
        {
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        // 이름이 지정된 빈 자식 root를 로컬 원점에 생성합니다.
        private static Transform NewRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root.transform;
        }
    }
}
