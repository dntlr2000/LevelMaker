"""Check wall wiring and UPM parity. Actual Unity evidence is reported separately."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
arena = root / "Assets/RogueDungeonLab/Runtime/Arena"
core = root / "UpmPackages/com.dntlr2000.rogue-dungeon-lab.core/Runtime/Arena"
for path in arena.glob("*"):
    if path.is_file():
        assert path.read_bytes() == (core / path.name).read_bytes(), path
        if path.suffix == ".cs":
            source = path.read_text(encoding="utf-8-sig")
            assert "UnityEditor" not in source, path
            assert not re.search(r"\b(?:UnityEngine\.)?Random\.(?:Range|value|InitState)", source), path
for suffix in ("", ".meta"):
    source = root / ("Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs" + suffix)
    package = root / ("UpmPackages/com.dntlr2000.rogue-dungeon-lab.lab/Editor/FpsArenaWindow.cs" + suffix)
    assert source.read_bytes() == package.read_bytes()

baseline = json.loads((root / "Assets/RogueDungeonLab/Tests/EditMode/FpsArenaWallsOffBaseline.json").read_text(encoding="utf-8-sig"))["cases"]
assert len(baseline) == 36
old_fields = list(json.loads(next(case["recipeJson"] for case in baseline if json.loads(case["recipeJson"])["generatorVersion"] == 2)))
snapshot = (arena / "FpsArenaWalls.cs").read_text(encoding="utf-8-sig").split("internal sealed class FpsArenaV2RecipeSnapshot")[1].split("public FpsArenaV2RecipeSnapshot")[0]
fields = []
for match in re.finditer(r"public\s+(?:int|float|FpsArenaShape|FpsArenaGeneratorVersion|FpsArenaCoverShapes)\s+([^;]+);", snapshot):
    fields.extend(name.strip() for name in match.group(1).split(","))
assert fields == old_fields, (fields, old_fields)
assert all(re.fullmatch(r"[0-9a-f]{64}", case["hash"]) for case in baseline)
print("PASS: Runtime boundary, independent seeded wall stream source, Core/Lab source+GUID parity")
print("PASS: exact pre-wall V2 field order, 36 actual Unity baseline fixtures")
print("Unity compile/Physics/GUI outcomes: see docs/FPS_ARENA_WALLS_VERIFICATION_KO.md")
