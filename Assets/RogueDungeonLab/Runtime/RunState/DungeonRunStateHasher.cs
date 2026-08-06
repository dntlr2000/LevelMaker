using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonRunStateHasher
    {
        // 저장 시각과 기존 hash를 제외하고 목록 순서에 독립적인 canonical SHA-256을 계산합니다.
        public static string Compute(DungeonRunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            using (DungeonCanonicalWriter writer =
                   new DungeonCanonicalWriter())
            {
                writer.WriteString("RDL_RUN_STATE");
                writer.WriteInt(state.formatVersion);
                writer.WriteString(state.stageId);
                writer.WriteInt((int)state.sourceMode);
                writer.WriteInt(state.runSeed);
                writer.WriteString(state.finalBlueprintHash);
                WriteRemovedSpawns(writer, state.removedSpawnIds);
                WriteGimmickStates(writer, state.gimmickStates);
                WritePlayer(writer, state.player);
                return writer.FinishHash();
            }
        }

        // 제거 ID를 ordinal 정렬해 Inspector·JSON 목록 순서의 영향을 제거합니다.
        private static void WriteRemovedSpawns(
            DungeonCanonicalWriter writer,
            List<string> source)
        {
            List<string> values = source != null
                ? new List<string>(source)
                : new List<string>();
            values.Sort(StringComparer.Ordinal);
            writer.WriteBool(source != null);
            writer.WriteInt(values.Count);
            for (int i = 0; i < values.Count; i++)
                writer.WriteString(values[i]);
        }

        // 기믹 레코드를 spawn ID·participant key·payload 순으로 정렬해 기록합니다.
        private static void WriteGimmickStates(
            DungeonCanonicalWriter writer,
            List<DungeonGimmickRunState> source)
        {
            List<DungeonGimmickRunState> values = source != null
                ? new List<DungeonGimmickRunState>(source)
                : new List<DungeonGimmickRunState>();
            values.Sort(CompareGimmickStates);
            writer.WriteBool(source != null);
            writer.WriteInt(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonGimmickRunState value = values[i];
                writer.WriteBool(value != null);
                if (value == null) continue;
                writer.WriteString(value.spawnId);
                writer.WriteString(value.stateKey);
                writer.WriteString(value.payload);
            }
        }

        // 선택적 플레이어 pose를 stage-local 위치와 회전으로 기록합니다.
        private static void WritePlayer(
            DungeonCanonicalWriter writer,
            DungeonRunPlayerState player)
        {
            writer.WriteBool(player != null);
            if (player == null) return;
            writer.WriteBool(player.isPresent);
            writer.WriteVector3(player.localPosition);
            writer.WriteVector3(player.localEulerAngles);
        }

        // null을 포함한 기믹 상태를 canonical 본문 값 전체로 비교합니다.
        private static int CompareGimmickStates(
            DungeonGimmickRunState left,
            DungeonGimmickRunState right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(
                left.spawnId,
                right.spawnId);
            if (result != 0) return result;
            result = string.CompareOrdinal(
                left.stateKey,
                right.stateKey);
            return result != 0
                ? result
                : string.CompareOrdinal(left.payload, right.payload);
        }
    }

    public static class DungeonRunStateSerialization
    {
        // RunState를 Unity Object 참조가 없는 JSON 문자열로 직렬화합니다.
        public static string ToJson(
            DungeonRunState state,
            bool prettyPrint = true)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return JsonUtility.ToJson(state, prettyPrint);
        }

        // JSON 문자열을 RunState DTO로 역직렬화하고 형식 오류를 명시적으로 보고합니다.
        public static DungeonRunState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("RunState JSON is empty.");
            DungeonRunState state;
            try
            {
                state = JsonUtility.FromJson<DungeonRunState>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException(
                    "RunState JSON could not be parsed.",
                    exception);
            }
            if (state == null)
                throw new FormatException(
                    "RunState JSON did not produce an object.");
            EnsureCollections(state);
            return state;
        }

        // JSON round-trip으로 목록과 중첩 DTO를 포함한 깊은 복사본을 만듭니다.
        public static DungeonRunState DeepClone(
            DungeonRunState state)
        {
            if (state == null) return null;
            return FromJson(ToJson(state, false));
        }

        // 이전 JSON이나 수동 입력에서 null인 선택적 컬렉션을 빈 값으로 정규화합니다.
        private static void EnsureCollections(DungeonRunState state)
        {
            if (state.removedSpawnIds == null)
                state.removedSpawnIds = new List<string>();
            if (state.gimmickStates == null)
                state.gimmickStates =
                    new List<DungeonGimmickRunState>();
            if (state.player == null)
                state.player = new DungeonRunPlayerState();
        }
    }
}
