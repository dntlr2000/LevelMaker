using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public enum DungeonValidationSeverity
    {
        Warning,
        Error
    }

    public static class DungeonBlueprintValidationCodes
    {
        public const string NullBlueprint = "RDL-BP-001";
        public const string UnsupportedFormat = "RDL-BP-002";
        public const string InvalidGeneratorVersion = "RDL-BP-003";
        public const string MissingGrid = "RDL-BP-004";
        public const string InvalidGrid = "RDL-BP-005";
        public const string MissingCells = "RDL-BP-006";
        public const string NullCell = "RDL-BP-007";
        public const string CellOutOfBounds = "RDL-BP-008";
        public const string DuplicateCell = "RDL-BP-009";
        public const string NoFloorCells = "RDL-BP-010";
        public const string NegativeDistance = "RDL-BP-011";
        public const string EndpointNotFloor = "RDL-BP-012";
        public const string DisconnectedFloor = "RDL-BP-013";
        public const string DistanceMismatch = "RDL-BP-014";
        public const string MissingRooms = "RDL-BP-015";
        public const string InvalidRoom = "RDL-BP-016";
        public const string DuplicateRoomId = "RDL-BP-017";
        public const string UnknownRoomId = "RDL-BP-018";
        public const string MissingSpawns = "RDL-BP-019";
        public const string InvalidSpawn = "RDL-BP-020";
        public const string DuplicateSpawnId = "RDL-BP-021";
        public const string SpawnNotOnFloor = "RDL-BP-022";
        public const string InvalidSpawnTransform = "RDL-BP-023";
        public const string MissingRecipeHash = "RDL-BP-024";
        public const string MissingBlueprintHash = "RDL-BP-025";
        public const string BlueprintHashMismatch = "RDL-BP-026";
        public const string EntranceEqualsExit = "RDL-BP-027";
        public const string SpawnRoomMismatch = "RDL-BP-028";
    }

    [Serializable]
    public sealed class DungeonValidationIssue
    {
        public string code = string.Empty;
        public DungeonValidationSeverity severity;
        public string message = string.Empty;
        public bool hasCell;
        public Vector2Int cell;
        public string spawnId = string.Empty;
    }

    [Serializable]
    public sealed class DungeonValidationReport
    {
        public List<DungeonValidationIssue> issues = new List<DungeonValidationIssue>();

        public bool IsValid
        {
            get { return ErrorCount == 0; }
        }

        public int ErrorCount
        {
            get { return Count(DungeonValidationSeverity.Error); }
        }

        public int WarningCount
        {
            get { return Count(DungeonValidationSeverity.Warning); }
        }

        // 지정 코드의 문제가 리포트에 포함되어 있는지 확인합니다.
        public bool ContainsCode(string code)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i] != null && string.Equals(issues[i].code, code, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // 오류 또는 경고를 선택적 셀·spawn 위치와 함께 추가합니다.
        public void Add(
            string code,
            DungeonValidationSeverity severity,
            string message,
            Vector2Int? cell = null,
            string spawnId = "")
        {
            issues.Add(new DungeonValidationIssue
            {
                code = code ?? string.Empty,
                severity = severity,
                message = message ?? string.Empty,
                hasCell = cell.HasValue,
                cell = cell.GetValueOrDefault(),
                spawnId = spawnId ?? string.Empty
            });
        }

        // 지정 심각도와 일치하는 문제 개수를 계산합니다.
        private int Count(DungeonValidationSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i] != null && issues[i].severity == severity) count++;
            }
            return count;
        }
    }

    public static class DungeonBlueprintValidator
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        // Blueprint의 버전, 연결성, 참조, 배치와 저장 해시를 한 번에 검증합니다.
        public static DungeonValidationReport Validate(DungeonBlueprint blueprint, bool verifyStoredHash = true)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (blueprint == null)
            {
                report.Add(DungeonBlueprintValidationCodes.NullBlueprint, DungeonValidationSeverity.Error, "Blueprint is null.");
                return report;
            }

            if (blueprint.formatVersion != DungeonBlueprintFormat.CurrentVersion)
            {
                report.Add(DungeonBlueprintValidationCodes.UnsupportedFormat, DungeonValidationSeverity.Error, "Unsupported Blueprint format version.");
            }
            if (blueprint.generatorVersion <= 0)
            {
                report.Add(DungeonBlueprintValidationCodes.InvalidGeneratorVersion, DungeonValidationSeverity.Error, "Generator version must be positive.");
            }

            bool gridValid = ValidateGrid(blueprint.grid, report);
            HashSet<string> roomIds = ValidateRooms(blueprint.rooms, blueprint.grid, gridValid, report);
            Dictionary<Vector2Int, DungeonCellRecord> floorCells = ValidateCells(blueprint.cells, blueprint.grid, gridValid, roomIds, report);
            ValidateEndpointsAndConnectivity(blueprint, floorCells, gridValid, report);
            ValidateSpawns(blueprint.spawns, floorCells, roomIds, report);
            ValidateMetadataAndHash(blueprint, verifyStoredHash, report);
            return report;
        }

        // 그리드 크기와 월드 단위 값이 유한한 양수인지 검증합니다.
        private static bool ValidateGrid(DungeonGridRecord grid, DungeonValidationReport report)
        {
            if (grid == null)
            {
                report.Add(DungeonBlueprintValidationCodes.MissingGrid, DungeonValidationSeverity.Error, "Grid record is missing.");
                return false;
            }
            bool valid = grid.width > 0 && grid.depth > 0 && IsFinitePositive(grid.cellSize) && IsFinitePositive(grid.wallHeight);
            if (!valid)
            {
                report.Add(DungeonBlueprintValidationCodes.InvalidGrid, DungeonValidationSeverity.Error, "Grid size, cell size, and wall height must be finite positive values.");
            }
            return valid;
        }

        // 방 stable ID의 중복과 사각 영역 범위를 검증합니다.
        private static HashSet<string> ValidateRooms(
            List<DungeonRoomRecord> rooms,
            DungeonGridRecord grid,
            bool gridValid,
            DungeonValidationReport report)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (rooms == null)
            {
                report.Add(DungeonBlueprintValidationCodes.MissingRooms, DungeonValidationSeverity.Error, "Room list is missing.");
                return ids;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoomRecord room = rooms[i];
                if (room == null || string.IsNullOrWhiteSpace(room.roomId))
                {
                    report.Add(DungeonBlueprintValidationCodes.InvalidRoom, DungeonValidationSeverity.Error, "Room record or stable ID is invalid.");
                    continue;
                }
                if (!ids.Add(room.roomId))
                {
                    report.Add(DungeonBlueprintValidationCodes.DuplicateRoomId, DungeonValidationSeverity.Error, "Duplicate room ID: " + room.roomId);
                }
                if (room.bounds.width <= 0 || room.bounds.height <= 0 ||
                    (gridValid && (room.bounds.xMin < 0 || room.bounds.yMin < 0 || room.bounds.xMax > grid.width || room.bounds.yMax > grid.depth)))
                {
                    report.Add(DungeonBlueprintValidationCodes.InvalidRoom, DungeonValidationSeverity.Error, "Room bounds are invalid: " + room.roomId);
                }
            }
            return ids;
        }

        // 셀 좌표, 중복, BFS 거리와 room 참조를 검증하고 floor 셀 사전을 만듭니다.
        private static Dictionary<Vector2Int, DungeonCellRecord> ValidateCells(
            List<DungeonCellRecord> cells,
            DungeonGridRecord grid,
            bool gridValid,
            HashSet<string> roomIds,
            DungeonValidationReport report)
        {
            Dictionary<Vector2Int, DungeonCellRecord> allCells = new Dictionary<Vector2Int, DungeonCellRecord>();
            Dictionary<Vector2Int, DungeonCellRecord> floorCells = new Dictionary<Vector2Int, DungeonCellRecord>();
            if (cells == null)
            {
                report.Add(DungeonBlueprintValidationCodes.MissingCells, DungeonValidationSeverity.Error, "Cell list is missing.");
                return floorCells;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                DungeonCellRecord cell = cells[i];
                if (cell == null)
                {
                    report.Add(DungeonBlueprintValidationCodes.NullCell, DungeonValidationSeverity.Error, "Cell record is null.");
                    continue;
                }
                if (gridValid && !IsInBounds(cell.coordinate, grid))
                {
                    report.Add(DungeonBlueprintValidationCodes.CellOutOfBounds, DungeonValidationSeverity.Error, "Cell is outside the grid.", cell.coordinate);
                }
                if (allCells.ContainsKey(cell.coordinate))
                {
                    report.Add(DungeonBlueprintValidationCodes.DuplicateCell, DungeonValidationSeverity.Error, "Duplicate cell coordinate.", cell.coordinate);
                    continue;
                }
                allCells.Add(cell.coordinate, cell);

                if (!string.IsNullOrEmpty(cell.roomId) && !roomIds.Contains(cell.roomId))
                {
                    report.Add(DungeonBlueprintValidationCodes.UnknownRoomId, DungeonValidationSeverity.Error, "Cell references an unknown room ID.", cell.coordinate);
                }
                if ((cell.flags & DungeonCellFlags.Floor) == 0) continue;
                floorCells.Add(cell.coordinate, cell);
                if (cell.distanceFromEntrance < 0)
                {
                    report.Add(DungeonBlueprintValidationCodes.NegativeDistance, DungeonValidationSeverity.Error, "Floor cell distance must be non-negative.", cell.coordinate);
                }
            }

            if (floorCells.Count == 0)
            {
                report.Add(DungeonBlueprintValidationCodes.NoFloorCells, DungeonValidationSeverity.Error, "Blueprint has no floor cells.");
            }
            return floorCells;
        }

        // 입구·출구가 floor인지 확인하고 실제 BFS 연결성과 저장 거리를 대조합니다.
        private static void ValidateEndpointsAndConnectivity(
            DungeonBlueprint blueprint,
            Dictionary<Vector2Int, DungeonCellRecord> floorCells,
            bool gridValid,
            DungeonValidationReport report)
        {
            if (gridValid && (!IsInBounds(blueprint.entrance, blueprint.grid) || !floorCells.ContainsKey(blueprint.entrance)))
            {
                report.Add(DungeonBlueprintValidationCodes.EndpointNotFloor, DungeonValidationSeverity.Error, "Entrance is not a valid floor cell.", blueprint.entrance);
            }
            if (gridValid && (!IsInBounds(blueprint.exit, blueprint.grid) || !floorCells.ContainsKey(blueprint.exit)))
            {
                report.Add(DungeonBlueprintValidationCodes.EndpointNotFloor, DungeonValidationSeverity.Error, "Exit is not a valid floor cell.", blueprint.exit);
            }
            if (blueprint.entrance == blueprint.exit)
            {
                report.Add(DungeonBlueprintValidationCodes.EntranceEqualsExit, DungeonValidationSeverity.Warning, "Entrance and exit use the same cell.", blueprint.entrance);
            }
            if (!floorCells.ContainsKey(blueprint.entrance)) return;

            Dictionary<Vector2Int, int> distances = CalculateDistances(blueprint.entrance, floorCells);
            if (distances.Count != floorCells.Count)
            {
                report.Add(DungeonBlueprintValidationCodes.DisconnectedFloor, DungeonValidationSeverity.Error, "Not every floor cell is reachable from the entrance.");
            }

            int mismatches = 0;
            foreach (KeyValuePair<Vector2Int, int> pair in distances)
            {
                DungeonCellRecord cell;
                if (floorCells.TryGetValue(pair.Key, out cell) && cell.distanceFromEntrance != pair.Value) mismatches++;
            }
            if (mismatches > 0)
            {
                report.Add(DungeonBlueprintValidationCodes.DistanceMismatch, DungeonValidationSeverity.Error, mismatches + " floor distances do not match BFS results.");
            }
        }

        // spawn ID, content key, floor 위치, room 참조와 transform 수치를 검증합니다.
        private static void ValidateSpawns(
            List<DungeonSpawnRecord> spawns,
            Dictionary<Vector2Int, DungeonCellRecord> floorCells,
            HashSet<string> roomIds,
            DungeonValidationReport report)
        {
            if (spawns == null)
            {
                report.Add(DungeonBlueprintValidationCodes.MissingSpawns, DungeonValidationSeverity.Error, "Spawn list is missing.");
                return;
            }

            HashSet<string> spawnIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = spawns[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.spawnId) || string.IsNullOrWhiteSpace(spawn.contentKey) || !Enum.IsDefined(typeof(DungeonSpawnCategory), spawn.category))
                {
                    report.Add(DungeonBlueprintValidationCodes.InvalidSpawn, DungeonValidationSeverity.Error, "Spawn record, stable ID, category, or content key is invalid.");
                    continue;
                }
                if (!spawnIds.Add(spawn.spawnId))
                {
                    report.Add(DungeonBlueprintValidationCodes.DuplicateSpawnId, DungeonValidationSeverity.Error, "Duplicate spawn ID: " + spawn.spawnId, spawn.cell, spawn.spawnId);
                }
                DungeonCellRecord floorCell;
                if (!floorCells.TryGetValue(spawn.cell, out floorCell))
                {
                    report.Add(DungeonBlueprintValidationCodes.SpawnNotOnFloor, DungeonValidationSeverity.Error, "Spawn is not on a floor cell.", spawn.cell, spawn.spawnId);
                }
                else if (!string.Equals(
                             spawn.roomId ?? string.Empty,
                             floorCell.roomId ?? string.Empty,
                             StringComparison.Ordinal))
                {
                    report.Add(DungeonBlueprintValidationCodes.SpawnRoomMismatch, DungeonValidationSeverity.Error, "Spawn room ID does not match its floor cell.", spawn.cell, spawn.spawnId);
                }
                if (!string.IsNullOrEmpty(spawn.roomId) && !roomIds.Contains(spawn.roomId))
                {
                    report.Add(DungeonBlueprintValidationCodes.UnknownRoomId, DungeonValidationSeverity.Error, "Spawn references an unknown room ID.", spawn.cell, spawn.spawnId);
                }
                if (!IsFinite(spawn.localPosition) || !IsFinite(spawn.localScale) ||
                    !IsFinite(spawn.pitchDegrees) || !IsFinite(spawn.yawDegrees) || !IsFinite(spawn.rollDegrees) ||
                    spawn.localScale.x <= 0f || spawn.localScale.y <= 0f || spawn.localScale.z <= 0f ||
                    !IsFinite(spawn.progression) || spawn.progression < 0f || spawn.progression > 1f)
                {
                    report.Add(DungeonBlueprintValidationCodes.InvalidSpawnTransform, DungeonValidationSeverity.Error, "Spawn transform or progression is invalid.", spawn.cell, spawn.spawnId);
                }
            }
        }

        // 출처 해시 존재 여부와 저장된 Blueprint 해시의 무결성을 검사합니다.
        private static void ValidateMetadataAndHash(DungeonBlueprint blueprint, bool verifyStoredHash, DungeonValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(blueprint.recipeHash))
            {
                report.Add(DungeonBlueprintValidationCodes.MissingRecipeHash, DungeonValidationSeverity.Warning, "Recipe hash is missing.");
            }
            if (string.IsNullOrWhiteSpace(blueprint.blueprintHash))
            {
                report.Add(DungeonBlueprintValidationCodes.MissingBlueprintHash, DungeonValidationSeverity.Warning, "Blueprint hash is missing.");
                return;
            }
            if (!verifyStoredHash) return;

            string actual = DungeonBlueprintHasher.Compute(blueprint);
            if (!string.Equals(blueprint.blueprintHash, actual, StringComparison.OrdinalIgnoreCase))
            {
                report.Add(DungeonBlueprintValidationCodes.BlueprintHashMismatch, DungeonValidationSeverity.Error, "Stored Blueprint hash does not match its logical data.");
            }
        }

        // floor 셀 집합에서 입구 기준 실제 최단 거리를 계산합니다.
        private static Dictionary<Vector2Int, int> CalculateDistances(
            Vector2Int entrance,
            Dictionary<Vector2Int, DungeonCellRecord> floorCells)
        {
            Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            distances.Add(entrance, 0);
            queue.Enqueue(entrance);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                int nextDistance = distances[current] + 1;
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int next = current + Directions[i];
                    if (!floorCells.ContainsKey(next) || distances.ContainsKey(next)) continue;
                    distances.Add(next, nextDistance);
                    queue.Enqueue(next);
                }
            }
            return distances;
        }

        // 좌표가 유효한 그리드 사각 영역 안에 있는지 확인합니다.
        private static bool IsInBounds(Vector2Int cell, DungeonGridRecord grid)
        {
            return cell.x >= 0 && cell.x < grid.width && cell.y >= 0 && cell.y < grid.depth;
        }

        // 단일 float가 NaN이나 무한대가 아닌지 확인합니다.
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // 단일 float가 유한한 양수인지 확인합니다.
        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        // Vector3의 모든 축이 유한한지 확인합니다.
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
