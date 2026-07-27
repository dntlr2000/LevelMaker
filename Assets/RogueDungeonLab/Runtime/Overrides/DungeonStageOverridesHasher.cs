using System;
using System.Collections.Generic;

namespace RogueDungeonLab
{
    public static class DungeonStageOverridesHasher
    {
        // 목록 표시 순서와 제작 메모를 제외한 Override 논리 데이터를 canonical SHA-256으로 계산합니다.
        public static string Compute(DungeonStageOverrides stageOverrides)
        {
            if (stageOverrides == null) return string.Empty;
            using (DungeonCanonicalWriter writer = new DungeonCanonicalWriter())
            {
                writer.WriteString("RDL_STAGE_OVERRIDES");
                writer.WriteInt(stageOverrides.formatVersion);
                writer.WriteString(stageOverrides.baseBlueprintHash);
                WriteDisabled(writer, stageOverrides.disabledSpawns);
                WriteAdded(writer, stageOverrides.addedSpawns);
                WriteContent(writer, stageOverrides.contentOverrides);
                WriteTransforms(writer, stageOverrides.transformOverrides);
                return writer.FinishHash();
            }
        }

        // 비활성화 명령을 target ID와 binding anchor 순으로 정렬해 기록합니다.
        private static void WriteDisabled(
            DungeonCanonicalWriter writer,
            List<DungeonSpawnDisableOverride> source)
        {
            List<DungeonSpawnDisableOverride> values = source != null
                ? new List<DungeonSpawnDisableOverride>(source)
                : new List<DungeonSpawnDisableOverride>();
            values.Sort((left, right) => CompareBindings(
                left != null ? left.binding : null,
                right != null ? right.binding : null));
            writer.WriteInt(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnDisableOverride value = values[i];
                writer.WriteBool(value != null);
                if (value != null) WriteBinding(writer, value.binding);
            }
        }

        // 추가 spawn을 stable ID 기준으로 정렬하고 Blueprint와 같은 논리 필드를 기록합니다.
        private static void WriteAdded(
            DungeonCanonicalWriter writer,
            List<DungeonSpawnRecord> source)
        {
            List<DungeonSpawnRecord> values = source != null
                ? new List<DungeonSpawnRecord>(source)
                : new List<DungeonSpawnRecord>();
            values.Sort(CompareSpawns);
            writer.WriteInt(values.Count);
            for (int i = 0; i < values.Count; i++)
                WriteSpawn(writer, values[i]);
        }

        // 콘텐츠 교체 명령을 target ID 순으로 정렬해 대체 key와 함께 기록합니다.
        private static void WriteContent(
            DungeonCanonicalWriter writer,
            List<DungeonSpawnContentOverride> source)
        {
            List<DungeonSpawnContentOverride> values = source != null
                ? new List<DungeonSpawnContentOverride>(source)
                : new List<DungeonSpawnContentOverride>();
            values.Sort(CompareContentOperations);
            writer.WriteInt(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnContentOverride value = values[i];
                writer.WriteBool(value != null);
                if (value == null) continue;
                WriteBinding(writer, value.binding);
                writer.WriteString(value.replacementContentKey);
            }
        }

        // 절대 transform 명령을 target ID 순으로 정렬해 모든 위치·회전·scale 값을 기록합니다.
        private static void WriteTransforms(
            DungeonCanonicalWriter writer,
            List<DungeonSpawnTransformOverride> source)
        {
            List<DungeonSpawnTransformOverride> values = source != null
                ? new List<DungeonSpawnTransformOverride>(source)
                : new List<DungeonSpawnTransformOverride>();
            values.Sort(CompareTransformOperations);
            writer.WriteInt(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnTransformOverride value = values[i];
                writer.WriteBool(value != null);
                if (value == null) continue;
                WriteBinding(writer, value.binding);
                writer.WriteVector3(value.localPosition);
                writer.WriteFloat(value.pitchDegrees);
                writer.WriteFloat(value.yawDegrees);
                writer.WriteFloat(value.rollDegrees);
                writer.WriteVector3(value.localScale);
            }
        }

        // 재결합용 binding의 stable ID와 의미 anchor를 고정 순서로 기록합니다.
        private static void WriteBinding(
            DungeonCanonicalWriter writer,
            DungeonSpawnBindingSnapshot binding)
        {
            writer.WriteBool(binding != null);
            if (binding == null) return;
            writer.WriteString(binding.spawnId);
            writer.WriteInt((int)binding.category);
            writer.WriteString(binding.contentKey);
            writer.WriteVector2Int(binding.cell);
            writer.WriteString(binding.roomId);
            writer.WriteInt(binding.variantSeed);
        }

        // 추가 spawn의 Blueprint 논리 필드를 원본 hasher와 같은 순서로 기록합니다.
        private static void WriteSpawn(
            DungeonCanonicalWriter writer,
            DungeonSpawnRecord spawn)
        {
            writer.WriteBool(spawn != null);
            if (spawn == null) return;
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
            List<string> tags = spawn.tags != null
                ? new List<string>(spawn.tags)
                : new List<string>();
            tags.Sort(StringComparer.Ordinal);
            writer.WriteInt(tags.Count);
            for (int i = 0; i < tags.Count; i++) writer.WriteString(tags[i]);
            writer.WriteInt(spawn.variantSeed);
        }

        // binding을 stable target ID와 의미 anchor 순으로 비교합니다.
        private static int CompareBindings(
            DungeonSpawnBindingSnapshot left,
            DungeonSpawnBindingSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.spawnId, right.spawnId);
            if (result != 0) return result;
            result = ((int)left.category).CompareTo((int)right.category);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.contentKey, right.contentKey);
            if (result != 0) return result;
            result = left.cell.y.CompareTo(right.cell.y);
            if (result != 0) return result;
            result = left.cell.x.CompareTo(right.cell.x);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.roomId, right.roomId);
            return result != 0
                ? result
                : left.variantSeed.CompareTo(right.variantSeed);
        }

        // 콘텐츠 작업을 binding 뒤 replacement key까지 비교해 잘못된 중복 데이터도 순서 독립적으로 hash합니다.
        private static int CompareContentOperations(
            DungeonSpawnContentOverride left,
            DungeonSpawnContentOverride right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = CompareBindings(left.binding, right.binding);
            return result != 0
                ? result
                : string.CompareOrdinal(
                    left.replacementContentKey,
                    right.replacementContentKey);
        }

        // Transform 작업을 binding 뒤 기록되는 모든 절대값까지 비교해 canonical 순서를 완성합니다.
        private static int CompareTransformOperations(
            DungeonSpawnTransformOverride left,
            DungeonSpawnTransformOverride right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = CompareBindings(left.binding, right.binding);
            if (result != 0) return result;
            result = CompareVector3(left.localPosition, right.localPosition);
            if (result != 0) return result;
            result = left.pitchDegrees.CompareTo(right.pitchDegrees);
            if (result != 0) return result;
            result = left.yawDegrees.CompareTo(right.yawDegrees);
            if (result != 0) return result;
            result = left.rollDegrees.CompareTo(right.rollDegrees);
            return result != 0
                ? result
                : CompareVector3(left.localScale, right.localScale);
        }

        // 추가 spawn을 기록되는 모든 논리 필드로 비교해 동일 ID의 손상 데이터도 안정적으로 정렬합니다.
        private static int CompareSpawns(
            DungeonSpawnRecord left,
            DungeonSpawnRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.spawnId, right.spawnId);
            if (result != 0) return result;
            result = ((int)left.category).CompareTo((int)right.category);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.contentKey, right.contentKey);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.instanceName, right.instanceName);
            if (result != 0) return result;
            result = left.cell.y.CompareTo(right.cell.y);
            if (result != 0) return result;
            result = left.cell.x.CompareTo(right.cell.x);
            if (result != 0) return result;
            result = CompareVector3(left.localPosition, right.localPosition);
            if (result != 0) return result;
            result = left.pitchDegrees.CompareTo(right.pitchDegrees);
            if (result != 0) return result;
            result = left.yawDegrees.CompareTo(right.yawDegrees);
            if (result != 0) return result;
            result = left.rollDegrees.CompareTo(right.rollDegrees);
            if (result != 0) return result;
            result = CompareVector3(left.localScale, right.localScale);
            if (result != 0) return result;
            result = string.CompareOrdinal(left.roomId, right.roomId);
            if (result != 0) return result;
            result = left.progression.CompareTo(right.progression);
            if (result != 0) return result;
            result = CompareTags(left.tags, right.tags);
            return result != 0
                ? result
                : left.variantSeed.CompareTo(right.variantSeed);
        }

        // Vector3를 x·y·z 순서로 비교합니다.
        private static int CompareVector3(
            UnityEngine.Vector3 left,
            UnityEngine.Vector3 right)
        {
            int result = left.x.CompareTo(right.x);
            if (result != 0) return result;
            result = left.y.CompareTo(right.y);
            return result != 0 ? result : left.z.CompareTo(right.z);
        }

        // tag 목록을 ordinal 정렬한 뒤 개수와 각 문자열로 비교합니다.
        private static int CompareTags(
            List<string> left,
            List<string> right)
        {
            List<string> leftValues = left != null
                ? new List<string>(left)
                : new List<string>();
            List<string> rightValues = right != null
                ? new List<string>(right)
                : new List<string>();
            leftValues.Sort(StringComparer.Ordinal);
            rightValues.Sort(StringComparer.Ordinal);
            int count = Math.Min(leftValues.Count, rightValues.Count);
            for (int i = 0; i < count; i++)
            {
                int result = string.CompareOrdinal(
                    leftValues[i],
                    rightValues[i]);
                if (result != 0) return result;
            }
            return leftValues.Count.CompareTo(rightValues.Count);
        }
    }
}
