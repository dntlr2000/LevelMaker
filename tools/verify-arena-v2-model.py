"""Independent model of stair selection and logical footprint checks, NOT C#/Unity execution."""
import math, sys
from collections import deque
from pathlib import Path
MASK=(1<<64)-1

def mix(v):
 v=(v+0x9E3779B97F4A7C15)&MASK;v=((v^(v>>30))*0xBF58476D1CE4E5B9)&MASK;v=((v^(v>>27))*0x94D049BB133111EB)&MASK;return v^(v>>31)
class RNG:
 def __init__(self,seed,stream):
  bits=seed&0xffffffff;initial=mix(((bits<<32)|bits)^stream^0xD1B54A32D192ED03);seq=mix(stream^0x8CB92BA72F3D8DD7)
  self.state=0;self.inc=((seq<<1)|1)&MASK;self.uint();self.state=(self.state+initial)&MASK;self.uint()
 def uint(self):
  p=self.state;self.state=(p*6364136223846793005+self.inc)&MASK;x=(((p>>18)^p)>>27)&0xffffffff;r=p>>59;return ((x>>r)|(x<<((-r)&31)))&0xffffffff
 def integer(self,a,b):
  bound=b-a; threshold=((1<<32)-bound)%bound
  while True:
   n=self.uint()
   if n>=threshold:return a+n%bound
 def flt(self):return (self.uint()>>8)/16777216
 def shuffle(self,a):
  for i in range(len(a)-1,0,-1):
   j=self.integer(0,i+1);a[i],a[j]=a[j],a[i]
def inside(w,d,shape,x,z):
 if not(0<=x<w and 0<=z<d):return False
 nx=abs((x+.5)*2/w-1);nz=abs((z+.5)*2/d-1)
 return nx*nx+nz*nz<=1 if shape=='Ellipse' else nx+nz<=1.55 if shape=='Octagon' else True

def generate(w,d,shape,floors,run,seed,stairs=2,content=False):
 fs={(x,f,z) for f in range(floors) for z in range(d) for x in range(w) if inside(w,d,shape,x,z)};reserved=set();spawns=[];ss=[]
 def reserve(f,x,z,width,depth):
  reserved.update((xx,f,zz) for xx in range(max(0,x),min(w,x+width)) for zz in range(max(0,z),min(d,z+depth)))
 for f in range(floors):
  reserve(f,0,d//2-1,w,2);reserve(f,w//2-1,0,2,d)
  for a in [(w//2-1,f,d//2-3),(w//2,f,d//2+3)]:spawns.append(a);reserve(f,a[0]-1,a[2]-1,3,3)
 for f in range(floors-1):
  cc=[]
  for z in range(2,d-run-1):
   for x in range(1,w-2):
    if (z+run>d//2-1 if f%2==0 else z<d//2+1):continue
    if not all((xx,ff,zz) in fs for ff in [f,f+1] for zz in range(z-1,z+run+1) for xx in range(x-1,x+3)):continue
    if any((xx,ff,zz) in reserved for ff in [f,f+1] for zz in range(z,z+run) for xx in range(x,x+2)):continue
    cc.append((x,z))
  RNG(seed,0x4152454E41535432+f).shuffle(cc);chosen=[]
  for a in cc:
   if stairs==1:chosen=[a];break
   for b in cc:
    if a[0]+3<b[0] or b[0]+3<a[0] or a[1]+run+1<b[1] or b[1]+run+1<a[1]:chosen=[a,b];break
   if chosen:break
  assert len(chosen)==stairs,('cannot fit stairs',w,d,shape,floors,run,seed,f,len(cc))
  for x,z in chosen:
   ss.append((f,x,z,run))
   for ff in [f,f+1]:
    reserve(ff,x-1,z-2,4,run+4)
    fs.difference_update((xxx,ff,zzz) for xxx in range(x,x+2) for zzz in range(z,z+run))
 for f,x,z,run in ss:
  for lane in [0,1]:
   for start in [(x+lane,f,z-1),(x+lane,f+1,z+run)]:
    target=spawns[start[1]*2];q=deque([start]);previous={start:start}
    while q and target not in previous:
     xx,ff,zz=q.popleft()
     for c in [(xx+1,ff,zz),(xx-1,ff,zz),(xx,ff,zz+1),(xx,ff,zz-1)]:
      if c in fs and c not in previous:previous[c]=(xx,ff,zz);q.append(c)
    assert target in previous
    c=target
    while True:
     reserve(c[1],c[0]-1,c[2]-1,3,3)
     if c==start:break
     c=previous[c]
 blocked=set();occupied=set();placements=[]
 if content:
  for kind in [0,3,4,2]:
   for f in range(floors):
    cc=[(x,f,z) for z in range(1,d-1) for x in range(1,w-1) if (x,f,z) in fs and (x,f,z) not in reserved];rng=RNG(seed,0x4152454E41435432+f*16+kind);rng.shuffle(cc);n=0
    for x,ff,z in cc:
     width=rng.integer(1,5) if kind==0 else 1;depth=rng.integer(1,5) if kind==0 else 1
     if not all((x+dx,f,z+dz) in fs for dz in range(-1,depth+1) for dx in range(-1,width+1)):continue
     footprint={(x+dx,f,z+dz) for dz in range(depth) for dx in range(width)}
     if footprint&reserved:continue
     if any((x+dx,f,z+dz) in occupied for dz in range(-1,depth+1) for dx in range(-1,width+1)):continue
     rng.flt();rng.flt();rng.flt();rng.integer(0,2);rng.integer(0,3)
     occupied|=footprint
     if kind==0:blocked|=footprint
     placements.append((kind,(x,f,z),width,depth));n+=1
     if n==100:break
 portals={}
 for f,x,z,run in ss:
  bottom=(x,f,z-1);top=(x,f+1,z+run);assert bottom in fs and top in fs
  portals.setdefault(bottom,[]).append(top);portals.setdefault(top,[]).append(bottom)
  for lane in [0,1]:assert (x+lane,f,z-1) in fs and (x+lane,f+1,z+run) in fs
 q=deque([spawns[0]]);seen={spawns[0]}
 while q:
  x,f,z=q.popleft()
  for c in [(x+1,f,z),(x-1,f,z),(x,f,z+1),(x,f,z-1)]+portals.get((x,f,z),[]):
   if c in fs and c not in blocked and c not in seen:seen.add(c);q.append(c)
 assert len(seen)==len(fs)-len(blocked),('disconnected',w,d,shape,floors,run,seed)
 assert all(c not in blocked for c in spawns)
 return ss,placements

if __name__=='__main__':
 count=0
 for w,d in [(20,20),(20,64),(21,64),(64,20),(64,64),(28,28),(32,32)]:
  for shape in ['Rectangle','Ellipse','Octagon']:
   for run in [1,2,3,4,5]:
    for seed in range(24):
     generate(w,d,shape,4,run,seed,content=True);count+=1
 print('PASS independent seeded-stair/content/full-connectivity model:',count,'extreme profiles')
 variants={tuple(generate(28,28,'Octagon',4,3,seed)[0]) for seed in range(24)}
 assert len(variants)>12
 print('PASS distinct stair arrangements for seeds0..23:',len(variants))
