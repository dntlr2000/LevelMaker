using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonContentCatalogValidationCodes
    {
        public const string NullCatalog = "RDL-CAT-001";
        public const string UnsupportedFormat = "RDL-CAT-002";
        public const string MissingEntries = "RDL-CAT-003";
        public const string NullEntry = "RDL-CAT-004";
        public const string MissingKey = "RDL-CAT-005";
        public const string KeyWhitespace = "RDL-CAT-006";
        public const string DuplicateKey = "RDL-CAT-007";
        public const string InvalidCategory = "RDL-CAT-008";
        public const string InvalidWeight = "RDL-CAT-009";
        public const string InvalidProgression = "RDL-CAT-010";
        public const string InvalidPlacement = "RDL-CAT-011";
        public const string InvalidFootprint = "RDL-CAT-012";
        public const string InvalidSpacing = "RDL-CAT-013";
        public const string InvalidRoomTag = "RDL-CAT-014";
        public const string DuplicateRoomTag = "RDL-CAT-015";
        public const string MissingPrefab = "RDL-CAT-016";
        public const string InvalidVariation = "RDL-CAT-017";
        public const string ReservedKeyCategoryMismatch = "RDL-CAT-018";
    }

    public static class DungeonContentValidationCodes
    {
        public const string MissingKey = "RDL-CONTENT-001";
        public const string CategoryMismatch = "RDL-CONTENT-002";
        public const string ProgressionMismatch = "RDL-CONTENT-003";
        public const string PlacementMismatch = "RDL-CONTENT-004";
        public const string RequiredRoomTagMissing = "RDL-CONTENT-005";
        public const string FootprintOutsideFloor = "RDL-CONTENT-006";
        public const string FootprintOverlap = "RDL-CONTENT-007";
        public const string MissingPrefab = "RDL-CONTENT-008";
        public const string CatalogHashMismatch = "RDL-CONTENT-009";
    }

    public static class DungeonContentCatalogValidator
    {
        // Catalog 자체의 직렬화·선택 필드와 Prefab 누락을 코드 기반으로 검증합니다.
        public static DungeonValidationReport Validate(DungeonContentCatalog catalog)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (catalog == null)
            {
                report.Add(DungeonContentCatalogValidationCodes.NullCatalog, DungeonValidationSeverity.Error, "Content Catalog is null.");
                return report;
            }
            if (catalog.formatVersion != DungeonContentCatalog.CurrentFormatVersion)
            {
                report.Add(DungeonContentCatalogValidationCodes.UnsupportedFormat, DungeonValidationSeverity.Error, "Unsupported Content Catalog format version.");
            }
            if (catalog.entries == null)
            {
                report.Add(DungeonContentCatalogValidationCodes.MissingEntries, DungeonValidationSeverity.Error, "Content Catalog entry list is missing.");
                return report;
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry == null)
                {
                    report.Add(DungeonContentCatalogValidationCodes.NullEntry, DungeonValidationSeverity.Error, "Content Catalog entry is null at index " + i + ".");
                    continue;
                }

                string key = entry.contentKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    report.Add(DungeonContentCatalogValidationCodes.MissingKey, DungeonValidationSeverity.Error, "Content key is empty at index " + i + ".");
                }
                else
                {
                    if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
                        report.Add(DungeonContentCatalogValidationCodes.KeyWhitespace, DungeonValidationSeverity.Error, "Content key has surrounding whitespace: " + key);
                    if (!keys.Add(key))
                        report.Add(DungeonContentCatalogValidationCodes.DuplicateKey, DungeonValidationSeverity.Error, "Duplicate content key: " + key);
                }

                if (!Enum.IsDefined(typeof(DungeonSpawnCategory), entry.category))
                    report.Add(DungeonContentCatalogValidationCodes.InvalidCategory, DungeonValidationSeverity.Error, "Content category is invalid for key: " + key);
                DungeonSpawnCategory reservedCategory;
                if (TryGetBuiltInCategory(key, out reservedCategory) && entry.category != reservedCategory)
                    report.Add(DungeonContentCatalogValidationCodes.ReservedKeyCategoryMismatch, DungeonValidationSeverity.Error, "Reserved built-in key uses a different category: " + key);
                if (!IsFinite(entry.weight) || entry.weight <= 0f)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidWeight, DungeonValidationSeverity.Error, "Content weight must be finite and positive for key: " + key);
                if (!IsFinite(entry.minProgression) || !IsFinite(entry.maxProgression) ||
                    entry.minProgression < 0f || entry.maxProgression > 1f || entry.minProgression > entry.maxProgression)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidProgression, DungeonValidationSeverity.Error, "Progression range is invalid for key: " + key);
                if (!Enum.IsDefined(typeof(DungeonContentPlacement), entry.placement))
                    report.Add(DungeonContentCatalogValidationCodes.InvalidPlacement, DungeonValidationSeverity.Error, "Placement rule is invalid for key: " + key);
                if (entry.footprintCells.x < 1 || entry.footprintCells.y < 1)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidFootprint, DungeonValidationSeverity.Error, "Footprint axes must be at least one cell for key: " + key);
                if (entry.minimumSpacingCells < 0)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidSpacing, DungeonValidationSeverity.Error, "Minimum spacing cannot be negative for key: " + key);
                ValidateTags(entry, report);
                if (!IsFinite(entry.yawDegreesRange.x) || !IsFinite(entry.yawDegreesRange.y) ||
                    entry.yawDegreesRange.x > entry.yawDegreesRange.y ||
                    !IsFinite(entry.uniformScaleRange.x) || !IsFinite(entry.uniformScaleRange.y) ||
                    entry.uniformScaleRange.x <= 0f || entry.uniformScaleRange.x > entry.uniformScaleRange.y)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidVariation, DungeonValidationSeverity.Error, "Yaw or scale variation range is invalid for key: " + key);
                if (entry.prefab == null)
                    report.Add(DungeonContentCatalogValidationCodes.MissingPrefab, DungeonValidationSeverity.Warning, "Content Prefab is missing for key: " + key);
            }
            return report;
        }

        // Unity 표현 참조가 제거된 planning snapshot도 동일한 결정성 필드 규칙으로 검증합니다.
        public static DungeonValidationReport ValidatePlanningSnapshot(
            DungeonContentCatalogPlanningSnapshot snapshot)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (snapshot == null)
            {
                report.Add(DungeonContentCatalogValidationCodes.NullCatalog, DungeonValidationSeverity.Error, "Content Catalog planning snapshot is null.");
                return report;
            }
            if (snapshot.formatVersion != DungeonContentCatalog.CurrentFormatVersion)
                report.Add(DungeonContentCatalogValidationCodes.UnsupportedFormat, DungeonValidationSeverity.Error, "Unsupported Content Catalog planning snapshot format version.");
            if (snapshot.entries == null)
            {
                report.Add(DungeonContentCatalogValidationCodes.MissingEntries, DungeonValidationSeverity.Error, "Content Catalog planning entry list is missing.");
                return report;
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.entries.Count; i++)
            {
                DungeonContentPlanningEntry entry = snapshot.entries[i];
                if (entry == null)
                {
                    report.Add(DungeonContentCatalogValidationCodes.NullEntry, DungeonValidationSeverity.Error, "Content Catalog planning entry is null at index " + i + ".");
                    continue;
                }

                string key = entry.contentKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    report.Add(DungeonContentCatalogValidationCodes.MissingKey, DungeonValidationSeverity.Error, "Content key is empty at index " + i + ".");
                }
                else
                {
                    if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
                        report.Add(DungeonContentCatalogValidationCodes.KeyWhitespace, DungeonValidationSeverity.Error, "Content key has surrounding whitespace: " + key);
                    if (!keys.Add(key))
                        report.Add(DungeonContentCatalogValidationCodes.DuplicateKey, DungeonValidationSeverity.Error, "Duplicate content key: " + key);
                }

                if (!Enum.IsDefined(typeof(DungeonSpawnCategory), entry.category))
                    report.Add(DungeonContentCatalogValidationCodes.InvalidCategory, DungeonValidationSeverity.Error, "Content category is invalid for key: " + key);
                DungeonSpawnCategory reservedCategory;
                if (TryGetBuiltInCategory(key, out reservedCategory) && entry.category != reservedCategory)
                    report.Add(DungeonContentCatalogValidationCodes.ReservedKeyCategoryMismatch, DungeonValidationSeverity.Error, "Reserved built-in key uses a different category: " + key);
                if (!IsFinite(entry.weight) || entry.weight <= 0f)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidWeight, DungeonValidationSeverity.Error, "Content weight must be finite and positive for key: " + key);
                if (!IsFinite(entry.minProgression) || !IsFinite(entry.maxProgression) ||
                    entry.minProgression < 0f || entry.maxProgression > 1f || entry.minProgression > entry.maxProgression)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidProgression, DungeonValidationSeverity.Error, "Progression range is invalid for key: " + key);
                if (!Enum.IsDefined(typeof(DungeonContentPlacement), entry.placement))
                    report.Add(DungeonContentCatalogValidationCodes.InvalidPlacement, DungeonValidationSeverity.Error, "Placement rule is invalid for key: " + key);
                if (entry.footprintCells.x < 1 || entry.footprintCells.y < 1)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidFootprint, DungeonValidationSeverity.Error, "Footprint axes must be at least one cell for key: " + key);
                if (entry.minimumSpacingCells < 0)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidSpacing, DungeonValidationSeverity.Error, "Minimum spacing cannot be negative for key: " + key);
                ValidatePlanningTags(entry, report);
                if (!IsFinite(entry.yawDegreesRange.x) || !IsFinite(entry.yawDegreesRange.y) ||
                    entry.yawDegreesRange.x > entry.yawDegreesRange.y ||
                    !IsFinite(entry.uniformScaleRange.x) || !IsFinite(entry.uniformScaleRange.y) ||
                    entry.uniformScaleRange.x <= 0f || entry.uniformScaleRange.x > entry.uniformScaleRange.y)
                    report.Add(DungeonContentCatalogValidationCodes.InvalidVariation, DungeonValidationSeverity.Error, "Yaw or scale variation range is invalid for key: " + key);
            }
            return report;
        }

        // Blueprint의 최종 key와 현재 catalog·배치 조건·누락 정책을 교차 검증합니다.
        public static DungeonValidationReport ValidateBlueprint(
            DungeonBlueprint blueprint,
            DungeonContentCatalog catalog,
            DungeonMissingContentPolicy missingPolicy)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (blueprint == null || blueprint.spawns == null) return report;

            Dictionary<string, DungeonContentCatalogEntry> entries = BuildEntryLookup(catalog);
            Dictionary<Vector2Int, DungeonCellRecord> floorCells = BuildFloorCellLookup(blueprint);
            HashSet<Vector2Int> floor = new HashSet<Vector2Int>(floorCells.Keys);
            Dictionary<string, DungeonRoomRecord> rooms = BuildRoomLookup(blueprint);
            List<PlacedFootprint> placed = new List<PlacedFootprint>();

            if (catalog != null && blueprint.generatorVersion == DungeonGeneratorVersions.StableV2)
            {
                string actualHash = catalog.ComputePlanningHash();
                if (!string.Equals(blueprint.catalogPlanningHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    report.Add(DungeonContentValidationCodes.CatalogHashMismatch, DungeonValidationSeverity.Warning, "Blueprint catalog planning hash differs from the current Content Catalog.");
            }

            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn == null) continue;
                DungeonContentCatalogEntry entry;
                bool hasEntry = entries.TryGetValue(spawn.contentKey ?? string.Empty, out entry);
                DungeonSpawnCategory builtInCategory;
                bool isBuiltIn = TryGetBuiltInCategory(spawn.contentKey, out builtInCategory);

                if (hasEntry)
                {
                    if (entry.category != spawn.category)
                        report.Add(DungeonContentValidationCodes.CategoryMismatch, DungeonValidationSeverity.Error, "Catalog category does not match Blueprint spawn: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                    if (isBuiltIn && entry.category != builtInCategory)
                        report.Add(DungeonContentValidationCodes.CategoryMismatch, DungeonValidationSeverity.Error, "Catalog overrides a reserved built-in key with a different category: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                    if (spawn.progression < entry.minProgression || spawn.progression > entry.maxProgression)
                        report.Add(DungeonContentValidationCodes.ProgressionMismatch, DungeonValidationSeverity.Error, "Spawn progression is outside the catalog range: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                    DungeonCellRecord floorCell;
                    string actualRoomId = floorCells.TryGetValue(spawn.cell, out floorCell)
                        ? floorCell.roomId ?? string.Empty
                        : string.Empty;
                    bool isRoom = !string.IsNullOrEmpty(actualRoomId);
                    if ((entry.placement == DungeonContentPlacement.RoomOnly && !isRoom) ||
                        (entry.placement == DungeonContentPlacement.CorridorOnly && isRoom))
                        report.Add(DungeonContentValidationCodes.PlacementMismatch, DungeonValidationSeverity.Error, "Spawn room/corridor placement does not match the catalog rule: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                    if (!HasRequiredTags(actualRoomId, entry, rooms))
                        report.Add(DungeonContentValidationCodes.RequiredRoomTagMissing, DungeonValidationSeverity.Error, "Spawn room does not contain every required catalog tag: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                    if (entry.prefab == null && !isBuiltIn)
                    {
                        DungeonValidationSeverity severity = missingPolicy == DungeonMissingContentPolicy.Error
                            ? DungeonValidationSeverity.Error
                            : DungeonValidationSeverity.Warning;
                        report.Add(DungeonContentValidationCodes.MissingPrefab, severity, "Catalog Prefab is missing for content key: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                    }
                }
                else if (isBuiltIn)
                {
                    if (builtInCategory != spawn.category)
                        report.Add(DungeonContentValidationCodes.CategoryMismatch, DungeonValidationSeverity.Error, "Built-in key category does not match Blueprint spawn: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                }
                else
                {
                    DungeonValidationSeverity severity = missingPolicy == DungeonMissingContentPolicy.Error
                        ? DungeonValidationSeverity.Error
                        : DungeonValidationSeverity.Warning;
                    report.Add(DungeonContentValidationCodes.MissingKey, severity, "Content key is not present in the catalog: " + spawn.contentKey, spawn.cell, spawn.spawnId);
                }

                Vector2Int footprint = hasEntry ? entry.footprintCells : Vector2Int.one;
                int spacing = hasEntry ? entry.minimumSpacingCells : 0;
                ValidateAndAddFootprint(spawn, footprint, spacing, floor, placed, report);
            }
            return report;
        }

        private static void ValidateTags(DungeonContentCatalogEntry entry, DungeonValidationReport report)
        {
            if (entry.requiredRoomTags == null) return;
            HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entry.requiredRoomTags.Count; i++)
            {
                string tag = entry.requiredRoomTags[i];
                if (string.IsNullOrWhiteSpace(tag) || !string.Equals(tag, tag.Trim(), StringComparison.Ordinal))
                    report.Add(DungeonContentCatalogValidationCodes.InvalidRoomTag, DungeonValidationSeverity.Error, "Required room tag is invalid for key: " + entry.contentKey);
                else if (!tags.Add(tag))
                    report.Add(DungeonContentCatalogValidationCodes.DuplicateRoomTag, DungeonValidationSeverity.Error, "Duplicate required room tag for key: " + entry.contentKey + " (" + tag + ")");
            }
        }

        // planning entry의 방 태그가 공백·중복 없이 canonical 값인지 검사합니다.
        private static void ValidatePlanningTags(
            DungeonContentPlanningEntry entry,
            DungeonValidationReport report)
        {
            if (entry.requiredRoomTags == null) return;
            HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entry.requiredRoomTags.Count; i++)
            {
                string tag = entry.requiredRoomTags[i];
                if (string.IsNullOrWhiteSpace(tag) || !string.Equals(tag, tag.Trim(), StringComparison.Ordinal))
                    report.Add(DungeonContentCatalogValidationCodes.InvalidRoomTag, DungeonValidationSeverity.Error, "Required room tag is invalid for key: " + entry.contentKey);
                else if (!tags.Add(tag))
                    report.Add(DungeonContentCatalogValidationCodes.DuplicateRoomTag, DungeonValidationSeverity.Error, "Duplicate required room tag for key: " + entry.contentKey + " (" + tag + ")");
            }
        }

        private static Dictionary<string, DungeonContentCatalogEntry> BuildEntryLookup(DungeonContentCatalog catalog)
        {
            Dictionary<string, DungeonContentCatalogEntry> result = new Dictionary<string, DungeonContentCatalogEntry>(StringComparer.Ordinal);
            if (catalog == null || catalog.entries == null) return result;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                DungeonContentCatalogEntry entry = catalog.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.contentKey) || result.ContainsKey(entry.contentKey)) continue;
                result.Add(entry.contentKey, entry);
            }
            return result;
        }

        // Blueprint의 실제 floor 좌표와 room 소속을 교차 검증용 lookup으로 만듭니다.
        private static Dictionary<Vector2Int, DungeonCellRecord> BuildFloorCellLookup(
            DungeonBlueprint blueprint)
        {
            Dictionary<Vector2Int, DungeonCellRecord> result =
                new Dictionary<Vector2Int, DungeonCellRecord>();
            if (blueprint.cells == null) return result;
            for (int i = 0; i < blueprint.cells.Count; i++)
            {
                DungeonCellRecord cell = blueprint.cells[i];
                if (cell != null &&
                    (cell.flags & DungeonCellFlags.Floor) != 0 &&
                    !result.ContainsKey(cell.coordinate))
                {
                    result.Add(cell.coordinate, cell);
                }
            }
            return result;
        }

        private static Dictionary<string, DungeonRoomRecord> BuildRoomLookup(DungeonBlueprint blueprint)
        {
            Dictionary<string, DungeonRoomRecord> result = new Dictionary<string, DungeonRoomRecord>(StringComparer.Ordinal);
            if (blueprint.rooms == null) return result;
            for (int i = 0; i < blueprint.rooms.Count; i++)
            {
                DungeonRoomRecord room = blueprint.rooms[i];
                if (room != null && !string.IsNullOrEmpty(room.roomId) && !result.ContainsKey(room.roomId)) result.Add(room.roomId, room);
            }
            return result;
        }

        private static bool HasRequiredTags(
            string roomId,
            DungeonContentCatalogEntry entry,
            Dictionary<string, DungeonRoomRecord> rooms)
        {
            if (entry.requiredRoomTags == null || entry.requiredRoomTags.Count == 0) return true;
            DungeonRoomRecord room;
            if (string.IsNullOrEmpty(roomId) || !rooms.TryGetValue(roomId, out room) || room.tags == null) return false;
            HashSet<string> tags = new HashSet<string>(room.tags, StringComparer.Ordinal);
            for (int i = 0; i < entry.requiredRoomTags.Count; i++)
            {
                if (!tags.Contains(entry.requiredRoomTags[i])) return false;
            }
            return true;
        }

        private static void ValidateAndAddFootprint(
            DungeonSpawnRecord spawn,
            Vector2Int footprint,
            int spacing,
            HashSet<Vector2Int> floor,
            List<PlacedFootprint> placed,
            DungeonValidationReport report)
        {
            List<Vector2Int> cells = FootprintCells(spawn.cell, footprint);
            bool outside = false;
            for (int i = 0; i < cells.Count; i++) if (!floor.Contains(cells[i])) outside = true;
            if (outside)
                report.Add(DungeonContentValidationCodes.FootprintOutsideFloor, DungeonValidationSeverity.Error, "Content footprint leaves the floor: " + spawn.contentKey, spawn.cell, spawn.spawnId);

            bool overlap = false;
            for (int previousIndex = 0; previousIndex < placed.Count && !overlap; previousIndex++)
            {
                PlacedFootprint previous = placed[previousIndex];
                int required = Mathf.Max(spacing, previous.Spacing);
                for (int i = 0; i < cells.Count && !overlap; i++)
                for (int j = 0; j < previous.Cells.Count; j++)
                {
                    int dx = Mathf.Abs(cells[i].x - previous.Cells[j].x);
                    int dz = Mathf.Abs(cells[i].y - previous.Cells[j].y);
                    if (Mathf.Max(dx, dz) <= required) overlap = true;
                }
            }
            if (overlap)
                report.Add(DungeonContentValidationCodes.FootprintOverlap, DungeonValidationSeverity.Error, "Content footprints overlap or violate minimum spacing: " + spawn.contentKey, spawn.cell, spawn.spawnId);
            placed.Add(new PlacedFootprint(cells, Mathf.Max(0, spacing)));
        }

        internal static List<Vector2Int> FootprintCells(Vector2Int anchor, Vector2Int footprint)
        {
            int width = Mathf.Max(1, footprint.x);
            int depth = Mathf.Max(1, footprint.y);
            int minX = -(width / 2);
            int minZ = -(depth / 2);
            List<Vector2Int> result = new List<Vector2Int>(width * depth);
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++) result.Add(new Vector2Int(anchor.x + minX + x, anchor.y + minZ + z));
            return result;
        }

        internal static bool TryGetBuiltInCategory(string key, out DungeonSpawnCategory category)
        {
            if (key == DungeonBuiltInContentKeys.EntranceMarker || key == DungeonBuiltInContentKeys.ExitMarker)
            {
                category = DungeonSpawnCategory.Marker;
                return true;
            }
            if (key == DungeonBuiltInContentKeys.Gimmick) { category = DungeonSpawnCategory.Gimmick; return true; }
            if (key == DungeonBuiltInContentKeys.Enemy) { category = DungeonSpawnCategory.Enemy; return true; }
            if (key == DungeonBuiltInContentKeys.Destructible) { category = DungeonSpawnCategory.Destructible; return true; }
            if (key == DungeonBuiltInContentKeys.PropCube || key == DungeonBuiltInContentKeys.PropCylinder)
            {
                category = DungeonSpawnCategory.Prop;
                return true;
            }
            category = default(DungeonSpawnCategory);
            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class PlacedFootprint
        {
            public readonly List<Vector2Int> Cells;
            public readonly int Spacing;

            public PlacedFootprint(List<Vector2Int> cells, int spacing)
            {
                Cells = cells;
                Spacing = spacing;
            }
        }
    }
}
