using System;
using UnityEngine;

namespace RogueDungeonLab
{
    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Dungeon Blueprint", fileName = "DungeonBlueprint")]
    public sealed class DungeonBlueprintAsset : ScriptableObject
    {
        public DungeonBlueprint blueprint = new DungeonBlueprint();
        [SerializeField] private bool hasAuthoringRecipeSnapshot;
        [SerializeField] private DungeonRecipeSnapshot authoringRecipeSnapshot;

        public bool HasAuthoringRecipeSnapshot
        {
            get { return hasAuthoringRecipeSnapshot && authoringRecipeSnapshot != null; }
        }

        public string AuthoringRecipeHash
        {
            get
            {
                return HasAuthoringRecipeSnapshot
                    ? authoringRecipeSnapshot.ComputeHash()
                    : string.Empty;
            }
        }

        // 레시피 출처가 없는 기존 호출을 유지하며 Blueprint만 깊은 복사해 저장합니다.
        public void Store(DungeonBlueprint source)
        {
            Store(source, null);
        }

        // Blueprint와 선택적 제작 레시피를 각각 깊은 복사하고 두 recipe hash의 일치를 보장합니다.
        public void Store(
            DungeonBlueprint source,
            DungeonRecipeSnapshot recipeSnapshot)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            DungeonBlueprint copy = source.DeepClone();
            if (copy.createdUtcTicks == 0L) copy.createdUtcTicks = DateTime.UtcNow.Ticks;
            copy.RefreshHash();

            DungeonRecipeSnapshot recipeCopy = null;
            if (recipeSnapshot != null)
            {
                if (recipeSnapshot.formatVersion != DungeonRecipeSnapshot.CurrentFormatVersion)
                    throw new NotSupportedException(
                        "Unsupported authoring recipe snapshot format: " + recipeSnapshot.formatVersion);
                recipeCopy = recipeSnapshot.DeepClone();
                string recipeHash = recipeCopy.ComputeHash();
                if (!string.Equals(recipeHash, copy.recipeHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "Authoring recipe snapshot hash does not match the Blueprint recipe hash.",
                        nameof(recipeSnapshot));
                }
            }

            blueprint = copy;
            authoringRecipeSnapshot = recipeCopy;
            hasAuthoringRecipeSnapshot = recipeCopy != null;
        }

        // 저장된 제작 레시피가 있으면 자산 내부 상태를 노출하지 않는 깊은 복사본을 반환합니다.
        public bool TryGetAuthoringRecipeSnapshot(out DungeonRecipeSnapshot snapshot)
        {
            if (!HasAuthoringRecipeSnapshot)
            {
                snapshot = null;
                return false;
            }
            snapshot = authoringRecipeSnapshot.DeepClone();
            return true;
        }
    }
}
