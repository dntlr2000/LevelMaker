using System;
using UnityEngine;

namespace RogueDungeonLab
{
    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Dungeon Blueprint", fileName = "DungeonBlueprint")]
    public sealed class DungeonBlueprintAsset : ScriptableObject
    {
        public DungeonBlueprint blueprint = new DungeonBlueprint();

        // 검증된 메모리 Blueprint를 자산 소유의 깊은 복사본으로 저장하고 해시를 갱신합니다.
        public void Store(DungeonBlueprint source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            DungeonBlueprint copy = source.DeepClone();
            if (copy.createdUtcTicks == 0L) copy.createdUtcTicks = DateTime.UtcNow.Ticks;
            copy.RefreshHash();
            blueprint = copy;
        }
    }
}
