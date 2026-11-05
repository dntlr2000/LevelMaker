using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RogueDungeonLab
{
    // 외곽과 바닥 구멍의 경계를 난수 없이 일반 경계형 벽으로 계산한다.
    public static class FpsArenaBoundaryWalls
    {
        public const float DefaultHeight = 1.5f;
        public const float DefaultThickness = .15f;
        internal static readonly Color WallColor = new Color(.4f, .43f, .48f);
        private static readonly Vector3Int[] Directions = { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };

        // 수동 데이터의 전체 층에서 내부 구멍을 포함하되 계단 공간과 맞닿은 경계는 제외한다.
        public static List<FpsArenaRoomWall> CreateForStage(FpsArenaStageData data)
        {
            if (data == null || data.floorCells == null || data.stairs == null) throw new ArgumentException("스테이지 바닥/계단 데이터가 없습니다.");
            var floors = new HashSet<Vector3Int>(data.floorCells);
            return Create(data.width, data.depth, data.floors, c => floors.Contains(c), data.stairs);
        }

        // 기존 절차 생성이 끝난 뒤 같은 형상 외곽만 추가하여 배치 계산과 기존 해시에 영향을 주지 않는다.
        internal static void PopulateProcedural(FpsArenaLayout layout)
        {
            var r = layout.Recipe;
            layout.SetBoundaryWalls(Create(r.width, r.depth, r.floors, c => c.y >= 0 && c.y < r.floors && FpsArenaPlanner.InShape(r, c.x, c.z), null));
        }

        // 경계 양쪽 중 실제 바닥이 있는 단면을 찾아 고정 순서와 좌표 기반 ID로 벽을 만든다.
        private static List<FpsArenaRoomWall> Create(int width, int depth, int floors, Func<Vector3Int, bool> hasFloor, List<FpsArenaStair> stairs)
        {
            if (width < 8 || width > 64 || depth < 8 || depth > 64 || floors < 1 || floors > 4) throw new InvalidOperationException("스테이지 경계 계산 범위가 잘못되었습니다.");
            var result = new List<FpsArenaRoomWall>();
            for (int f = 0; f < floors; f++) for (int z = 0; z < depth; z++) for (int x = 0; x < width; x++)
            {
                var cell = new Vector3Int(x, f, z);
                if (!hasFloor(cell)) continue;
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (hasFloor(next) || IsStairSpace(next, stairs)) continue;
                    bool alongX = direction.z != 0;
                    var anchor = direction.x < 0 || direction.z < 0 ? next : cell;
                    result.Add(new FpsArenaRoomWall {
                        id = string.Format(CultureInfo.InvariantCulture, "arena:boundary:v1:{0}:{1}:{2}:{3}", f, anchor.x, anchor.z, alongX ? "z" : "x"),
                        cell = anchor, alongX = alongX, lengthCells = 1, height = DefaultHeight, thickness = DefaultThickness
                    });
                }
            }
            return result;
        }

        // 기존 수동 가드가 제외하던 계단 footprint와 양층 개구부를 동일하게 판별한다.
        internal static bool IsStairSpace(Vector3Int cell, List<FpsArenaStair> stairs)
        {
            if (stairs == null) return false;
            foreach (var stair in stairs)
            {
                if (stair == null) throw new InvalidOperationException("계단 데이터가 없습니다.");
                if ((cell.y == stair.lowerFloor || cell.y == stair.lowerFloor + 1)
                    && cell.x >= stair.x && cell.x < stair.x + stair.FootprintWidth
                    && cell.z >= stair.z && cell.z < stair.z + stair.FootprintDepth) return true;
            }
            return false;
        }

        // 절차 외곽 레코드가 유효한 한쪽 바닥 경계이며 중복/손상되지 않았는지 검사한다.
        internal static void Validate(FpsArenaLayout layout)
        {
            var edges = new HashSet<FpsArenaRoomEdge>(); var ids = new HashSet<string>();
            foreach (var wall in layout.BoundaryWalls)
            {
                if (wall == null || string.IsNullOrWhiteSpace(wall.id) || !ids.Add(wall.id) || wall.lengthCells < 1 || wall.lengthCells > 64
                    || !FpsArenaStageData.Finite(wall.height, .1f, layout.Recipe.floorHeight - .1f)
                    || !FpsArenaStageData.Finite(wall.thickness, .1f, 1)) throw new InvalidOperationException("외곽벽 데이터 또는 치수가 잘못되었습니다.");
                for (int i = 0; i < wall.lengthCells; i++)
                {
                    var a = wall.cell + wall.Step * i; var b = a + wall.Normal;
                    if (layout.HasFloor(a) == layout.HasFloor(b) || !edges.Add(new FpsArenaRoomEdge(a, b))) throw new InvalidOperationException("외곽벽은 한쪽에만 바닥이 있는 경계에 놓으세요: " + a);
                }
            }
        }
    }

    public sealed partial class FpsArenaLayout
    {
        private readonly List<FpsArenaRoomWall> boundaryWalls = new List<FpsArenaRoomWall>();
        public IReadOnlyList<FpsArenaRoomWall> BoundaryWalls { get { return boundaryWalls; } }

        // 로드/계획 단계에서 확정한 벽만 보관하며 SceneBuilder에서 외곽을 다시 만들지 않는다.
        internal void SetBoundaryWalls(IEnumerable<FpsArenaRoomWall> walls)
        { boundaryWalls.Clear(); boundaryWalls.AddRange(walls); }
    }
}
