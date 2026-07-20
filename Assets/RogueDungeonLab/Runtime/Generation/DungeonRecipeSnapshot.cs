using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    [Serializable]
    public sealed class DungeonCurveKeySnapshot
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        public float inWeight;
        public float outWeight;
        public WeightedMode weightedMode;

        // Unity Keyframe의 결정 관련 값을 직렬화 가능한 레코드로 복사합니다.
        public static DungeonCurveKeySnapshot Capture(Keyframe key)
        {
            return new DungeonCurveKeySnapshot
            {
                time = key.time,
                value = key.value,
                inTangent = key.inTangent,
                outTangent = key.outTangent,
                inWeight = key.inWeight,
                outWeight = key.outWeight,
                weightedMode = key.weightedMode
            };
        }

        // 저장된 곡선 키를 독립적인 Unity Keyframe 값으로 복원합니다.
        public Keyframe ToKeyframe()
        {
            Keyframe key = new Keyframe(time, value, inTangent, outTangent)
            {
                inWeight = inWeight,
                outWeight = outWeight,
                weightedMode = weightedMode
            };
            return key;
        }
    }

    [Serializable]
    public sealed class DungeonCurveSnapshot
    {
        public WrapMode preWrapMode = WrapMode.ClampForever;
        public WrapMode postWrapMode = WrapMode.ClampForever;
        public List<DungeonCurveKeySnapshot> keys = new List<DungeonCurveKeySnapshot>();

        // 비어 있거나 null인 곡선은 기존 설정 검증과 같은 선형 기본 곡선으로 정규화합니다.
        public static DungeonCurveSnapshot Capture(AnimationCurve curve)
        {
            AnimationCurve source = curve;
            if (source == null || source.length == 0)
            {
                source = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            }

            DungeonCurveSnapshot snapshot = new DungeonCurveSnapshot
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
            Keyframe[] sourceKeys = source.keys;
            for (int i = 0; i < sourceKeys.Length; i++)
            {
                snapshot.keys.Add(DungeonCurveKeySnapshot.Capture(sourceKeys[i]));
            }
            return snapshot;
        }

        // 스냅샷 키와 wrap mode로 새 AnimationCurve 인스턴스를 만듭니다.
        public AnimationCurve ToAnimationCurve()
        {
            List<DungeonCurveKeySnapshot> sourceKeys = keys ?? new List<DungeonCurveKeySnapshot>();
            Keyframe[] restoredKeys = new Keyframe[sourceKeys.Count];
            for (int i = 0; i < sourceKeys.Count; i++)
            {
                DungeonCurveKeySnapshot key = sourceKeys[i];
                restoredKeys[i] = key != null ? key.ToKeyframe() : new Keyframe();
            }

            if (restoredKeys.Length == 0)
            {
                restoredKeys = AnimationCurve.Linear(0f, 1f, 1f, 1f).keys;
            }

            return new AnimationCurve(restoredKeys)
            {
                preWrapMode = preWrapMode,
                postWrapMode = postWrapMode
            };
        }
    }

    [Serializable]
    public sealed class DungeonDensityProfileSnapshot
    {
        public float baseDensity;
        public DungeonCurveSnapshot overProgression = new DungeonCurveSnapshot();
        public float roomBias;
        public float clustering;
        public int maxCount;

        // 밀도 프로필을 원본 수정 없이 기존 ClampValues 규칙과 같은 범위로 정규화합니다.
        public static DungeonDensityProfileSnapshot Capture(DensityProfile profile, DensityProfile fallback)
        {
            DensityProfile source = profile ?? fallback ?? new DensityProfile();
            return new DungeonDensityProfileSnapshot
            {
                baseDensity = Mathf.Clamp(source.baseDensity, 0f, 0.5f),
                overProgression = DungeonCurveSnapshot.Capture(source.overProgression),
                roomBias = Mathf.Clamp01(source.roomBias),
                clustering = Mathf.Clamp01(source.clustering),
                maxCount = Mathf.Max(0, source.maxCount)
            };
        }
    }

    [Serializable]
    public sealed class DungeonRecipeSnapshot
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public int stageWidthCells;
        public int stageDepthCells;
        public float cellSize;
        public float wallHeight;
        public int desiredRoomCount;
        public Vector2Int roomSizeMin;
        public Vector2Int roomSizeMax;
        public int roomPlacementAttempts;
        public int corridorWidthCells;
        public float extraConnectionChance;
        public int specialGimmickCount;
        public int contentSpacingCells;
        public int reservedEntranceRadiusCells;
        public DungeonDensityProfileSnapshot enemyProfile = new DungeonDensityProfileSnapshot();
        public DungeonDensityProfileSnapshot destructibleProfile = new DungeonDensityProfileSnapshot();
        public DungeonDensityProfileSnapshot propProfile = new DungeonDensityProfileSnapshot();

        // 생성에 영향을 주는 설정만 읽고 정규화해 변경 불가능한 요청용 값 묶음을 만듭니다.
        public static DungeonRecipeSnapshot Capture(RogueDungeonSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            int width = Mathf.Clamp(settings.stageWidthCells, 12, 96);
            int depth = Mathf.Clamp(settings.stageDepthCells, 12, 96);
            Vector2Int minimum = new Vector2Int(
                Mathf.Clamp(settings.roomSizeMin.x, 3, Mathf.Max(3, width - 4)),
                Mathf.Clamp(settings.roomSizeMin.y, 3, Mathf.Max(3, depth - 4)));
            Vector2Int maximum = new Vector2Int(
                Mathf.Clamp(settings.roomSizeMax.x, minimum.x, Mathf.Max(minimum.x, width - 4)),
                Mathf.Clamp(settings.roomSizeMax.y, minimum.y, Mathf.Max(minimum.y, depth - 4)));

            return new DungeonRecipeSnapshot
            {
                formatVersion = CurrentFormatVersion,
                stageWidthCells = width,
                stageDepthCells = depth,
                cellSize = Mathf.Clamp(settings.cellSize, 1.5f, 6f),
                wallHeight = Mathf.Clamp(settings.wallHeight, 1.5f, 8f),
                desiredRoomCount = Mathf.Clamp(settings.desiredRoomCount, 2, 40),
                roomSizeMin = minimum,
                roomSizeMax = maximum,
                roomPlacementAttempts = Mathf.Clamp(settings.roomPlacementAttempts, 5, 100),
                corridorWidthCells = Mathf.Clamp(settings.corridorWidthCells, 1, 4),
                extraConnectionChance = Mathf.Clamp01(settings.extraConnectionChance),
                specialGimmickCount = Mathf.Clamp(settings.specialGimmickCount, 0, 30),
                contentSpacingCells = Mathf.Clamp(settings.contentSpacingCells, 0, 4),
                reservedEntranceRadiusCells = Mathf.Clamp(settings.reservedEntranceRadiusCells, 0, 8),
                enemyProfile = DungeonDensityProfileSnapshot.Capture(settings.enemyProfile, DensityProfile.EnemyDefault()),
                destructibleProfile = DungeonDensityProfileSnapshot.Capture(settings.destructibleProfile, DensityProfile.DestructibleDefault()),
                propProfile = DungeonDensityProfileSnapshot.Capture(settings.propProfile, DensityProfile.PropDefault())
            };
        }

        // 정규화된 레시피 값의 순서 독립적인 SHA-256 지문을 계산합니다.
        public string ComputeHash()
        {
            return DungeonRecipeHasher.Compute(this);
        }
    }
}
