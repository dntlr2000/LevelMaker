using System;
using System.Collections.Generic;

namespace RogueDungeonLab
{
    public static class DungeonContentCatalogHasher
    {
        // 선택에 영향을 주는 순수 planning 필드만 canonical 순서로 해시합니다.
        public static string Compute(DungeonContentCatalogPlanningSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            using (DungeonCanonicalWriter writer = new DungeonCanonicalWriter())
            {
                writer.WriteString("RDL_CONTENT_CATALOG_PLANNING");
                writer.WriteInt(snapshot.formatVersion);
                List<DungeonContentPlanningEntry> entries = GetCanonicalEntries(snapshot.entries);
                writer.WriteBool(snapshot.entries != null);
                writer.WriteInt(entries.Count);
                for (int i = 0; i < entries.Count; i++) WriteEntry(writer, entries[i]);
                return writer.FinishHash();
            }
        }

        // Planner와 hash가 같은 후보 순서를 사용하도록 전체 payload 기준 복사본을 반환합니다.
        public static List<DungeonContentPlanningEntry> GetCanonicalEntries(
            List<DungeonContentPlanningEntry> source)
        {
            List<DungeonContentPlanningEntry> entries = source != null
                ? new List<DungeonContentPlanningEntry>(source)
                : new List<DungeonContentPlanningEntry>();
            entries.Sort(CompareEntries);
            return entries;
        }

        private static void WriteEntry(DungeonCanonicalWriter writer, DungeonContentPlanningEntry entry)
        {
            writer.WriteBool(entry != null);
            if (entry == null) return;
            writer.WriteString(entry.contentKey);
            writer.WriteInt((int)entry.category);
            writer.WriteFloat(entry.weight);
            writer.WriteFloat(entry.minProgression);
            writer.WriteFloat(entry.maxProgression);
            writer.WriteInt((int)entry.placement);
            WriteTags(writer, entry.requiredRoomTags);
            writer.WriteVector2Int(entry.footprintCells);
            writer.WriteInt(entry.minimumSpacingCells);
            writer.WriteBool(entry.randomizeYaw);
            writer.WriteFloat(entry.yawDegreesRange.x);
            writer.WriteFloat(entry.yawDegreesRange.y);
            writer.WriteFloat(entry.uniformScaleRange.x);
            writer.WriteFloat(entry.uniformScaleRange.y);
        }

        private static void WriteTags(DungeonCanonicalWriter writer, List<string> source)
        {
            List<string> tags = source != null ? new List<string>(source) : new List<string>();
            tags.Sort(StringComparer.Ordinal);
            writer.WriteBool(source != null);
            writer.WriteInt(tags.Count);
            for (int i = 0; i < tags.Count; i++) writer.WriteString(tags[i]);
        }

        // 중복 key도 Inspector 순서에 의존하지 않도록 모든 planning 필드에 total order를 정의합니다.
        private static int CompareEntries(DungeonContentPlanningEntry left, DungeonContentPlanningEntry right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.contentKey, right.contentKey);
            if (result != 0) return result;
            result = ((int)left.category).CompareTo((int)right.category);
            if (result != 0) return result;
            result = left.weight.CompareTo(right.weight);
            if (result != 0) return result;
            result = left.minProgression.CompareTo(right.minProgression);
            if (result != 0) return result;
            result = left.maxProgression.CompareTo(right.maxProgression);
            if (result != 0) return result;
            result = ((int)left.placement).CompareTo((int)right.placement);
            if (result != 0) return result;
            result = CompareTags(left.requiredRoomTags, right.requiredRoomTags);
            if (result != 0) return result;
            result = left.footprintCells.x.CompareTo(right.footprintCells.x);
            if (result != 0) return result;
            result = left.footprintCells.y.CompareTo(right.footprintCells.y);
            if (result != 0) return result;
            result = left.minimumSpacingCells.CompareTo(right.minimumSpacingCells);
            if (result != 0) return result;
            result = left.randomizeYaw.CompareTo(right.randomizeYaw);
            if (result != 0) return result;
            result = left.yawDegreesRange.x.CompareTo(right.yawDegreesRange.x);
            if (result != 0) return result;
            result = left.yawDegreesRange.y.CompareTo(right.yawDegreesRange.y);
            if (result != 0) return result;
            result = left.uniformScaleRange.x.CompareTo(right.uniformScaleRange.x);
            return result != 0 ? result : left.uniformScaleRange.y.CompareTo(right.uniformScaleRange.y);
        }

        private static int CompareTags(List<string> left, List<string> right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            List<string> leftCopy = new List<string>(left);
            List<string> rightCopy = new List<string>(right);
            leftCopy.Sort(StringComparer.Ordinal);
            rightCopy.Sort(StringComparer.Ordinal);
            int result = leftCopy.Count.CompareTo(rightCopy.Count);
            if (result != 0) return result;
            for (int i = 0; i < leftCopy.Count; i++)
            {
                result = string.CompareOrdinal(leftCopy[i], rightCopy[i]);
                if (result != 0) return result;
            }
            return 0;
        }
    }
}
