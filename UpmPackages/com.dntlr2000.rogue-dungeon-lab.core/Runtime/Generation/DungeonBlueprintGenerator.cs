using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RogueDungeonLab
{
    public sealed class DungeonBlueprintGenerationResult
    {
        public DungeonBlueprint Blueprint { get; private set; }
        public DungeonLayout Layout { get; private set; }
        public ContentSpawnCounts ContentCounts { get; private set; }

        // 한 번의 계산에서 나온 Blueprint, legacy layout과 콘텐츠 개수를 함께 보관합니다.
        public DungeonBlueprintGenerationResult(
            DungeonBlueprint blueprint,
            DungeonLayout layout,
            ContentSpawnCounts contentCounts)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            ContentCounts = contentCounts;
        }
    }

    public static class DungeonBlueprintGenerator
    {
        // 버전이 지정된 생성 요청을 GameObject 없는 layout과 Blueprint 결과로 계산합니다.
        public static DungeonBlueprintGenerationResult Generate(DungeonGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.recipeSnapshot == null) throw new ArgumentException("Generation request has no recipe snapshot.", nameof(request));
            if (request.recipeSnapshot.formatVersion != DungeonRecipeSnapshot.CurrentFormatVersion)
            {
                throw new NotSupportedException("Unsupported recipe snapshot format: " + request.recipeSnapshot.formatVersion);
            }
            if (!DungeonGeneratorVersions.IsSupported(request.generatorVersion))
            {
                throw new NotSupportedException("Unsupported generator version: " + request.generatorVersion);
            }

            DungeonLayout layout;
            DungeonContentPlan contentPlan;
            if (request.generatorVersion == DungeonGeneratorVersions.StableV2)
            {
                DungeonValidationReport catalogValidation = request.contentCatalogSnapshot != null
                    ? DungeonContentCatalogValidator.ValidatePlanningSnapshot(request.contentCatalogSnapshot)
                    : new DungeonValidationReport();
                if (!catalogValidation.IsValid)
                {
                    throw new ArgumentException(
                        "Generation request contains an invalid catalog planning snapshot " +
                        FormatErrorCodes(catalogValidation) + ".",
                        nameof(request));
                }
                string currentCatalogHash = request.contentCatalogSnapshot != null
                    ? request.contentCatalogSnapshot.ComputeHash()
                    : DungeonBuiltInContentKeys.StableCatalogPlanningHash;
                if (!string.Equals(
                        currentCatalogHash,
                        request.catalogPlanningHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "Generation request catalog snapshot no longer matches its captured planning hash.",
                        nameof(request));
                }

                layout = DungeonLayoutGenerator.GenerateStableV2(request.recipeSnapshot, request.seed);
                contentPlan = DungeonContentPlanner.PlanStableV2(
                    layout,
                    request.recipeSnapshot,
                    request.seed,
                    request.contentCatalogSnapshot);
            }
            else
            {
                layout = DungeonLayoutGenerator.Generate(request.recipeSnapshot, request.seed);
                contentPlan = DungeonContentPlanner.Plan(layout, request.recipeSnapshot, request.seed);
            }
            DungeonBlueprint blueprint = CreateBlueprint(request, layout, contentPlan.Spawns);
            return new DungeonBlueprintGenerationResult(blueprint, layout, contentPlan.Counts);
        }

        // 검증 리포트의 오류 코드를 결정적 입력 순서로 예외 메시지에 요약합니다.
        private static string FormatErrorCodes(DungeonValidationReport report)
        {
            List<string> codes = new List<string>();
            if (report != null && report.issues != null)
            {
                for (int i = 0; i < report.issues.Count; i++)
                {
                    DungeonValidationIssue issue = report.issues[i];
                    if (issue != null && issue.severity == DungeonValidationSeverity.Error)
                        codes.Add(issue.code);
                }
            }
            return codes.Count > 0 ? "[" + string.Join(",", codes) + "]" : "[]";
        }

        // legacy layout과 계획된 spawn 레코드를 버전 1 Blueprint로 복사합니다.
        private static DungeonBlueprint CreateBlueprint(
            DungeonGenerationRequest request,
            DungeonLayout layout,
            List<DungeonSpawnRecord> spawns)
        {
            DungeonRecipeSnapshot recipe = request.recipeSnapshot;
            DungeonBlueprint blueprint = new DungeonBlueprint
            {
                formatVersion = DungeonBlueprintFormat.CurrentVersion,
                generatorVersion = request.generatorVersion,
                seed = request.seed,
                recipeHash = request.RecipeHash,
                catalogPlanningHash = request.catalogPlanningHash ?? string.Empty,
                grid = new DungeonGridRecord
                {
                    width = layout.Width,
                    depth = layout.Depth,
                    cellSize = recipe.cellSize,
                    wallHeight = recipe.wallHeight
                },
                entrance = layout.Entrance,
                exit = layout.Exit
            };

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                blueprint.rooms.Add(new DungeonRoomRecord
                {
                    roomId = request.generatorVersion == DungeonGeneratorVersions.StableV2
                        ? DungeonBlueprintIds.StableRoomId(i)
                        : DungeonBlueprintIds.LegacyRoomId(i),
                    bounds = layout.Rooms[i]
                });
            }

            foreach (Vector2Int cell in layout.EnumerateFloorCells())
            {
                int roomIndex = layout.GetRoomId(cell);
                blueprint.cells.Add(new DungeonCellRecord
                {
                    coordinate = cell,
                    flags = DungeonCellFlags.Floor,
                    roomId = roomIndex >= 0
                        ? request.generatorVersion == DungeonGeneratorVersions.StableV2
                            ? DungeonBlueprintIds.StableRoomId(roomIndex)
                            : DungeonBlueprintIds.LegacyRoomId(roomIndex)
                        : string.Empty,
                    distanceFromEntrance = layout.GetDistance(cell)
                });
            }

            if (spawns != null) blueprint.spawns.AddRange(spawns);
            blueprint.RefreshHash();
            return blueprint;
        }
    }

    public static class DungeonBlueprintLayoutConverter
    {
        // Blueprint의 논리 셀과 방을 기존 소비 코드용 DungeonLayout projection으로 복원합니다.
        public static DungeonLayout ToLayout(DungeonBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (blueprint.grid == null) throw new ArgumentException("Blueprint grid is missing.", nameof(blueprint));

            DungeonLayout layout = new DungeonLayout(blueprint.grid.width, blueprint.grid.depth);
            Dictionary<string, int> roomIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            List<DungeonRoomRecord> rooms = blueprint.rooms ?? new List<DungeonRoomRecord>();
            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoomRecord room = rooms[i];
                if (room == null) continue;
                int roomIndex = layout.Rooms.Count;
                layout.AddRoom(room.bounds);
                if (!string.IsNullOrEmpty(room.roomId) && !roomIndices.ContainsKey(room.roomId))
                {
                    roomIndices.Add(room.roomId, roomIndex);
                }
            }

            List<DungeonCellRecord> cells = blueprint.cells ?? new List<DungeonCellRecord>();
            for (int i = 0; i < cells.Count; i++)
            {
                DungeonCellRecord cell = cells[i];
                if (cell == null || (cell.flags & DungeonCellFlags.Floor) == 0 || !layout.InBounds(cell.coordinate)) continue;
                int roomIndex = -1;
                int mappedRoomIndex;
                if (!string.IsNullOrEmpty(cell.roomId) && roomIndices.TryGetValue(cell.roomId, out mappedRoomIndex))
                {
                    roomIndex = mappedRoomIndex;
                }
                layout.SetFloor(cell.coordinate, roomIndex, false);
                layout.SetDistance(cell.coordinate, cell.distanceFromEntrance);
            }

            layout.Entrance = blueprint.entrance;
            layout.Exit = blueprint.exit;
            layout.MaxDistance = Mathf.Max(1, layout.GetDistance(layout.Exit));
            return layout;
        }
    }

    internal static class DungeonBlueprintIds
    {
        // legacy 정수 방 인덱스를 Blueprint stable room ID로 변환합니다.
        public static string LegacyRoomId(int roomIndex)
        {
            return "legacy-v1:room:" + roomIndex.ToString(CultureInfo.InvariantCulture);
        }

        // 범주·셀·범주 내 순번으로 LegacyV1 stable spawn ID를 만듭니다.
        public static string LegacySpawnId(DungeonSpawnCategory category, Vector2Int cell, int index)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "legacy-v1:{0}:{1}:{2}:{3}",
                CategoryKey(category),
                cell.x,
                cell.y,
                index);
        }

        // StableV2 정수 방 인덱스를 버전 namespace가 포함된 stable ID로 변환합니다.
        public static string StableRoomId(int roomIndex)
        {
            return "stable-v2:room:" + roomIndex.ToString(CultureInfo.InvariantCulture);
        }

        // StableV2 범주·셀·범주 내 순번으로 다른 버전과 충돌하지 않는 spawn ID를 만듭니다.
        public static string StableSpawnId(DungeonSpawnCategory category, Vector2Int cell, int index)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stable-v2:{0}:{1}:{2}:{3}",
                CategoryKey(category),
                cell.x,
                cell.y,
                index);
        }

        // enum 이름 변경의 영향을 받지 않는 고정 spawn 범주 키를 반환합니다.
        private static string CategoryKey(DungeonSpawnCategory category)
        {
            switch (category)
            {
                case DungeonSpawnCategory.Marker: return "marker";
                case DungeonSpawnCategory.Gimmick: return "gimmick";
                case DungeonSpawnCategory.Enemy: return "enemy";
                case DungeonSpawnCategory.Destructible: return "destructible";
                case DungeonSpawnCategory.Prop: return "prop";
                default: return "unknown";
            }
        }
    }
}
