using UnityEngine;

namespace RogueDungeonLab
{
    /// <summary>
    /// 제품 플레이어나 선택적 실험용 플레이어가 런 상태 pose 저장·복원에 참여하는 계약입니다.
    /// </summary>
    public interface IDungeonRunStatePlayer
    {
        RogueDungeonGenerator RunStateGenerator { get; }

        Transform RunStateTransform { get; }

        // 저장된 stage-local pose를 구현별 이동 컴포넌트의 안전 절차로 복원합니다.
        void RestoreRunStatePose(
            Transform stageTransform,
            DungeonRunPlayerState state);
    }
}
