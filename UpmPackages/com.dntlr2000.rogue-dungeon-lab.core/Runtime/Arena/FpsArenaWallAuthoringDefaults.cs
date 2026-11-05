using System;
using UnityEngine;

namespace RogueDungeonLab
{
    // 제작 도구의 새 벽 규격이며 실제 배치 데이터/해시나 런타임 구축에는 포함하지 않는다.
    [Serializable]
    public sealed class FpsArenaWallAuthoringDefaults
    {
        public bool automaticInteriorHeight = true;
        public float interiorHeight = 3.75f, interiorThickness = .25f;
        public float boundaryHeight = FpsArenaBoundaryWalls.DefaultHeight, boundaryThickness = FpsArenaBoundaryWalls.DefaultThickness;

        // 입력 UI와 사본 저장이 원본 메타데이터를 공유하지 않도록 복제한다.
        public FpsArenaWallAuthoringDefaults Clone()
        { return JsonUtility.FromJson<FpsArenaWallAuthoringDefaults>(JsonUtility.ToJson(this)); }

        // 자동 높이는 앞으로 배치하는 내부벽에만 현재 층간 높이를 사용한다.
        public float InteriorHeight(float floorHeight)
        { return automaticInteriorHeight ? floorHeight - .25f : interiorHeight; }
    }
}
