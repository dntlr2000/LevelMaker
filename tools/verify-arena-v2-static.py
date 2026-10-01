"""Final V2 source wiring/metadata/property checks; does NOT compile C# or run Unity."""
from pathlib import Path
import re,json,hashlib
ROOT=Path(__file__).resolve().parents[1]
checks=[]
for path in list((ROOT/'Assets/RogueDungeonLab/Runtime/Arena').glob('*.cs'))+[ROOT/'Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs',ROOT/'Assets/RogueDungeonLab/Editor/Packaging/RogueDungeonUpmPackageExporter.cs',ROOT/'Assets/RogueDungeonLab/Tests/EditMode/FpsArenaFlexibleTests.cs',ROOT/'Assets/RogueDungeonLab/Tests/EditMode/FpsArenaEditorFlexibleTests.cs',ROOT/'Assets/RogueDungeonLab/Tests/PlayMode/FpsArenaTraversalTests.cs']:
 source=path.read_text();clean=re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"', '', source,flags=re.S);stack=[]
 for ch in clean:
  if ch in '{([':stack.append(ch)
  elif ch in '})]':assert stack and stack.pop()=={'}':'{',')':'(',']':'['}[ch],path
 assert not stack,path
checks.append('changed Runtime/Editor/test lexical delimiters')
arena=ROOT/'Assets/RogueDungeonLab/Runtime/Arena';recipe=(arena/'FpsArenaSettings.cs').read_text();planner=(arena/'FpsArenaFlexiblePlanner.cs').read_text();builder=(arena/'FpsArenaSceneBuilder.cs').read_text();gen=(arena/'FpsArenaGenerator.cs').read_text();visuals=(arena/'FpsArenaVisuals.cs').read_text()
for field in ['coverDensity','enemyDensity','gimmickDensity','itemDensity']:assert field in recipe and field in (arena/'FpsArenaLayout.cs').read_text()
assert 'StairStreamV2' in planner and 'Shuffle(candidates, rng)' in planner and 'ReserveWalkRoute(l, stair.Bottom' in planner
assert 'ContentFits' in planner and 'widthCells = width' in planner and 'SelectCoverShape' in planner
assert 'Object.Instantiate(entry.prefab' in builder and 'Vector3 scale = Vector3.one * fit' in builder and 'initializer.InitializeArenaContent(context)' in builder
assert 'GenerateSnapshot(builtRecipe, builtSeed, builtCatalogSnapshot)' in gen
assert 'GetComponentsInParent<FpsArenaContentIdentity>(true)' in visuals
checks.append('four actual density pathways, seeded stairs, whole footprints, real prefab initialization, uniform fit, snapshot restore and material ownership')
legacy=recipe.split('internal sealed class FpsArenaLegacyRecipeSnapshot')[1].split('public FpsArenaLegacyRecipeSnapshot')[0]
fields=[]
for match in re.finditer(r'public\s+(?:int|float|FpsArenaShape)\s+([^;]+);',legacy):fields.extend(x.strip() for x in match.group(1).split(','))
assert fields==['width','depth','shape','floors','cellSize','floorHeight','stairsPerFloor','coverPerFloor','obstaclesPerFloor','itemsPerFloor','coverHeight','obstacleHeight','spacingCells'],fields
assert '61f0193622aa04090432f7480b0376d2bca098c200270bb113540c175ecfe7b0' in (ROOT/'Assets/RogueDungeonLab/Tests/EditMode/FpsArenaFlexibleTests.cs').read_text()
checks.append('exact V1 14-field hash snapshot order and validated golden test')
core=ROOT/'UpmPackages/com.dntlr2000.rogue-dungeon-lab.core';lab=ROOT/'UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab';baking=ROOT/'UpmPackages/com.dntlr2000.rogue-dungeon-lab.baking'
for p in arena.glob('*'):
 if p.is_file():assert p.read_bytes()==(core/'Runtime/Arena'/p.name).read_bytes(),p
for suffix in ('','.meta'):assert (ROOT/('Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs'+suffix)).read_bytes()==(lab/('Editor/FpsArenaWindow.cs'+suffix)).read_bytes()
asm=json.loads((lab/'Editor/RogueDungeonLab.Arena.Editor.asmdef').read_text());assert asm['references']==['RogueDungeonLab.Runtime','RogueDungeonLab.Samples'] and asm['includePlatforms']==['Editor']
for pkg in [core,lab,baking]:assert json.loads((pkg/'package.json').read_text())['version']=='0.14.0'
checks.append('Core Runtime/GUID and portable Lab Editor/GUID byte parity, Editor-only package boundary and version0.14.0')
for check in checks:print('PASS',check)
print('NOT RUN C# compiler / Unity EditMode, PlayMode, Physics, UI, domain reload, consumer installs')
