using UnityEngine;

namespace RogueDungeonLab
{
    [CreateAssetMenu(menuName = "Rogue Dungeon Lab/Bake Material Set", fileName = "DungeonBakeMaterialSet")]
    public sealed class DungeonBakeMaterialSet : ScriptableObject
    {
        [Header("지오메트리")]
        public Material floor;
        public Material wall;

        [Header("내장 콘텐츠")]
        public Material enemy;
        public Material destructible;
        public Material prop;
        public Material gimmick;
        public Material entrance;
        public Material exit;
    }
}
