using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonRunStateFormat
    {
        public const int CurrentVersion = 1;
    }

    public enum DungeonRunStateHashMismatchPolicy
    {
        Reject = 0,
        ApplyMatchingSpawnIds = 1
    }

    [Serializable]
    public sealed class DungeonRunPlayerState
    {
        public bool isPresent;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;

        // 플레이어 pose를 값 단위로 복사해 저장본과 런타임 편집본을 분리합니다.
        public DungeonRunPlayerState DeepClone()
        {
            return new DungeonRunPlayerState
            {
                isPresent = isPresent,
                localPosition = localPosition,
                localEulerAngles = localEulerAngles
            };
        }
    }

    [Serializable]
    public sealed class DungeonGimmickRunState
    {
        public string spawnId = string.Empty;
        public string stateKey = string.Empty;
        public string payload = string.Empty;

        // 기믹 상태 레코드를 문자열 payload까지 독립 복사합니다.
        public DungeonGimmickRunState DeepClone()
        {
            return new DungeonGimmickRunState
            {
                spawnId = spawnId ?? string.Empty,
                stateKey = stateKey ?? string.Empty,
                payload = payload ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class DungeonRunState
    {
        public int formatVersion = DungeonRunStateFormat.CurrentVersion;
        public string stageId = string.Empty;
        public DungeonStageSourceMode sourceMode;
        public int runSeed;
        public string finalBlueprintHash = string.Empty;
        public List<string> removedSpawnIds = new List<string>();
        public List<DungeonGimmickRunState> gimmickStates =
            new List<DungeonGimmickRunState>();
        public DungeonRunPlayerState player = new DungeonRunPlayerState();
        public long savedUtcTicks;
        public string stateHash = string.Empty;

        // 현재 값에서 저장 시각을 제외한 canonical 상태 hash를 다시 계산합니다.
        public void RefreshHash()
        {
            stateHash = DungeonRunStateHasher.Compute(this);
        }

        // JSON 직렬화 round-trip으로 모든 목록과 중첩 DTO가 분리된 복사본을 만듭니다.
        public DungeonRunState DeepClone()
        {
            return DungeonRunStateSerialization.DeepClone(this);
        }

        // 제거된 stable spawn ID를 중복 없이 기록하고 변경 여부를 반환합니다.
        public bool AddRemovedSpawn(string spawnId)
        {
            string normalized = spawnId != null
                ? spawnId.Trim()
                : string.Empty;
            if (string.IsNullOrEmpty(normalized)) return false;
            if (removedSpawnIds == null)
                removedSpawnIds = new List<string>();
            for (int i = 0; i < removedSpawnIds.Count; i++)
            {
                if (string.Equals(
                        removedSpawnIds[i],
                        normalized,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            removedSpawnIds.Add(normalized);
            RefreshHash();
            return true;
        }

        // spawn ID와 participant key 조합의 기믹 payload를 추가하거나 교체합니다.
        public void SetGimmickState(
            string spawnId,
            string stateKey,
            string payload)
        {
            string normalizedSpawn =
                spawnId != null ? spawnId.Trim() : string.Empty;
            string normalizedKey =
                stateKey != null ? stateKey.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalizedSpawn))
                throw new ArgumentException(
                    "A gimmick state requires a spawn ID.",
                    nameof(spawnId));
            if (string.IsNullOrEmpty(normalizedKey))
                throw new ArgumentException(
                    "A gimmick state requires a participant key.",
                    nameof(stateKey));
            if (gimmickStates == null)
                gimmickStates = new List<DungeonGimmickRunState>();
            for (int i = 0; i < gimmickStates.Count; i++)
            {
                DungeonGimmickRunState entry = gimmickStates[i];
                if (entry != null &&
                    string.Equals(
                        entry.spawnId,
                        normalizedSpawn,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        entry.stateKey,
                        normalizedKey,
                        StringComparison.Ordinal))
                {
                    entry.payload = payload ?? string.Empty;
                    RefreshHash();
                    return;
                }
            }
            gimmickStates.Add(new DungeonGimmickRunState
            {
                spawnId = normalizedSpawn,
                stateKey = normalizedKey,
                payload = payload ?? string.Empty
            });
            RefreshHash();
        }
    }

    public sealed class DungeonRunStateSpawnDescriptor
    {
        public string SpawnId { get; private set; }
        public DungeonSpawnCategory Category { get; private set; }

        // 호환성 검증에 필요한 stable ID와 범주만 불변 값으로 보관합니다.
        internal DungeonRunStateSpawnDescriptor(
            string spawnId,
            DungeonSpawnCategory category)
        {
            SpawnId = spawnId ?? string.Empty;
            Category = category;
        }
    }

    public sealed class DungeonRunStateTarget
    {
        private readonly List<DungeonRunStateSpawnDescriptor> _spawns;

        public string StageId { get; private set; }
        public DungeonStageSourceMode SourceMode { get; private set; }
        public int RunSeed { get; private set; }
        public string FinalBlueprintHash { get; private set; }
        public IReadOnlyList<DungeonRunStateSpawnDescriptor> Spawns
        {
            get { return _spawns; }
        }

        // 로드 대상의 식별자·seed·최종 hash와 spawn 계약을 하나의 migration 입력으로 묶습니다.
        internal DungeonRunStateTarget(
            string stageId,
            DungeonStageSourceMode sourceMode,
            int runSeed,
            string finalBlueprintHash,
            List<DungeonRunStateSpawnDescriptor> spawns)
        {
            StageId = stageId ?? string.Empty;
            SourceMode = sourceMode;
            RunSeed = runSeed;
            FinalBlueprintHash = finalBlueprintHash ?? string.Empty;
            _spawns = spawns ?? new List<DungeonRunStateSpawnDescriptor>();
        }
    }

    public interface IDungeonRunStateMigrator
    {
        // 이전 상태를 현재 로드 대상 계약에 맞춘 새 상태로 변환할 수 있는지 시도합니다.
        bool TryMigrate(
            DungeonRunState source,
            DungeonRunStateTarget target,
            out DungeonRunState migrated,
            out string message);
    }

    public interface IDungeonRunStateParticipant
    {
        // 같은 spawn 안에서 상태 레코드를 구분하는 안정적인 participant key를 반환합니다.
        string RunStateKey { get; }

        // 현재 기믹 진행 상태를 Unity Object 참조가 없는 문자열 payload로 캡처합니다.
        string CaptureRunState();

        // 저장된 문자열 payload를 현재 기믹 인스턴스에 복원합니다.
        void RestoreRunState(string payload);
    }
}
