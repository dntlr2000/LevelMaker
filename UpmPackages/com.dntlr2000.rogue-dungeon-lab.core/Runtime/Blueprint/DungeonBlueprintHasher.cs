using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonRecipeHasher
    {
        // 생성 결과에 영향을 주는 레시피 스냅샷 값만 canonical 순서로 해시합니다.
        public static string Compute(DungeonRecipeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            using (DungeonCanonicalWriter writer = new DungeonCanonicalWriter())
            {
                writer.WriteString("RDL_RECIPE");
                writer.WriteInt(snapshot.formatVersion);
                writer.WriteInt(snapshot.stageWidthCells);
                writer.WriteInt(snapshot.stageDepthCells);
                writer.WriteFloat(snapshot.cellSize);
                writer.WriteFloat(snapshot.wallHeight);
                writer.WriteInt(snapshot.desiredRoomCount);
                writer.WriteVector2Int(snapshot.roomSizeMin);
                writer.WriteVector2Int(snapshot.roomSizeMax);
                writer.WriteInt(snapshot.roomPlacementAttempts);
                writer.WriteInt(snapshot.corridorWidthCells);
                writer.WriteFloat(snapshot.extraConnectionChance);
                writer.WriteInt(snapshot.specialGimmickCount);
                writer.WriteInt(snapshot.contentSpacingCells);
                writer.WriteInt(snapshot.reservedEntranceRadiusCells);
                WriteProfile(writer, snapshot.enemyProfile);
                WriteProfile(writer, snapshot.destructibleProfile);
                WriteProfile(writer, snapshot.propProfile);
                return writer.FinishHash();
            }
        }

        // 밀도 수치와 진행도 곡선을 null 여부까지 포함해 기록합니다.
        private static void WriteProfile(DungeonCanonicalWriter writer, DungeonDensityProfileSnapshot profile)
        {
            writer.WriteBool(profile != null);
            if (profile == null) return;
            writer.WriteFloat(profile.baseDensity);
            writer.WriteFloat(profile.roomBias);
            writer.WriteFloat(profile.clustering);
            writer.WriteInt(profile.maxCount);
            WriteCurve(writer, profile.overProgression);
        }

        // 곡선 키를 시간과 값 기준의 canonical 순서로 정렬해 기록합니다.
        private static void WriteCurve(DungeonCanonicalWriter writer, DungeonCurveSnapshot curve)
        {
            writer.WriteBool(curve != null);
            if (curve == null) return;
            writer.WriteInt((int)curve.preWrapMode);
            writer.WriteInt((int)curve.postWrapMode);
            List<DungeonCurveKeySnapshot> keys = curve.keys != null
                ? new List<DungeonCurveKeySnapshot>(curve.keys)
                : new List<DungeonCurveKeySnapshot>();
            keys.Sort(CompareCurveKeys);
            writer.WriteInt(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                DungeonCurveKeySnapshot key = keys[i];
                writer.WriteBool(key != null);
                if (key == null) continue;
                writer.WriteFloat(key.time);
                writer.WriteFloat(key.value);
                writer.WriteFloat(key.inTangent);
                writer.WriteFloat(key.outTangent);
                writer.WriteFloat(key.inWeight);
                writer.WriteFloat(key.outWeight);
                writer.WriteInt((int)key.weightedMode);
            }
        }

        // 같은 시간의 키도 모든 수치 기준으로 안정적으로 정렬합니다.
        private static int CompareCurveKeys(DungeonCurveKeySnapshot left, DungeonCurveKeySnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = left.time.CompareTo(right.time);
            if (result != 0) return result;
            result = left.value.CompareTo(right.value);
            if (result != 0) return result;
            result = left.inTangent.CompareTo(right.inTangent);
            if (result != 0) return result;
            result = left.outTangent.CompareTo(right.outTangent);
            if (result != 0) return result;
            result = left.inWeight.CompareTo(right.inWeight);
            if (result != 0) return result;
            result = left.outWeight.CompareTo(right.outWeight);
            return result != 0 ? result : ((int)left.weightedMode).CompareTo((int)right.weightedMode);
        }
    }

    public static class DungeonBlueprintHasher
    {
        // 편집 메모·생성 시각·기존 해시를 제외한 Blueprint 논리 데이터를 해시합니다.
        public static string Compute(DungeonBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            using (DungeonCanonicalWriter writer = new DungeonCanonicalWriter())
            {
                writer.WriteString("RDL_BLUEPRINT");
                writer.WriteInt(blueprint.formatVersion);
                writer.WriteInt(blueprint.generatorVersion);
                writer.WriteInt(blueprint.seed);
                writer.WriteString(blueprint.recipeHash);
                writer.WriteString(blueprint.catalogPlanningHash);
                WriteGrid(writer, blueprint.grid);
                writer.WriteVector2Int(blueprint.entrance);
                writer.WriteVector2Int(blueprint.exit);
                WriteCells(writer, blueprint.cells);
                WriteRooms(writer, blueprint.rooms);
                WriteSpawns(writer, blueprint.spawns);
                return writer.FinishHash();
            }
        }

        // 그리드가 null인지와 크기·월드 배율을 고정 순서로 기록합니다.
        private static void WriteGrid(DungeonCanonicalWriter writer, DungeonGridRecord grid)
        {
            writer.WriteBool(grid != null);
            if (grid == null) return;
            writer.WriteInt(grid.width);
            writer.WriteInt(grid.depth);
            writer.WriteFloat(grid.cellSize);
            writer.WriteFloat(grid.wallHeight);
        }

        // 셀 목록을 z, x와 나머지 값 순으로 정렬해 입력 순서의 영향을 제거합니다.
        private static void WriteCells(DungeonCanonicalWriter writer, List<DungeonCellRecord> source)
        {
            List<DungeonCellRecord> cells = source != null
                ? new List<DungeonCellRecord>(source)
                : new List<DungeonCellRecord>();
            cells.Sort(CompareCells);
            writer.WriteBool(source != null);
            writer.WriteInt(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                DungeonCellRecord cell = cells[i];
                writer.WriteBool(cell != null);
                if (cell == null) continue;
                writer.WriteVector2Int(cell.coordinate);
                writer.WriteInt((int)cell.flags);
                writer.WriteString(cell.roomId);
                writer.WriteInt(cell.distanceFromEntrance);
            }
        }

        // 방 목록과 tag를 stable ID 기준으로 정렬해 기록합니다.
        private static void WriteRooms(DungeonCanonicalWriter writer, List<DungeonRoomRecord> source)
        {
            List<DungeonRoomRecord> rooms = source != null
                ? new List<DungeonRoomRecord>(source)
                : new List<DungeonRoomRecord>();
            rooms.Sort(CompareRooms);
            writer.WriteBool(source != null);
            writer.WriteInt(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoomRecord room = rooms[i];
                writer.WriteBool(room != null);
                if (room == null) continue;
                writer.WriteString(room.roomId);
                writer.WriteRectInt(room.bounds);
                WriteTags(writer, room.tags);
            }
        }

        // spawn 목록을 stable ID 기준으로 정렬해 배치 레코드 전체를 기록합니다.
        private static void WriteSpawns(DungeonCanonicalWriter writer, List<DungeonSpawnRecord> source)
        {
            List<DungeonSpawnRecord> spawns = source != null
                ? new List<DungeonSpawnRecord>(source)
                : new List<DungeonSpawnRecord>();
            spawns.Sort(CompareSpawns);
            writer.WriteBool(source != null);
            writer.WriteInt(spawns.Count);
            for (int i = 0; i < spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = spawns[i];
                writer.WriteBool(spawn != null);
                if (spawn == null) continue;
                writer.WriteString(spawn.spawnId);
                writer.WriteInt((int)spawn.category);
                writer.WriteString(spawn.contentKey);
                writer.WriteString(spawn.instanceName);
                writer.WriteVector2Int(spawn.cell);
                writer.WriteVector3(spawn.localPosition);
                writer.WriteFloat(spawn.pitchDegrees);
                writer.WriteFloat(spawn.yawDegrees);
                writer.WriteFloat(spawn.rollDegrees);
                writer.WriteVector3(spawn.localScale);
                writer.WriteString(spawn.roomId);
                writer.WriteFloat(spawn.progression);
                WriteTags(writer, spawn.tags);
                writer.WriteInt(spawn.variantSeed);
            }
        }

        // 문자열 tag의 null 여부와 canonical 순서를 함께 기록합니다.
        private static void WriteTags(DungeonCanonicalWriter writer, List<string> source)
        {
            List<string> tags = source != null ? new List<string>(source) : new List<string>();
            tags.Sort(StringComparer.Ordinal);
            writer.WriteBool(source != null);
            writer.WriteInt(tags.Count);
            for (int i = 0; i < tags.Count; i++) writer.WriteString(tags[i]);
        }

        // 셀을 z, x, flag, room ID, 거리 순으로 비교합니다.
        private static int CompareCells(DungeonCellRecord left, DungeonCellRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = left.coordinate.y.CompareTo(right.coordinate.y);
            if (result != 0) return result;
            result = left.coordinate.x.CompareTo(right.coordinate.x);
            if (result != 0) return result;
            result = ((int)left.flags).CompareTo((int)right.flags);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.roomId, right.roomId);
            return result != 0 ? result : left.distanceFromEntrance.CompareTo(right.distanceFromEntrance);
        }

        // 방을 stable ID와 사각 영역 순으로 비교합니다.
        private static int CompareRooms(DungeonRoomRecord left, DungeonRoomRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.roomId, right.roomId);
            if (result != 0) return result;
            result = left.bounds.x.CompareTo(right.bounds.x);
            if (result != 0) return result;
            result = left.bounds.y.CompareTo(right.bounds.y);
            if (result != 0) return result;
            result = left.bounds.width.CompareTo(right.bounds.width);
            return result != 0 ? result : left.bounds.height.CompareTo(right.bounds.height);
        }

        // spawn을 stable ID와 위치·범주 보조 키 순으로 비교합니다.
        private static int CompareSpawns(DungeonSpawnRecord left, DungeonSpawnRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.spawnId, right.spawnId);
            if (result != 0) return result;
            result = ((int)left.category).CompareTo((int)right.category);
            if (result != 0) return result;
            result = left.cell.y.CompareTo(right.cell.y);
            return result != 0 ? result : left.cell.x.CompareTo(right.cell.x);
        }
    }

    public static class DungeonBlueprintSerialization
    {
        // Blueprint를 Unity 호환 JSON 문자열로 직렬화합니다.
        public static string ToJson(DungeonBlueprint blueprint, bool prettyPrint = false)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            return JsonUtility.ToJson(blueprint, prettyPrint);
        }

        // JSON 문자열을 새 Blueprint 인스턴스로 역직렬화합니다.
        public static DungeonBlueprint FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Blueprint JSON is empty.", nameof(json));
            DungeonBlueprint blueprint = JsonUtility.FromJson<DungeonBlueprint>(json);
            if (blueprint == null) throw new FormatException("Blueprint JSON did not produce an object.");
            return blueprint;
        }

        // JSON round-trip으로 모든 직렬화 컬렉션이 분리된 깊은 복사본을 만듭니다.
        public static DungeonBlueprint DeepClone(DungeonBlueprint blueprint)
        {
            return FromJson(ToJson(blueprint));
        }
    }

    internal sealed class DungeonCanonicalWriter : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;

        // 고정 UTF-8과 little-endian BinaryWriter를 사용하는 canonical 버퍼를 준비합니다.
        public DungeonCanonicalWriter()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream, new UTF8Encoding(false, true));
        }

        // null과 빈 문자열을 구분해 문자열을 기록합니다.
        public void WriteString(string value)
        {
            _writer.Write(value != null);
            if (value != null) _writer.Write(value);
        }

        // 정수 값을 고정 폭으로 기록합니다.
        public void WriteInt(int value)
        {
            _writer.Write(value);
        }

        // boolean 값을 한 바이트로 기록합니다.
        public void WriteBool(bool value)
        {
            _writer.Write(value);
        }

        // 음수 0을 양수 0으로 정규화한 IEEE 단정밀도 값을 기록합니다.
        public void WriteFloat(float value)
        {
            _writer.Write(value == 0f ? 0f : value);
        }

        // Vector2Int 좌표를 x, y 순으로 기록합니다.
        public void WriteVector2Int(Vector2Int value)
        {
            WriteInt(value.x);
            WriteInt(value.y);
        }

        // Vector3 값을 x, y, z 순으로 기록합니다.
        public void WriteVector3(Vector3 value)
        {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        // RectInt를 시작 좌표와 크기 순으로 기록합니다.
        public void WriteRectInt(RectInt value)
        {
            WriteInt(value.x);
            WriteInt(value.y);
            WriteInt(value.width);
            WriteInt(value.height);
        }

        // 지금까지 기록한 canonical 바이트의 소문자 SHA-256 문자열을 반환합니다.
        public string FinishHash()
        {
            _writer.Flush();
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(_stream.ToArray());
            }
            StringBuilder builder = new StringBuilder(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++) builder.Append(digest[i].ToString("x2"));
            return builder.ToString();
        }

        // canonical writer와 내부 메모리 스트림을 함께 해제합니다.
        public void Dispose()
        {
            _writer.Dispose();
            _stream.Dispose();
        }
    }
}
