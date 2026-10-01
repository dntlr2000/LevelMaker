"""Source/schema boundary check; behavioral evidence comes from actual Unity tests."""
from pathlib import Path
import json,re
r=Path(__file__).resolve().parents[1]
a=r/'Assets/RogueDungeonLab/Runtime/Arena'
core=r/'UpmPackages/com.dntlr2000.rogue-dungeon-lab.core/Runtime/Arena'
for p in a.iterdir():
    if p.is_file():
        assert p.read_bytes()==(core/p.name).read_bytes(),p
        if p.suffix=='.cs':
            s=p.read_text(encoding='utf-8-sig')
            assert 'UnityEditor' not in s,p
            assert not re.search(r'\b(?:UnityEngine\.)?Random\.(?:Range|value|InitState)',s),p
for suffix in ('','.meta'):
    assert (r/('Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs'+suffix)).read_bytes()==(r/('UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab/Editor/FpsArenaWindow.cs'+suffix)).read_bytes()
cases=json.loads((r/'Assets/RogueDungeonLab/Tests/EditMode/FpsArenaStairDirectionsLegacyBaseline.json').read_text(encoding='utf-8-sig'))['cases']
assert len(cases)==18
s=(a/'FpsArenaStairDirections.cs').read_text().split('internal sealed class FpsArenaRoomRecipeSnapshot')[1].split('}',1)[0]
fields=[]
for m in re.finditer(r'public\s+(?:int|float|bool|FpsArenaShape|FpsArenaGeneratorVersion|FpsArenaCoverShapes)\s+([^;]+);',s):fields.extend(x.strip() for x in m.group(1).split(','))
assert fields==list(json.loads(cases[0]['recipeJson']))
for filename in ('FpsArenaFlexiblePlanner.cs','FpsArenaRoomPlanner.cs','FpsArenaRooms.cs'):
    s=(a/filename).read_text(encoding='utf-8-sig')
    assert not re.search(r'(?:stair|s)\.Bottom\s*\+\s*Vector3Int\.right',s),filename
s=(a/'FpsArenaSceneBuilder.cs').read_text(encoding='utf-8-sig')
assert 'Quaternion.Euler(0,90*(int)s.direction,0)' in s
ui=(r/'Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs').read_text(encoding='utf-8-sig')
for token in ('시드별 계단 방향 사용','유효 방향','StairDirectionReports'):assert token in ui
print('PASS: Runtime boundary, Core/Lab source+GUID parity, exact legacy room schema')
print('PASS: cardinal stair/lane axes, rotated protection/landings, scene geometry and Korean direction UI')
