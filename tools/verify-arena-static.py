"""Cloud-only independent geometry/property checks. Not a C# or Unity test runner."""
from pathlib import Path
import hashlib, json, math, re
ROOT=Path(__file__).resolve().parents[1]

def inside(w,d,shape,x,z):
    if not (0<=x<w and 0<=z<d): return False
    nx=abs((x+.5)*2/w-1); nz=abs((z+.5)*2/d-1)
    return nx*nx+nz*nz<=1 if shape=='Ellipse' else nx+nz<=1.55 if shape=='Octagon' else True

checks=0
for w in range(20,65):
 for d in range(20,65):
  for shape in ('Rectangle','Ellipse','Octagon'):
   for run in range(1,6):
    front=max(3,d//4-run//2)
    for z0 in (front,d-run-front):
     for x0 in (w//3,w*2//3):
      for x in range(x0,x0+2):
       for z in range(z0-1,z0+run+1):
        assert inside(w,d,shape,x,z),(w,d,shape,run,x,z)
        checks+=1
    assert front+run < d-run-front, (d,run)
print(f'PASS independent shape/stair footprint and full landing sweep: {checks} cells')
for cell in (2,3,5):
 for height in (3,4,6):
  run=math.ceil(height/(cell*.6));steps=math.ceil(height/.18)
  assert height/steps<=.1800001
  assert height/(run*cell)<=.6000001
  assert height-.2>=2.8-1e-8
print('PASS independent stair riser, slope, slab headroom formulas')
for path in (ROOT/'Assets/RogueDungeonLab/Runtime/Arena').glob('*.cs'):
 s=path.read_text()
 assert not re.search(r'\bUnityEditor\b|\bUnityEngine\.Random\b|\bRandom\.(Range|value)',s),path
 # Strip strings/comments for structural delimiter sanity, not a compiler.
 s=re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"', '', s, flags=re.S)
 stack=[]
 for ch in s:
  if ch in '{([':stack.append(ch)
  elif ch in '})]': assert stack and stack.pop()=={'}':'{',')':'(',']':'['}[ch],path
 assert not stack,path
print('PASS Runtime boundary/global RNG and lexical delimiter checks')
for path in ROOT.rglob('*.asmdef'):json.loads(path.read_text(encoding='utf-8-sig'))
print('PASS asmdef JSON parsing')
for p in (ROOT/'Assets/RogueDungeonLab/Runtime/Arena').glob('*'):
 if p.is_file(): assert p.read_bytes()==(ROOT/'UpmPackages/com.dntlr2000.rogue-dungeon-lab.core/Runtime/Arena'/p.name).read_bytes(),p
for suffix in ('','.meta'):
 assert (ROOT/('Assets/RogueDungeonLab/Samples/Lab/FpsArenaPlayer.cs'+suffix)).read_bytes()==(ROOT/('UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab/Runtime/FpsArenaPlayer.cs'+suffix)).read_bytes()
print('PASS new UPM Runtime/Lab source and metadata byte parity')
manifest=json.loads((ROOT/'_handoff/source_manifest.json').read_text())
protected=('Assets/RogueDungeonLab/Runtime/Generation/','Assets/RogueDungeonLab/Runtime/Blueprint/', 'Assets/RogueDungeonLab/Runtime/Loading/')
for f in manifest['files']:
 if f['path'].startswith(protected) or f['path'] in ('Assets/RogueDungeonLab/Runtime/RogueDungeonGeneration.cs','Assets/RogueDungeonLab/Runtime/RogueDungeonData.cs'):
  assert hashlib.sha256((ROOT/f['path']).read_bytes()).hexdigest()==f['sha256'],f['path']
print('PASS original roguelike generation/blueprint/loading source byte identity')
print('NOT RUN: C# compilation, Unity EditMode/PlayMode, Physics traversal, scene reload or UI rendering')
