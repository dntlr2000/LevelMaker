using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonBuiltInContentKeys
    {
        public const string LegacyCatalogPlanningHash = "builtin-legacy-v1";
        public const string StableCatalogPlanningHash = "builtin-stable-v2";
        public const string EntranceMarker = "builtin/marker/entrance";
        public const string ExitMarker = "builtin/marker/exit";
        public const string Gimmick = "builtin/gimmick";
        public const string Enemy = "builtin/enemy";
        public const string Destructible = "builtin/destructible";
        public const string PropCube = "builtin/prop/cube";
        public const string PropCylinder = "builtin/prop/cylinder";
    }

    public sealed class DungeonContentPlan
    {
        public List<DungeonSpawnRecord> Spawns { get; private set; }
        public ContentSpawnCounts Counts { get; private set; }

        // 계획된 spawn 레코드와 기존 GenerationReport용 개수를 함께 보관합니다.
        public DungeonContentPlan(List<DungeonSpawnRecord> spawns, ContentSpawnCounts counts)
        {
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
            Counts = counts;
        }
    }

    public static class DungeonContentPlanner
    {
        // LegacyV1 난수 호출 순서를 보존하면서 콘텐츠를 GameObject 없는 spawn 레코드로 계획합니다.
        public static DungeonContentPlan Plan(DungeonLayout layout, DungeonRecipeSnapshot recipe, int seed)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            System.Random random = new System.Random(unchecked(seed * 486187739 + 17));
            List<Vector2Int> candidates = new List<Vector2Int>();
            foreach (Vector2Int cell in layout.EnumerateFloorCells())
            {
                if (!IsReserved(cell, layout, recipe.reservedEntranceRadiusCells)) candidates.Add(cell);
            }

            List<DungeonSpawnRecord> spawns = new List<DungeonSpawnRecord>();
            spawns.Add(CreateMarker(layout.Entrance, 0, "Entrance", DungeonBuiltInContentKeys.EntranceMarker, layout, recipe));
            spawns.Add(CreateMarker(layout.Exit, 1, "Exit", DungeonBuiltInContentKeys.ExitMarker, layout, recipe));

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            ContentSpawnCounts counts = new ContentSpawnCounts();
            counts.GimmickCount = PlanGimmicks(spawns, candidates, occupied, layout, recipe, random);
            counts.EnemyCount = PlanProfile(
                spawns,
                candidates,
                occupied,
                layout,
                recipe,
                recipe.enemyProfile,
                random,
                seed + 101,
                delegate(Vector2Int cell, int index) { return CreateEnemy(cell, index, layout, recipe); });
            counts.DestructibleCount = PlanProfile(
                spawns,
                candidates,
                occupied,
                layout,
                recipe,
                recipe.destructibleProfile,
                random,
                seed + 211,
                delegate(Vector2Int cell, int index) { return CreateDestructible(cell, index, layout, recipe, random); });
            counts.PropCount = PlanProfile(
                spawns,
                candidates,
                occupied,
                layout,
                recipe,
                recipe.propProfile,
                random,
                seed + 307,
                delegate(Vector2Int cell, int index) { return CreateProp(cell, index, layout, recipe, random); });
            return new DungeonContentPlan(spawns, counts);
        }

        // 진행도 목표를 따르는 특별 기믹 셀과 기존 회전 난수 소비를 기록합니다.
        private static int PlanGimmicks(
            List<DungeonSpawnRecord> spawns,
            List<Vector2Int> candidates,
            HashSet<Vector2Int> occupied,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            System.Random random)
        {
            int requested = Mathf.Min(recipe.specialGimmickCount, candidates.Count);
            int count = 0;
            for (int i = 0; i < requested; i++)
            {
                float target = (i + 1f) / (requested + 1f);
                float bestScore = float.MaxValue;
                Vector2Int best = default(Vector2Int);
                bool found = false;
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    Vector2Int cell = candidates[candidateIndex];
                    if (!CanOccupy(cell, occupied, recipe.contentSpacingCells)) continue;
                    float score = Mathf.Abs(layout.GetProgression(cell) - target) +
                                  (layout.GetRoomId(cell) >= 0 ? 0f : 0.18f) +
                                  (float)random.NextDouble() * 0.06f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = cell;
                        found = true;
                    }
                }
                if (!found) break;
                occupied.Add(best);
                spawns.Add(CreateGimmick(best, count, layout, recipe, random));
                count++;
            }
            return count;
        }

        // 한 밀도 프로필의 shuffle·확률·변형 난수를 기존 순서로 소비해 spawn 레코드를 추가합니다.
        private static int PlanProfile(
            List<DungeonSpawnRecord> spawns,
            List<Vector2Int> candidates,
            HashSet<Vector2Int> occupied,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            DungeonDensityProfileSnapshot profile,
            System.Random random,
            int noiseSeed,
            Func<Vector2Int, int, DungeonSpawnRecord> create)
        {
            if (profile == null) return 0;
            AnimationCurve curve = profile.overProgression != null
                ? profile.overProgression.ToAnimationCurve()
                : AnimationCurve.Linear(0f, 1f, 1f, 1f);
            List<Vector2Int> shuffled = new List<Vector2Int>(candidates);
            Shuffle(shuffled, random);
            int count = 0;
            for (int i = 0; i < shuffled.Count; i++)
            {
                if (profile.maxCount > 0 && count >= profile.maxCount) break;
                Vector2Int cell = shuffled[i];
                if (!CanOccupy(cell, occupied, recipe.contentSpacingCells)) continue;
                float noise = Mathf.PerlinNoise(
                    (cell.x + noiseSeed * 0.013f) * 0.17f,
                    (cell.y - noiseSeed * 0.019f) * 0.17f);
                float probability = EvaluateProbability(profile, curve, layout.GetProgression(cell), layout.GetRoomId(cell) >= 0, noise);
                if (random.NextDouble() > probability) continue;
                occupied.Add(cell);
                spawns.Add(create(cell, count));
                count++;
            }
            return count;
        }

        // 정규화된 프로필에서 기존 DensityProfile과 같은 배치 확률을 계산합니다.
        private static float EvaluateProbability(
            DungeonDensityProfileSnapshot profile,
            AnimationCurve curve,
            float progression,
            bool isInsideRoom,
            float clusterNoise)
        {
            float curveMultiplier = Mathf.Max(0f, curve.Evaluate(Mathf.Clamp01(progression)));
            float roomMultiplier = isInsideRoom
                ? Mathf.Lerp(1f, 1.45f, profile.roomBias)
                : Mathf.Lerp(1f, 0.55f, profile.roomBias);
            float clumpValue = Mathf.Lerp(0.2f, 1.8f, Mathf.Clamp01(clusterNoise));
            float clusterMultiplier = Mathf.Lerp(1f, clumpValue, profile.clustering);
            return Mathf.Clamp01(profile.baseDensity * curveMultiplier * roomMultiplier * clusterMultiplier);
        }

        // 입구·출구 반경 안의 셀을 기존 배치 후보 규칙대로 제외합니다.
        private static bool IsReserved(Vector2Int cell, DungeonLayout layout, int entranceRadius)
        {
            int entranceDistance = Mathf.Abs(cell.x - layout.Entrance.x) + Mathf.Abs(cell.y - layout.Entrance.y);
            int exitDistance = Mathf.Abs(cell.x - layout.Exit.x) + Mathf.Abs(cell.y - layout.Exit.y);
            return entranceDistance <= entranceRadius || exitDistance <= 1;
        }

        // 이미 선택한 콘텐츠와 Chebyshev 간격 제약을 만족하는지 확인합니다.
        private static bool CanOccupy(Vector2Int cell, HashSet<Vector2Int> occupied, int spacing)
        {
            if (occupied.Contains(cell)) return false;
            for (int x = -spacing; x <= spacing; x++)
            for (int z = -spacing; z <= spacing; z++)
            {
                if (occupied.Contains(new Vector2Int(cell.x + x, cell.y + z))) return false;
            }
            return true;
        }

        // 입구 또는 출구 marker 레코드를 고정 크기와 높이로 만듭니다.
        private static DungeonSpawnRecord CreateMarker(
            Vector2Int cell,
            int index,
            string instanceName,
            string contentKey,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe)
        {
            return CreateRecord(
                DungeonSpawnCategory.Marker,
                cell,
                index,
                instanceName,
                contentKey,
                layout,
                CellPosition(layout, recipe, cell) + Vector3.up * 0.08f,
                new Vector3(1.15f, 0.08f, 1.15f),
                0f,
                0f,
                0f);
        }

        // 특별 기믹의 root 위치와 legacy yaw 난수를 기록합니다.
        private static DungeonSpawnRecord CreateGimmick(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            System.Random random)
        {
            return CreateRecord(
                DungeonSpawnCategory.Gimmick,
                cell,
                index,
                string.Format(CultureInfo.InvariantCulture, "SpecialGimmick_{0:00}", index),
                DungeonBuiltInContentKeys.Gimmick,
                layout,
                CellPosition(layout, recipe, cell),
                Vector3.one,
                0f,
                (float)random.NextDouble() * 360f,
                0f);
        }

        // 적 캡슐의 legacy 이름, 크기와 바닥 기준 위치를 기록합니다.
        private static DungeonSpawnRecord CreateEnemy(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe)
        {
            float scale = Mathf.Clamp(recipe.cellSize * 0.32f, 0.65f, 1.25f);
            return CreateRecord(
                DungeonSpawnCategory.Enemy,
                cell,
                index,
                string.Format(CultureInfo.InvariantCulture, "Enemy_{0:000}", index),
                DungeonBuiltInContentKeys.Enemy,
                layout,
                CellPosition(layout, recipe, cell) + Vector3.up * scale,
                Vector3.one * scale,
                0f,
                0f,
                0f);
        }

        // 파괴물 큐브의 legacy 크기, 위치와 yaw 난수를 기록합니다.
        private static DungeonSpawnRecord CreateDestructible(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            System.Random random)
        {
            Vector3 scale = new Vector3(
                Mathf.Clamp(recipe.cellSize * 0.32f, 0.65f, 1.3f),
                Mathf.Clamp(recipe.cellSize * 0.4f, 0.75f, 1.5f),
                Mathf.Clamp(recipe.cellSize * 0.32f, 0.65f, 1.3f));
            return CreateRecord(
                DungeonSpawnCategory.Destructible,
                cell,
                index,
                string.Format(CultureInfo.InvariantCulture, "Breakable_{0:000}", index),
                DungeonBuiltInContentKeys.Destructible,
                layout,
                CellPosition(layout, recipe, cell) + Vector3.up * (scale.y * 0.5f),
                scale,
                0f,
                (float)random.NextDouble() * 360f,
                0f);
        }

        // 지형지물 primitive 종류, 크기, 위치와 세 축 회전 난수를 legacy 순서로 기록합니다.
        private static DungeonSpawnRecord CreateProp(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            System.Random random)
        {
            bool cylinder = random.NextDouble() < 0.5;
            float baseSize = Mathf.Clamp(recipe.cellSize * (0.14f + (float)random.NextDouble() * 0.12f), 0.3f, 1.2f);
            float height = baseSize * (0.8f + (float)random.NextDouble() * 1.2f);
            float pitch = (float)random.NextDouble() * 8f;
            float yaw = (float)random.NextDouble() * 360f;
            float roll = (float)random.NextDouble() * 8f;
            return CreateRecord(
                DungeonSpawnCategory.Prop,
                cell,
                index,
                string.Format(CultureInfo.InvariantCulture, "TerrainProp_{0:000}", index),
                cylinder ? DungeonBuiltInContentKeys.PropCylinder : DungeonBuiltInContentKeys.PropCube,
                layout,
                CellPosition(layout, recipe, cell) + Vector3.up * (cylinder ? height : height * 0.5f),
                new Vector3(baseSize, height, baseSize),
                pitch,
                yaw,
                roll);
        }

        // 공통 stable ID, 방, 진행도와 transform 값을 하나의 spawn 레코드로 묶습니다.
        private static DungeonSpawnRecord CreateRecord(
            DungeonSpawnCategory category,
            Vector2Int cell,
            int index,
            string instanceName,
            string contentKey,
            DungeonLayout layout,
            Vector3 localPosition,
            Vector3 localScale,
            float pitch,
            float yaw,
            float roll)
        {
            int roomIndex = layout.GetRoomId(cell);
            return new DungeonSpawnRecord
            {
                spawnId = DungeonBlueprintIds.LegacySpawnId(category, cell, index),
                category = category,
                contentKey = contentKey,
                instanceName = instanceName,
                cell = cell,
                localPosition = localPosition,
                pitchDegrees = pitch,
                yawDegrees = yaw,
                rollDegrees = roll,
                localScale = localScale,
                roomId = roomIndex >= 0 ? DungeonBlueprintIds.LegacyRoomId(roomIndex) : string.Empty,
                progression = layout.GetProgression(cell),
                variantSeed = 0
            };
        }

        // legacy DungeonLayout의 셀을 레시피 cellSize 기준 로컬 위치로 변환합니다.
        private static Vector3 CellPosition(DungeonLayout layout, DungeonRecipeSnapshot recipe, Vector2Int cell)
        {
            return layout.CellToLocalPosition(cell, recipe.cellSize);
        }

        // System.Random을 사용하는 기존 Fisher-Yates 순서로 후보를 섞습니다.
        private static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int selected = random.Next(0, i + 1);
                T value = list[i];
                list[i] = list[selected];
                list[selected] = value;
            }
        }

        // StableV2의 범주별 stream, canonical catalog 후보와 고정 footprint 규칙으로 콘텐츠를 계획합니다.
        public static DungeonContentPlan PlanStableV2(
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            int seed,
            DungeonContentCatalogPlanningSnapshot catalogSnapshot)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (catalogSnapshot != null && catalogSnapshot.formatVersion != DungeonContentCatalog.CurrentFormatVersion)
                throw new NotSupportedException("Unsupported Content Catalog planning snapshot format: " + catalogSnapshot.formatVersion);

            List<Vector2Int> candidates = new List<Vector2Int>();
            foreach (Vector2Int cell in layout.EnumerateFloorCells())
            {
                if (!IsReserved(cell, layout, recipe.reservedEntranceRadiusCells)) candidates.Add(cell);
            }
            candidates.Sort(CompareCellsStableV2);
            List<DungeonContentPlanningEntry> entries = catalogSnapshot != null
                ? DungeonContentCatalogHasher.GetCanonicalEntries(catalogSnapshot.entries)
                : new List<DungeonContentPlanningEntry>();

            List<DungeonSpawnRecord> spawns = new List<DungeonSpawnRecord>();
            spawns.Add(CreateStableMarker(layout.Entrance, 0, "Entrance", DungeonBuiltInContentKeys.EntranceMarker, layout, recipe));
            spawns.Add(CreateStableMarker(layout.Exit, 1, "Exit", DungeonBuiltInContentKeys.ExitMarker, layout, recipe));

            List<StablePlacedFootprint> occupied = new List<StablePlacedFootprint>();
            ContentSpawnCounts counts = new ContentSpawnCounts();
            counts.GimmickCount = PlanStableGimmicks(spawns, candidates, occupied, entries, layout, recipe, seed);
            counts.EnemyCount = PlanStableProfile(
                spawns, candidates, occupied, entries, layout, recipe, recipe.enemyProfile,
                seed, DungeonSpawnCategory.Enemy, DungeonStableRandomStreams.Enemy);
            counts.DestructibleCount = PlanStableProfile(
                spawns, candidates, occupied, entries, layout, recipe, recipe.destructibleProfile,
                seed, DungeonSpawnCategory.Destructible, DungeonStableRandomStreams.Destructible);
            counts.PropCount = PlanStableProfile(
                spawns, candidates, occupied, entries, layout, recipe, recipe.propProfile,
                seed, DungeonSpawnCategory.Prop, DungeonStableRandomStreams.Prop);
            return new DungeonContentPlan(spawns, counts);
        }

        // 고정 우선순위의 첫 gameplay 범주인 기믹을 전용 stream으로 선택합니다.
        private static int PlanStableGimmicks(
            List<DungeonSpawnRecord> spawns,
            List<Vector2Int> candidates,
            List<StablePlacedFootprint> occupied,
            List<DungeonContentPlanningEntry> entries,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            int seed)
        {
            DungeonStableRandom placement = DungeonStableRandomStreams.Create(seed, DungeonStableRandomStreams.Gimmick);
            int requested = Mathf.Min(recipe.specialGimmickCount, candidates.Count);
            int count = 0;
            for (int i = 0; i < requested; i++)
            {
                float target = (i + 1f) / (requested + 1f);
                float bestScore = float.MaxValue;
                Vector2Int bestCell = default(Vector2Int);
                StableCatalogSelection bestSelection = null;
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    Vector2Int cell = candidates[candidateIndex];
                    StableCatalogSelection selection = SelectStableCatalogEntry(
                        entries, DungeonSpawnCategory.Gimmick, layout, cell, count, seed);
                    if (!CanOccupyStable(cell, selection.Footprint, selection.MinimumSpacing, occupied, layout, recipe)) continue;
                    float score = Mathf.Abs(layout.GetProgression(cell) - target) +
                                  (layout.GetRoomId(cell) >= 0 ? 0f : 0.18f) +
                                  placement.NextFloat01() * 0.06f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCell = cell;
                        bestSelection = selection;
                    }
                }
                if (bestSelection == null) break;
                AddStableFootprint(bestCell, bestSelection.Footprint, bestSelection.MinimumSpacing, occupied, recipe);
                spawns.Add(CreateStableGimmick(bestCell, count, layout, recipe, bestSelection));
                count++;
            }
            return count;
        }

        // Enemy·Destructible·Prop가 서로의 난수 호출 수를 공유하지 않는 고정 stream 배치입니다.
        private static int PlanStableProfile(
            List<DungeonSpawnRecord> spawns,
            List<Vector2Int> candidates,
            List<StablePlacedFootprint> occupied,
            List<DungeonContentPlanningEntry> entries,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            DungeonDensityProfileSnapshot profile,
            int seed,
            DungeonSpawnCategory category,
            ulong streamId)
        {
            if (profile == null) return 0;
            AnimationCurve curve = profile.overProgression != null
                ? profile.overProgression.ToAnimationCurve()
                : AnimationCurve.Linear(0f, 1f, 1f, 1f);
            DungeonStableRandom placement = DungeonStableRandomStreams.Create(seed, streamId);
            List<Vector2Int> shuffled = new List<Vector2Int>(candidates);
            ShuffleStable(shuffled, placement);
            int count = 0;
            for (int i = 0; i < shuffled.Count; i++)
            {
                if (profile.maxCount > 0 && count >= profile.maxCount) break;
                Vector2Int cell = shuffled[i];
                StableCatalogSelection selection = SelectStableCatalogEntry(entries, category, layout, cell, count, seed);
                if (!CanOccupyStable(cell, selection.Footprint, selection.MinimumSpacing, occupied, layout, recipe)) continue;
                DungeonStableRandom noise = DungeonStableRandomStreams.CreateChild(
                    seed, streamId, (int)category, cell.x, cell.y, -1);
                float probability = EvaluateProbability(
                    profile,
                    curve,
                    layout.GetProgression(cell),
                    layout.GetRoomId(cell) >= 0,
                    noise.NextFloat01());
                if (placement.NextFloat01() > probability) continue;

                AddStableFootprint(cell, selection.Footprint, selection.MinimumSpacing, occupied, recipe);
                if (category == DungeonSpawnCategory.Enemy)
                    spawns.Add(CreateStableEnemy(cell, count, layout, recipe, selection));
                else if (category == DungeonSpawnCategory.Destructible)
                    spawns.Add(CreateStableDestructible(cell, count, layout, recipe, selection));
                else
                    spawns.Add(CreateStableProp(cell, count, layout, recipe, selection));
                count++;
            }
            return count;
        }

        // 한 spawn의 key와 변형을 cell 기반 child stream으로 선택해 전역 호출 순서 의존성을 제거합니다.
        private static StableCatalogSelection SelectStableCatalogEntry(
            List<DungeonContentPlanningEntry> entries,
            DungeonSpawnCategory category,
            DungeonLayout layout,
            Vector2Int cell,
            int categoryIndex,
            int seed)
        {
            DungeonStableRandom variant = DungeonStableRandomStreams.CreateChild(
                seed,
                DungeonStableRandomStreams.Variant,
                (int)category,
                cell.x,
                cell.y,
                categoryIndex);
            int variantSeed = unchecked((int)variant.NextUInt());
            List<DungeonContentPlanningEntry> eligible = new List<DungeonContentPlanningEntry>();
            double totalWeight = 0.0;
            bool inRoom = layout.GetRoomId(cell) >= 0;
            float progression = layout.GetProgression(cell);
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonContentPlanningEntry entry = entries[i];
                if (entry == null || entry.category != category || entry.weight <= 0f ||
                    float.IsNaN(entry.weight) || float.IsInfinity(entry.weight) ||
                    progression < entry.minProgression || progression > entry.maxProgression ||
                    (entry.placement == DungeonContentPlacement.RoomOnly && !inRoom) ||
                    (entry.placement == DungeonContentPlacement.CorridorOnly && inRoom) ||
                    (entry.requiredRoomTags != null && entry.requiredRoomTags.Count > 0) ||
                    entry.footprintCells.x < 1 || entry.footprintCells.y < 1)
                    continue;
                eligible.Add(entry);
                totalWeight += entry.weight;
            }

            DungeonContentPlanningEntry selected = null;
            if (eligible.Count > 0 && totalWeight > 0.0)
            {
                double target = variant.NextDouble01() * totalWeight;
                for (int i = 0; i < eligible.Count; i++)
                {
                    selected = eligible[i];
                    target -= selected.weight;
                    if (target < 0.0) break;
                }
            }
            return new StableCatalogSelection(selected, variantSeed, variant);
        }

        private static DungeonSpawnRecord CreateStableMarker(
            Vector2Int cell,
            int index,
            string instanceName,
            string contentKey,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe)
        {
            return CreateStableRecord(
                DungeonSpawnCategory.Marker, cell, index, instanceName, contentKey, layout,
                CellPosition(layout, recipe, cell) + Vector3.up * 0.08f,
                new Vector3(1.15f, 0.08f, 1.15f), 0f, 0f, 0f, 0);
        }

        // custom Prefab은 원본 transform을 기준으로 하고 built-in 기믹만 기본 임의 회전을 사용합니다.
        private static DungeonSpawnRecord CreateStableGimmick(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            StableCatalogSelection selection)
        {
            float yaw = selection.Entry == null ? selection.Random.NextFloat01() * 360f : 0f;
            return CreateStableVariedRecord(
                DungeonSpawnCategory.Gimmick, cell, index,
                string.Format(CultureInfo.InvariantCulture, "SpecialGimmick_{0:00}", index),
                DungeonBuiltInContentKeys.Gimmick, layout, CellPosition(layout, recipe, cell),
                Vector3.one, 0f, yaw, 0f, selection);
        }

        // custom 적은 셀 원점·단위 크기에서 catalog 변형만 적용하고 built-in 캡슐은 기존 크기를 유지합니다.
        private static DungeonSpawnRecord CreateStableEnemy(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            StableCatalogSelection selection)
        {
            bool custom = selection.Entry != null;
            float scale = custom ? 1f : Mathf.Clamp(recipe.cellSize * 0.32f, 0.65f, 1.25f);
            return CreateStableVariedRecord(
                DungeonSpawnCategory.Enemy, cell, index,
                string.Format(CultureInfo.InvariantCulture, "Enemy_{0:000}", index),
                DungeonBuiltInContentKeys.Enemy, layout,
                CellPosition(layout, recipe, cell) + (custom ? Vector3.zero : Vector3.up * scale),
                Vector3.one * scale, 0f, 0f, 0f, selection);
        }

        // custom 파괴물은 authored Prefab transform을 보존하고 built-in 큐브에만 기본 크기·회전을 계산합니다.
        private static DungeonSpawnRecord CreateStableDestructible(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            StableCatalogSelection selection)
        {
            bool custom = selection.Entry != null;
            Vector3 scale = custom
                ? Vector3.one
                : new Vector3(
                    Mathf.Clamp(recipe.cellSize * 0.32f, 0.65f, 1.3f),
                    Mathf.Clamp(recipe.cellSize * 0.4f, 0.75f, 1.5f),
                    Mathf.Clamp(recipe.cellSize * 0.32f, 0.65f, 1.3f));
            float yaw = custom ? 0f : selection.Random.NextFloat01() * 360f;
            return CreateStableVariedRecord(
                DungeonSpawnCategory.Destructible, cell, index,
                string.Format(CultureInfo.InvariantCulture, "Breakable_{0:000}", index),
                DungeonBuiltInContentKeys.Destructible, layout,
                CellPosition(layout, recipe, cell) + (custom ? Vector3.zero : Vector3.up * (scale.y * 0.5f)),
                scale, 0f, yaw, 0f, selection);
        }

        // custom 지형지물은 built-in 형상 난수를 소비하지 않고 catalog 변형만 적용합니다.
        private static DungeonSpawnRecord CreateStableProp(
            Vector2Int cell,
            int index,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe,
            StableCatalogSelection selection)
        {
            if (selection.Entry != null)
            {
                return CreateStableVariedRecord(
                    DungeonSpawnCategory.Prop, cell, index,
                    string.Format(CultureInfo.InvariantCulture, "TerrainProp_{0:000}", index),
                    DungeonBuiltInContentKeys.PropCube,
                    layout,
                    CellPosition(layout, recipe, cell),
                    Vector3.one,
                    0f,
                    0f,
                    0f,
                    selection);
            }

            bool cylinder = selection.Random.NextFloat01() < 0.5f;
            float baseSize = Mathf.Clamp(recipe.cellSize * (0.14f + selection.Random.NextFloat01() * 0.12f), 0.3f, 1.2f);
            float height = baseSize * (0.8f + selection.Random.NextFloat01() * 1.2f);
            return CreateStableVariedRecord(
                DungeonSpawnCategory.Prop, cell, index,
                string.Format(CultureInfo.InvariantCulture, "TerrainProp_{0:000}", index),
                cylinder ? DungeonBuiltInContentKeys.PropCylinder : DungeonBuiltInContentKeys.PropCube,
                layout,
                CellPosition(layout, recipe, cell) + Vector3.up * (cylinder ? height : height * 0.5f),
                new Vector3(baseSize, height, baseSize),
                selection.Random.NextFloat01() * 8f,
                selection.Random.NextFloat01() * 360f,
                selection.Random.NextFloat01() * 8f,
                selection);
        }

        // 선택된 catalog entry의 절대 yaw 범위와 균일 scale 배율을 spawn 레코드에 적용합니다.
        private static DungeonSpawnRecord CreateStableVariedRecord(
            DungeonSpawnCategory category,
            Vector2Int cell,
            int index,
            string instanceName,
            string builtInKey,
            DungeonLayout layout,
            Vector3 localPosition,
            Vector3 localScale,
            float pitch,
            float yaw,
            float roll,
            StableCatalogSelection selection)
        {
            DungeonContentPlanningEntry entry = selection.Entry;
            string key = entry != null ? entry.contentKey : builtInKey;
            if (entry != null)
            {
                if (entry.randomizeYaw)
                    yaw = Mathf.Lerp(entry.yawDegreesRange.x, entry.yawDegreesRange.y, selection.Random.NextFloat01());
                float multiplier = Mathf.Lerp(
                    entry.uniformScaleRange.x,
                    entry.uniformScaleRange.y,
                    selection.Random.NextFloat01());
                localScale *= multiplier;
            }
            return CreateStableRecord(
                category, cell, index, instanceName, key, layout, localPosition, localScale,
                pitch, yaw, roll, selection.VariantSeed);
        }

        private static DungeonSpawnRecord CreateStableRecord(
            DungeonSpawnCategory category,
            Vector2Int cell,
            int index,
            string instanceName,
            string contentKey,
            DungeonLayout layout,
            Vector3 localPosition,
            Vector3 localScale,
            float pitch,
            float yaw,
            float roll,
            int variantSeed)
        {
            int roomIndex = layout.GetRoomId(cell);
            return new DungeonSpawnRecord
            {
                spawnId = DungeonBlueprintIds.StableSpawnId(category, cell, index),
                category = category,
                contentKey = contentKey,
                instanceName = instanceName,
                cell = cell,
                localPosition = localPosition,
                pitchDegrees = pitch,
                yawDegrees = yaw,
                rollDegrees = roll,
                localScale = localScale,
                roomId = roomIndex >= 0 ? DungeonBlueprintIds.StableRoomId(roomIndex) : string.Empty,
                progression = layout.GetProgression(cell),
                variantSeed = variantSeed
            };
        }

        private static bool CanOccupyStable(
            Vector2Int anchor,
            Vector2Int footprint,
            int entrySpacing,
            List<StablePlacedFootprint> occupied,
            DungeonLayout layout,
            DungeonRecipeSnapshot recipe)
        {
            List<Vector2Int> cells = DungeonContentCatalogValidator.FootprintCells(anchor, footprint);
            for (int i = 0; i < cells.Count; i++)
            {
                if (!layout.IsFloor(cells[i]) || IsReserved(cells[i], layout, recipe.reservedEntranceRadiusCells)) return false;
            }
            int spacing = Mathf.Max(recipe.contentSpacingCells, Mathf.Max(0, entrySpacing));
            for (int previousIndex = 0; previousIndex < occupied.Count; previousIndex++)
            {
                StablePlacedFootprint previous = occupied[previousIndex];
                int required = Mathf.Max(spacing, previous.Spacing);
                for (int i = 0; i < cells.Count; i++)
                for (int j = 0; j < previous.Cells.Count; j++)
                {
                    int dx = Mathf.Abs(cells[i].x - previous.Cells[j].x);
                    int dz = Mathf.Abs(cells[i].y - previous.Cells[j].y);
                    if (Mathf.Max(dx, dz) <= required) return false;
                }
            }
            return true;
        }

        private static void AddStableFootprint(
            Vector2Int anchor,
            Vector2Int footprint,
            int entrySpacing,
            List<StablePlacedFootprint> occupied,
            DungeonRecipeSnapshot recipe)
        {
            occupied.Add(new StablePlacedFootprint(
                DungeonContentCatalogValidator.FootprintCells(anchor, footprint),
                Mathf.Max(recipe.contentSpacingCells, Mathf.Max(0, entrySpacing))));
        }

        private static void ShuffleStable<T>(IList<T> list, DungeonStableRandom random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int selected = random.NextInt(0, i + 1);
                T value = list[i];
                list[i] = list[selected];
                list[selected] = value;
            }
        }

        private static int CompareCellsStableV2(Vector2Int left, Vector2Int right)
        {
            int result = left.y.CompareTo(right.y);
            return result != 0 ? result : left.x.CompareTo(right.x);
        }

        private sealed class StableCatalogSelection
        {
            public readonly DungeonContentPlanningEntry Entry;
            public readonly int VariantSeed;
            public readonly DungeonStableRandom Random;
            public Vector2Int Footprint { get { return Entry != null ? Entry.footprintCells : Vector2Int.one; } }
            public int MinimumSpacing { get { return Entry != null ? Entry.minimumSpacingCells : 0; } }

            public StableCatalogSelection(
                DungeonContentPlanningEntry entry,
                int variantSeed,
                DungeonStableRandom random)
            {
                Entry = entry;
                VariantSeed = variantSeed;
                Random = random;
            }
        }

        private sealed class StablePlacedFootprint
        {
            public readonly List<Vector2Int> Cells;
            public readonly int Spacing;

            public StablePlacedFootprint(List<Vector2Int> cells, int spacing)
            {
                Cells = cells;
                Spacing = spacing;
            }
        }
    }
}
