"""Room feature source/schema/UPM parity checks; Unity tests supply behavioral evidence."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
arena=root/'Assets/RogueDungeonLab/Runtime/Arena'
core=root/'UpmPackages/com.dntlr2000.rogue-dungeon-lab.core/Runtime/Arena'
for p in arena.glob('*'):
    if p.is_file():
        assert p.read_bytes()==(core/p.name).read_bytes(),p
        if p.suffix=='.cs':
            s=p.read_text(encoding='utf-8-sig')
            assert 'UnityEditor' not in s,p
            assert not re.search(r'\b(?:UnityEngine\.)?Random\.(?:Range|value|InitState)',s),p
for suffix in ('','.meta'):
    assert (root/('Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs'+suffix)).read_bytes()==(root/('UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab/Editor/FpsArenaWindow.cs'+suffix)).read_bytes()
cases=json.loads((root/'Assets/RogueDungeonLab/Tests/EditMode/FpsArenaRoomsOffBaseline.json').read_text(encoding='utf-8-sig'))['cases']
assert len(cases)==24
expected=list(json.loads(cases[0]['recipeJson']))
snapshot=(arena/'FpsArenaWalls.cs').read_text(encoding='utf-8-sig').split('internal sealed class FpsArenaWallRecipeSnapshot')[1].split('}',1)[0]
fields=[]
for m in re.finditer(r'public\s+(?:int|float|bool|FpsArenaShape|FpsArenaGeneratorVersion|FpsArenaCoverShapes)\s+([^;]+);',snapshot):fields.extend(x.strip() for x in m.group(1).split(','))
assert fields==expected,(fields,expected)
planner=(arena/'FpsArenaRoomPlanner.cs').read_text(encoding='utf-8-sig')
for token in ['RoomStreamV2','ClosedRoomEdges.Add','SolidRoomEdges.Add','ReserveRoomRoute','RoomHasMinimumSpan','actual=regions.Count','RoomConnections.Add']:assert token in planner,token
layout=(arena/'FpsArenaRooms.cs').read_text(encoding='utf-8-sig')
assert 'CountClosedRooms(report.floor)' in layout
ui=(root/'Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs').read_text(encoding='utf-8-sig')
for token in ['방 구획 사용','층별 방 개수','방 실제 / 요청','출입구 연결','이전 벽 조각 설정 (호환)','OnSceneGUI']:assert token in ui,token
print('PASS: Runtime boundary, source/GUID UPM parity, 24 pre-room wall/OFF schemas')
print('PASS: explicit room regions, boundary/door graph, minimum footprint span, protected routes and Korean room-count UI')
print('Actual room counts, Physics, GUI and reload: see room verification report')
