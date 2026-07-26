using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class PrototypePlayerPlayModeTests
    {
        private GameObject _generatorObject;
        private GameObject _cameraObject;
        private RogueDungeonSettings _settings;
        private RogueDungeonGenerator _generator;
        private RuntimeLabHUD _hud;
        private LabOrbitCamera _orbitCamera;
        private Keyboard _keyboard;
        private Mouse _mouse;

        // 각 테스트용 소형 던전, 주 카메라와 가상 키보드·마우스를 준비합니다.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            _settings.stageWidthCells = 20;
            _settings.stageDepthCells = 20;
            _settings.desiredRoomCount = 5;
            _settings.specialGimmickCount = 0;
            _settings.enemyProfile.baseDensity = 0f;
            _settings.destructibleProfile.baseDensity = 0f;
            _settings.propProfile.baseDensity = 0f;
            _settings.generateOnPlay = false;

            _generatorObject = new GameObject("Prototype Player Test Generator");
            _generator = _generatorObject.AddComponent<RogueDungeonGenerator>();
            _generator.settings = _settings;
            _generator.GenerateWithSeed(73125);
            _hud = _generatorObject.AddComponent<RuntimeLabHUD>();

            _cameraObject = new GameObject("Prototype Player Test Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();
            _orbitCamera = _cameraObject.AddComponent<LabOrbitCamera>();
            _keyboard = InputSystem.AddDevice<Keyboard>("Prototype Player Test Keyboard");
            _keyboard.MakeCurrent();
            _mouse = InputSystem.AddDevice<Mouse>("Prototype Player Test Mouse");
            yield return null;
        }

        // 충돌체, 캐릭터 흐름, 3D 이동, 확장 피치, 반응형 HUD와 설정 자동 재생성을 검증합니다.
        [UnityTest]
        public IEnumerator TemporaryPlayer_SpawnsMovesFollowsAndRecovers()
        {
            MeshCollider[] meshColliders = _generator.GetComponentsInChildren<MeshCollider>();
            Assert.That(meshColliders.Length, Is.EqualTo(2));
            for (int i = 0; i < meshColliders.Length; i++)
                Assert.That(meshColliders[i].sharedMesh, Is.Not.Null);

            PrototypePlayerController player = PrototypePlayerController.Spawn(_generator);
            Assert.That(player, Is.Not.Null);
            Assert.That(PrototypePlayerController.Spawn(_generator), Is.SameAs(player));
            Assert.That(Object.FindObjectsByType<PrototypePlayerController>().Length, Is.EqualTo(1));
            Assert.That(player.isActiveAndEnabled, Is.True);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(_orbitCamera.IsFollowing, Is.True);
            Assert.That(_orbitCamera.FollowTarget, Is.EqualTo(player.transform));
            Assert.That(Vector3.Distance(player.transform.position, ExpectedEntrancePosition()), Is.LessThan(0.001f));

            yield return null;
            yield return null;
            Vector3 moveStart = player.transform.position;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(_keyboard));
            Assert.That(_keyboard.wKey.isPressed, Is.True);
            Assert.That(Time.deltaTime, Is.GreaterThan(0f));
            InvokeCharacterMove(player, Vector2.up, false, false, 0.02f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Vector2 horizontalMove = new Vector2(player.transform.position.x - moveStart.x, player.transform.position.z - moveStart.z);
            Assert.That(horizontalMove.magnitude, Is.GreaterThan(0.01f));

            _generator.GenerateWithSeed(90210);
            Assert.That(Vector3.Distance(player.transform.position, ExpectedEntrancePosition()), Is.LessThan(0.001f));
            Assert.That(_orbitCamera.FollowTarget, Is.EqualTo(player.transform));

            CharacterController character = player.GetComponent<CharacterController>();
            character.Move(Vector3.down * 0.2f);
            Assert.That(character.isGrounded, Is.True);
            float jumpStart = player.transform.position.y;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(_keyboard));
            Assert.That(_keyboard.spaceKey.isPressed, Is.True);
            InvokeCharacterMove(player, Vector2.zero, false, true, 0.02f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(player.transform.position.y, Is.GreaterThan(jumpStart + 0.01f));

            PrototypePlayerController.DestroyActive();
            yield return null;
            Assert.That(PrototypePlayerController.Active, Is.Null);
            Assert.That(_orbitCamera.IsFollowing, Is.False);

            Vector3 cameraStart = _orbitCamera.target;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Vector3 cameraMove = _orbitCamera.target - cameraStart;
            Assert.That(cameraMove.magnitude, Is.GreaterThan(0.01f));
            Vector3 expectedForward = _orbitCamera.transform.forward.normalized;
            Assert.That(Vector3.Dot(cameraMove.normalized, expectedForward), Is.GreaterThan(0.999f));
            Assert.That(Mathf.Abs(cameraMove.y), Is.GreaterThan(0.001f));

            float cameraHeight = _orbitCamera.target.y;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_orbitCamera.target.y, Is.GreaterThan(cameraHeight));

            cameraHeight = _orbitCamera.target.y;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(_orbitCamera.target.y, Is.LessThan(cameraHeight));

            Vector3 cameraPositionBeforeRotation = _orbitCamera.transform.position;
            float yawBeforeRotation = _orbitCamera.yaw;
            _mouse.MakeCurrent();
            MouseState rotateState = new MouseState { position = new Vector2(1000f, 1000f), delta = new Vector2(40f, -20f) }.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, rotateState);
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(1000f, 1000f) });
            InputSystem.Update();
            Assert.That(_orbitCamera.yaw, Is.Not.EqualTo(yawBeforeRotation));
            Assert.That(Vector3.Distance(_orbitCamera.transform.position, cameraPositionBeforeRotation), Is.LessThan(0.001f));

            _orbitCamera.pitch = 0f;
            Vector3 cameraPositionBeforeHighLook = _orbitCamera.transform.position;
            MouseState highLookState = new MouseState { position = new Vector2(1000f, 1000f), delta = new Vector2(0f, 400f) }.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(_mouse, highLookState);
            InputSystem.Update();
            _orbitCamera.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = new Vector2(1000f, 1000f) });
            InputSystem.Update();
            Assert.That(_orbitCamera.pitch, Is.LessThan(-15f));
            Assert.That(_orbitCamera.pitch, Is.GreaterThanOrEqualTo(-89f));
            Assert.That(Vector3.Distance(_orbitCamera.transform.position, cameraPositionBeforeHighLook), Is.LessThan(0.001f));

            AssertPanelFitsScreen(320, 180);
            AssertPanelFitsScreen(360, 800);
            AssertPanelFitsScreen(3840, 1080);

            int activeSeedBeforeLiveRegeneration = _generator.ActiveSeed;
            int sourceWidthBeforeLiveRegeneration = _settings.stageWidthCells;
            RogueDungeonSettings runtimeSettings = _generator.ActiveRuntimeSettings;
            Assert.That(runtimeSettings, Is.Not.Null);
            Assert.That(runtimeSettings, Is.Not.SameAs(_settings));
            Assert.That(
                (runtimeSettings.hideFlags & HideFlags.HideAndDontSave),
                Is.EqualTo(HideFlags.HideAndDontSave));
            runtimeSettings.stageWidthCells = 24;
            InvokeHudMethod("RequestLiveRegeneration");
            InvokeHudMethod("ProcessLiveRegeneration");
            Assert.That(_generator.ActiveSeed, Is.EqualTo(activeSeedBeforeLiveRegeneration));
            Assert.That(_generator.CurrentLayout.Width, Is.EqualTo(24));
            Assert.That(_settings.stageWidthCells, Is.EqualTo(sourceWidthBeforeLiveRegeneration));
        }

        // 별도 Generator settings가 있어도 Procedural StageDefinition recipe 복제본만 HUD가 편집하고 두 원본을 보존하는지 검증합니다.
        [UnityTest]
        public IEnumerator ProceduralStageDefinition_UsesIsolatedRecipeCloneWithSeparateGeneratorSettings()
        {
            RogueDungeonSettings recipe = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            WeightedDropTable runtimeDropTable = ScriptableObject.CreateInstance<WeightedDropTable>();
            RogueDungeonSettings failingRecipe = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonStageDefinition failingDefinition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            try
            {
                recipe.ApplyPreset(DungeonPreset.Compact);
                recipe.stageWidthCells = 30;
                recipe.stageDepthCells = 28;
                recipe.seed = 111111;
                string recipeHashBefore = DungeonRecipeSnapshot.Capture(recipe).ComputeHash();
                int generatorSettingsWidthBefore = _settings.stageWidthCells;
                runtimeDropTable.entries.Add(new DropEntry
                {
                    itemId = "RuntimeOnlyDrop",
                    weight = 1f
                });
                _settings.enemyDropTable = runtimeDropTable;

                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.recipe = recipe;
                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 424242;
                definition.generatorVersion = DungeonGeneratorVersions.StableV2;
                RogueDungeonSettings previousRuntimeSettings = _generator.ActiveRuntimeSettings;
                _generator.stageDefinition = definition;
                _generator.LoadStageDefinition();
                yield return null;

                RogueDungeonSettings runtimeSettings = _generator.ActiveRuntimeSettings;
                Assert.That(previousRuntimeSettings == null, Is.True);
                Assert.That(_generator.CurrentStageInstance.Definition, Is.SameAs(definition));
                Assert.That(_generator.CanEditActiveRuntimeRecipe, Is.True);
                Assert.That(runtimeSettings, Is.Not.Null);
                Assert.That(runtimeSettings, Is.Not.SameAs(recipe));
                Assert.That(runtimeSettings, Is.Not.SameAs(_settings));
                Assert.That(runtimeSettings.stageWidthCells, Is.EqualTo(30));
                Assert.That(runtimeSettings.enemyDropTable, Is.SameAs(runtimeDropTable));
                Assert.That(
                    (runtimeSettings.hideFlags & HideFlags.HideAndDontSave),
                    Is.EqualTo(HideFlags.HideAndDontSave));

                runtimeSettings.stageWidthCells = 34;
                _generator.GenerateActiveRecipeWithSeed(525252);
                Assert.That(_generator.ActiveSeed, Is.EqualTo(525252));
                Assert.That(_generator.CurrentLayout.Width, Is.EqualTo(34));
                Assert.That(_generator.CurrentBlueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.StableV2));
                Assert.That(_generator.CurrentStageInstance.Definition, Is.SameAs(definition));

                InvokeHudMethod("RequestLiveRegeneration");
                InvokeHudMethod("ProcessLiveRegeneration");

                Assert.That(_generator.ActiveSeed, Is.EqualTo(525252));
                Assert.That(_generator.CurrentLayout.Width, Is.EqualTo(34));
                Assert.That(_generator.CurrentBlueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.StableV2));
                Assert.That(_generator.CurrentStageInstance.Definition, Is.SameAs(definition));
                Assert.That(recipe.stageWidthCells, Is.EqualTo(30));
                Assert.That(DungeonRecipeSnapshot.Capture(recipe).ComputeHash(), Is.EqualTo(recipeHashBefore));
                Assert.That(_settings.stageWidthCells, Is.EqualTo(generatorSettingsWidthBefore));

                string activeHashBeforeFailure = _generator.CurrentBlueprint.blueprintHash;
                GameObject activeRootBeforeFailure = _generator.CurrentStageInstance.Root;
                failingRecipe.ApplyPreset(DungeonPreset.Chaos);
                failingDefinition.sourceMode = DungeonStageSourceMode.Procedural;
                failingDefinition.buildMode = DungeonStageBuildMode.BakedPrefab;
                failingDefinition.recipe = failingRecipe;
                _generator.stageDefinition = failingDefinition;

                Assert.Throws<DungeonStageLoadException>(delegate
                {
                    _generator.LoadStageDefinition();
                });
                yield return null;

                Assert.That(_generator.ActiveRuntimeSettings, Is.SameAs(runtimeSettings));
                Assert.That(runtimeSettings == null, Is.False);
                Assert.That(_generator.CurrentStageInstance.Root, Is.SameAs(activeRootBeforeFailure));
                Assert.That(_generator.CurrentBlueprint.blueprintHash, Is.EqualTo(activeHashBeforeFailure));
                FieldInfo pendingField = typeof(RogueDungeonGenerator).GetField(
                    "_pendingRuntimeSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pendingField, Is.Not.Null);
                Assert.That(pendingField.GetValue(_generator), Is.Null);
                _generator.stageDefinition = definition;
            }
            finally
            {
                _generator.stageDefinition = null;
                if (failingDefinition != null) Object.Destroy(failingDefinition);
                if (failingRecipe != null) Object.Destroy(failingRecipe);
                if (definition != null) Object.Destroy(definition);
                if (runtimeDropTable != null) Object.Destroy(runtimeDropTable);
                if (recipe != null) Object.Destroy(recipe);
            }
        }

        // SavedBlueprint 활성화 중에는 HUD 구조 편집이 차단되고 새 시드 요청도 저장 논리 맵을 바꾸지 않는지 검증합니다.
        [UnityTest]
        public IEnumerator SavedBlueprint_DisablesRuntimeRecipeEditingAndKeepsLogicalMap()
        {
            RogueDungeonSettings sourceSettings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            try
            {
                sourceSettings.ApplyPreset(DungeonPreset.Compact);
                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.Create(
                        sourceSettings,
                        717171,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys.LegacyCatalogPlanningHash)).Blueprint;
                asset.Store(blueprint);
                definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.savedBlueprint = asset;
                RogueDungeonSettings previousRuntimeSettings = _generator.ActiveRuntimeSettings;
                _generator.stageDefinition = definition;
                _generator.LoadStageDefinition();
                yield return null;

                string blueprintHash = _generator.CurrentBlueprint.blueprintHash;
                int activeSeed = _generator.ActiveSeed;
                Assert.That(previousRuntimeSettings == null, Is.True);
                Assert.That(_generator.CanEditActiveRuntimeRecipe, Is.False);
                Assert.That(_generator.ActiveRuntimeSettings, Is.Null);

                LogAssert.Expect(LogType.Warning, "Saved Blueprint source does not allow runtime recipe editing.");
                _generator.GenerateActiveRecipeWithSeed(999999);
                _generator.GenerateNewSeed();

                Assert.That(_generator.ActiveSeed, Is.EqualTo(activeSeed));
                Assert.That(_generator.CurrentBlueprint.blueprintHash, Is.EqualTo(blueprintHash));
                Assert.That(asset.blueprint.blueprintHash, Is.EqualTo(blueprintHash));
                yield return null;
            }
            finally
            {
                _generator.stageDefinition = null;
                if (definition != null) Object.Destroy(definition);
                if (asset != null) Object.Destroy(asset);
                if (sourceSettings != null) Object.Destroy(sourceSettings);
            }
        }

        // Generator 수명이 끝나면 소유한 HideAndDontSave 설정 복제본도 함께 파괴되는지 검증합니다.
        [UnityTest]
        public IEnumerator RuntimeSettingsClone_IsDestroyedWithOwningGenerator()
        {
            RogueDungeonSettings source = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            GameObject owner = new GameObject("Runtime Settings Clone Owner");
            RogueDungeonSettings runtimeSettings = null;
            try
            {
                source.ApplyPreset(DungeonPreset.Compact);
                source.generateOnPlay = false;
                RogueDungeonGenerator generator = owner.AddComponent<RogueDungeonGenerator>();
                generator.settings = source;
                generator.GenerateWithSeed(818181);
                runtimeSettings = generator.ActiveRuntimeSettings;

                Assert.That(runtimeSettings, Is.Not.Null);
                Assert.That(runtimeSettings, Is.Not.SameAs(source));
                Object.Destroy(owner);
                owner = null;
                yield return null;

                Assert.That(runtimeSettings == null, Is.True);
                Assert.That(source, Is.Not.Null);
            }
            finally
            {
                if (owner != null) Object.Destroy(owner);
                if (source != null) Object.Destroy(source);
            }
        }

        // 첫 로드 전에도 loadOnPlay Procedural Definition이 settings-only fallback보다 재생성·새 시드에서 우선되는지 검사합니다.
        [UnityTest]
        public IEnumerator ConfiguredProceduralDefinition_IsPreferredBeforeFirstLoad()
        {
            RogueDungeonSettings source = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            RogueDungeonSettings recipe = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonStageDefinition regenerateDefinition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            DungeonStageDefinition randomDefinition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject regenerateOwner = new GameObject("Definition First Regenerate Owner");
            GameObject randomOwner = new GameObject("Definition First Random Owner");
            try
            {
                source.ApplyPreset(DungeonPreset.Balanced);
                source.generateOnPlay = false;
                recipe.ApplyPreset(DungeonPreset.Compact);
                regenerateDefinition.sourceMode = DungeonStageSourceMode.Procedural;
                regenerateDefinition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                regenerateDefinition.recipe = recipe;
                regenerateDefinition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                regenerateDefinition.fixedSeed = 1357911;
                regenerateDefinition.loadOnPlay = true;

                RogueDungeonGenerator regenerateGenerator =
                    regenerateOwner.AddComponent<RogueDungeonGenerator>();
                regenerateGenerator.settings = source;
                regenerateGenerator.stageDefinition = regenerateDefinition;
                regenerateGenerator.RegenerateActiveSeed();

                Assert.That(
                    regenerateGenerator.CurrentStageInstance.Definition,
                    Is.SameAs(regenerateDefinition));
                Assert.That(regenerateGenerator.ActiveSeed, Is.EqualTo(1357911));
                Assert.That(
                    regenerateGenerator.CurrentLayout.Width,
                    Is.EqualTo(recipe.stageWidthCells));

                randomDefinition.sourceMode = DungeonStageSourceMode.Procedural;
                randomDefinition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                randomDefinition.recipe = recipe;
                randomDefinition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                randomDefinition.fixedSeed = 2468022;
                randomDefinition.loadOnPlay = true;

                RogueDungeonGenerator randomGenerator =
                    randomOwner.AddComponent<RogueDungeonGenerator>();
                randomGenerator.settings = source;
                randomGenerator.stageDefinition = randomDefinition;
                randomGenerator.GenerateNewSeed();

                Assert.That(
                    randomGenerator.CurrentStageInstance.Definition,
                    Is.SameAs(randomDefinition));
                Assert.That(
                    randomGenerator.ActiveSeed,
                    Is.EqualTo(randomGenerator.CurrentBlueprint.seed));
                Assert.That(
                    randomGenerator.CurrentLayout.Width,
                    Is.EqualTo(recipe.stageWidthCells));
                yield return null;
            }
            finally
            {
                if (randomOwner != null) Object.Destroy(randomOwner);
                if (regenerateOwner != null) Object.Destroy(regenerateOwner);
                if (randomDefinition != null) Object.Destroy(randomDefinition);
                if (regenerateDefinition != null) Object.Destroy(regenerateDefinition);
                if (recipe != null) Object.Destroy(recipe);
                if (source != null) Object.Destroy(source);
            }
        }

        // 저장 Blueprint의 cellSize가 현재 settings와 달라도 플레이어가 저장된 입구 좌표에 생성되는지 검사합니다.
        [UnityTest]
        public IEnumerator SavedBlueprintPlayer_UsesLoadedBlueprintCellSize()
        {
            RogueDungeonSettings sourceSettings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            try
            {
                sourceSettings.ApplyPreset(DungeonPreset.Compact);
                sourceSettings.cellSize = 5f;
                sourceSettings.specialGimmickCount = 0;
                sourceSettings.enemyProfile.baseDensity = 0f;
                sourceSettings.destructibleProfile.baseDensity = 0f;
                sourceSettings.propProfile.baseDensity = 0f;
                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.Create(
                        sourceSettings,
                        606060,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys.LegacyCatalogPlanningHash)).Blueprint;
                asset.Store(blueprint);

                definition.sourceMode = DungeonStageSourceMode.SavedBlueprint;
                definition.savedBlueprint = asset;
                _settings.cellSize = 1.5f;
                _generator.stageDefinition = definition;
                _generator.LoadStageDefinitionWithSeed(999999);

                Assert.That(_generator.ActiveSeed, Is.EqualTo(606060));
                Assert.That(_generator.CurrentCellSize, Is.EqualTo(5f));
                PrototypePlayerController player = PrototypePlayerController.Spawn(_generator);
                Assert.That(player, Is.Not.Null);
                DungeonLayout layout = _generator.CurrentLayout;
                Vector3 localEntrance = layout.CellToLocalPosition(layout.Entrance, 5f) + Vector3.up * 0.08f;
                Vector3 expected = _generator.transform.TransformPoint(localEntrance);
                Assert.That(Vector3.Distance(player.transform.position, expected), Is.LessThan(0.001f));
                yield return null;
            }
            finally
            {
                PrototypePlayerController.DestroyActive();
                if (definition != null) Object.Destroy(definition);
                if (asset != null) Object.Destroy(asset);
                if (sourceSettings != null) Object.Destroy(sourceSettings);
            }
        }

        // 실제 BakedPrefab을 로드한 뒤 화면 클릭 한 번이 복제된 파괴 대상과 드랍 통계 1회를 처리하는지 검사합니다.
        [UnityTest]
        public IEnumerator BakedPrefab_ClickRecordsExactlyOneDropSample()
        {
            RogueDungeonSettings sourceSettings =
                ScriptableObject.CreateInstance<RogueDungeonSettings>();
            DungeonBlueprintAsset blueprintAsset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            DungeonBakeMaterialSet materialSet =
                ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
            DungeonBakeManifest manifest =
                ScriptableObject.CreateInstance<DungeonBakeManifest>();
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            WeightedDropTable dropTable =
                ScriptableObject.CreateInstance<WeightedDropTable>();
            Material material = null;
            GameObject bakedTemplate = null;
            try
            {
                sourceSettings.ApplyPreset(DungeonPreset.Compact);
                sourceSettings.specialGimmickCount = 0;
                sourceSettings.enemyProfile.baseDensity = 0f;
                sourceSettings.destructibleProfile.baseDensity = 0f;
                sourceSettings.propProfile.baseDensity = 0f;
                sourceSettings.spawnDropMarkers = false;
                sourceSettings.resetDropStatsOnGenerate = true;

                _orbitCamera.enabled = false;
                _cameraObject.transform.position = Vector3.zero;
                _cameraObject.transform.rotation = Quaternion.identity;
                Camera camera = _cameraObject.GetComponent<Camera>();
                Vector2 clickPosition = new Vector2(
                    Mathf.Max(1f, Screen.width - 1f),
                    Mathf.Max(1f, Screen.height * 0.5f));
                Assert.That(
                    RuntimeLabHUD.IsPointerInside(clickPosition),
                    Is.False);
                Vector3 targetPosition =
                    camera.ScreenPointToRay(clickPosition).GetPoint(8f);

                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.Create(
                        sourceSettings,
                        606606,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                        "r6-baked-click-playmode")).Blueprint;
                DungeonCellRecord spawnCell = blueprint.cells.Find(
                    cell => cell != null &&
                            (cell.flags & DungeonCellFlags.Floor) != 0);
                Assert.That(spawnCell, Is.Not.Null);
                DungeonSpawnRecord spawnRecord = new DungeonSpawnRecord
                {
                    spawnId = "r6-baked-click-target",
                    category = DungeonSpawnCategory.Destructible,
                    contentKey = DungeonBuiltInContentKeys.Destructible,
                    instanceName = "R6 Baked Click Target",
                    cell = spawnCell.coordinate,
                    localPosition = targetPosition,
                    localScale = Vector3.one,
                    roomId = spawnCell.roomId,
                    progression = 0f,
                    variantSeed = 606
                };
                blueprint.spawns.Add(spawnRecord);
                blueprint.RefreshHash();
                blueprintAsset.Store(blueprint);

                dropTable.name = "R6 Baked Guaranteed Drop";
                dropTable.entries.Add(new DropEntry
                {
                    itemId = "BakedGuaranteedDrop",
                    weight = 1f,
                    minQuantity = 1,
                    maxQuantity = 1,
                    representsNoDrop = false
                });

                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                FillBakeMaterialSet(materialSet, material);

                bakedTemplate = new GameObject("R6 Baked Click Template");
                bakedTemplate.SetActive(false);
                GameObject targetObject =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                targetObject.name = spawnRecord.instanceName;
                targetObject.transform.SetParent(
                    bakedTemplate.transform,
                    false);
                targetObject.transform.localPosition =
                    spawnRecord.localPosition;
                DungeonSpawnIdentity identity =
                    targetObject.AddComponent<DungeonSpawnIdentity>();
                identity.Configure(spawnRecord);
                DestructibleDropTarget target =
                    targetObject.AddComponent<DestructibleDropTarget>();
                target.Configure(
                    "R6BakedClickTarget",
                    DropSourceKind.Destructible,
                    dropTable,
                    false);

                DungeonSceneBuildResult buildResult =
                    new DungeonSceneBuildResult
                    {
                        MeshTriangleCount = 0,
                        ContentCounts = new ContentSpawnCounts
                        {
                            DestructibleCount = 1
                        },
                        BuiltInFallbackCount = 1,
                        ValidationReport = new DungeonValidationReport()
                    };
                DungeonBakedStageMetadata metadata =
                    bakedTemplate.AddComponent<DungeonBakedStageMetadata>();
                metadata.Configure(
                    DungeonBakeFormat.Current,
                    DungeonBakeBuilderVersions.Current,
                    blueprintAsset.blueprint.blueprintHash,
                    buildResult);

                manifest.sourceBlueprint = blueprintAsset;
                manifest.sourceRuntimeSettings = sourceSettings;
                manifest.materialSet = materialSet;
                manifest.bakedPrefab = bakedTemplate;
                manifest.sourceBlueprintHash =
                    blueprintAsset.blueprint.blueprintHash;
                manifest.finalBlueprintHash =
                    blueprintAsset.blueprint.blueprintHash;
                manifest.catalogPlanningHash =
                    blueprintAsset.blueprint.catalogPlanningHash;
                manifest.contentRealizationHash =
                    "r6-playmode-realization";
                manifest.gameplayBuildConfigHash =
                    "r6-playmode-gameplay";
                manifest.materialDependencyHash =
                    "r6-playmode-material";
                manifest.ownedArtifacts.Add(new DungeonBakeArtifactRecord
                {
                    role = "prefab",
                    assetGuid = "r6-playmode-prefab-guid",
                    dependencyHash = "r6-playmode-prefab-dependency"
                });

                definition.sourceMode =
                    DungeonStageSourceMode.SavedBlueprint;
                definition.buildMode =
                    DungeonStageBuildMode.BakedPrefab;
                definition.savedBlueprint = blueprintAsset;
                definition.bakedPrefab = bakedTemplate;
                definition.bakeManifest = manifest;
                _generator.stageDefinition = definition;
                _generator.LoadStageDefinition();
                yield return null;

                Assert.That(
                    _generator.CurrentStageInstance.BuildMode,
                    Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
                DungeonSpawnIdentity loadedIdentity = null;
                DungeonSpawnIdentity[] loadedIdentities =
                    _generator.CurrentStageInstance.Root
                        .GetComponentsInChildren<DungeonSpawnIdentity>(true);
                for (int i = 0; i < loadedIdentities.Length; i++)
                {
                    if (loadedIdentities[i].SpawnId ==
                        spawnRecord.spawnId)
                    {
                        loadedIdentity = loadedIdentities[i];
                        break;
                    }
                }
                Assert.That(loadedIdentity, Is.Not.Null);

                DropValidationService service =
                    DropValidationService.Active;
                Assert.That(service, Is.Not.Null);
                service.ResetStatistics();
                RogueDungeonClickInteractor interactor =
                    _cameraObject.GetComponent<RogueDungeonClickInteractor>();
                if (interactor == null)
                {
                    interactor =
                        _cameraObject.AddComponent<RogueDungeonClickInteractor>();
                }
                interactor.targetCamera = camera;
                Physics.SyncTransforms();
                RaycastHit clickHit;
                Assert.That(
                    Physics.Raycast(
                        camera.ScreenPointToRay(clickPosition),
                        out clickHit,
                        interactor.maximumDistance,
                        interactor.interactionMask,
                        QueryTriggerInteraction.Ignore),
                    Is.True);
                Assert.That(
                    clickHit.collider
                        .GetComponentInParent<DestructibleDropTarget>(),
                    Is.Not.Null);

                _mouse.MakeCurrent();
                InputState.Change(
                    _mouse,
                    new MouseState { position = clickPosition },
                    InputUpdateType.Dynamic);
                Assert.That(_mouse.leftButton.isPressed, Is.False);
                Assert.That(
                    _mouse.leftButton.wasPressedThisFrame,
                    Is.False);
                InputState.Change(
                    _mouse,
                    new MouseState { position = clickPosition }
                        .WithButton(MouseButton.Left),
                    InputUpdateType.Dynamic);
                Assert.That(_mouse.leftButton.isPressed, Is.True);
                Assert.That(_mouse.leftButton.wasPressedThisFrame, Is.True);
                interactor.SendMessage(
                    "Update",
                    SendMessageOptions.RequireReceiver);

                var snapshots = service.GetSnapshots();
                Assert.That(snapshots.Count, Is.EqualTo(1));
                Assert.That(snapshots[0].SourceKind,
                    Is.EqualTo(DropSourceKind.Destructible));
                Assert.That(snapshots[0].Attempts, Is.EqualTo(1));
                Assert.That(snapshots[0].Entries.Count, Is.EqualTo(1));
                Assert.That(
                    snapshots[0].Entries[0].ItemId,
                    Is.EqualTo("BakedGuaranteedDrop"));
                Assert.That(snapshots[0].Entries[0].Hits, Is.EqualTo(1));
                yield return null;
                Assert.That(loadedIdentity == null, Is.True);
            }
            finally
            {
                _generator.stageDefinition = null;
                _generator.ClearGenerated();
                if (bakedTemplate != null) Object.Destroy(bakedTemplate);
                if (definition != null) Object.Destroy(definition);
                if (manifest != null) Object.Destroy(manifest);
                if (materialSet != null) Object.Destroy(materialSet);
                if (material != null) Object.Destroy(material);
                if (dropTable != null) Object.Destroy(dropTable);
                if (blueprintAsset != null) Object.Destroy(blueprintAsset);
                if (sourceSettings != null) Object.Destroy(sourceSettings);
            }
        }

        // 클릭 대상의 1회 파괴가 드랍 추첨·통계 1회를 만들고 중복 호출을 거부하는지 검사합니다.
        [UnityTest]
        public IEnumerator DestructibleTarget_DestroyOnceRecordsOneDropSample()
        {
            WeightedDropTable table = ScriptableObject.CreateInstance<WeightedDropTable>();
            GameObject targetObject = null;
            try
            {
                table.name = "PlayMode Deterministic Drop";
                table.entries.Add(new DropEntry
                {
                    itemId = "GuaranteedGold",
                    weight = 1f,
                    minQuantity = 2,
                    maxQuantity = 2,
                    representsNoDrop = false
                });
                DropValidationService service = DropValidationService.Active;
                Assert.That(service, Is.Not.Null);
                service.ResetStatistics();

                targetObject = new GameObject("Playable Prefab Root");
                DungeonSpawnIdentity identity = targetObject.AddComponent<DungeonSpawnIdentity>();
                identity.Configure(new DungeonSpawnRecord
                {
                    spawnId = "playmode-target",
                    contentKey = "test/destructible",
                    category = DungeonSpawnCategory.Destructible,
                    cell = Vector2Int.zero
                });
                GameObject targetChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                targetChild.transform.SetParent(targetObject.transform, false);
                DestructibleDropTarget target = targetChild.AddComponent<DestructibleDropTarget>();
                target.Configure("PlayModeTarget", DropSourceKind.Destructible, table, false);

                Assert.That(target.TryDestroy(targetObject.transform.position), Is.True);
                Assert.That(target.TryDestroy(targetObject.transform.position), Is.False);
                var snapshots = service.GetSnapshots();
                Assert.That(snapshots.Count, Is.EqualTo(1));
                Assert.That(snapshots[0].Attempts, Is.EqualTo(1));
                Assert.That(snapshots[0].Entries[0].ItemId, Is.EqualTo("GuaranteedGold"));
                Assert.That(snapshots[0].Entries[0].Hits, Is.EqualTo(1));
                Assert.That(snapshots[0].Entries[0].TotalQuantity, Is.EqualTo(2));
                yield return null;
                Assert.That(targetObject == null, Is.True);
            }
            finally
            {
                if (targetObject != null) Object.Destroy(targetObject);
                if (table != null) Object.Destroy(table);
            }
        }

        // 하나의 Material을 R6 BakedPrefab manifest의 모든 필수 재질 슬롯에 채웁니다.
        private static void FillBakeMaterialSet(
            DungeonBakeMaterialSet materialSet,
            Material material)
        {
            materialSet.floor = material;
            materialSet.wall = material;
            materialSet.enemy = material;
            materialSet.destructible = material;
            materialSet.prop = material;
            materialSet.gimmick = material;
            materialSet.entrance = material;
            materialSet.exit = material;
        }

        // 지정 해상도에서 계산한 HUD 패널이 화면의 네 경계를 벗어나지 않는지 확인합니다.
        private static void AssertPanelFitsScreen(int width, int height)
        {
            MethodInfo method = typeof(RuntimeLabHUD).GetMethod("CalculatePanelScreenRect", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Rect panel = (Rect)method.Invoke(null, new object[] { width, height });
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(width + 0.01f));
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(height + 0.01f));
        }

        // HUD의 비공개 자동 재생성 단계를 호출해 실제 생성 결과 반영을 검증합니다.
        private void InvokeHudMethod(string methodName)
        {
            MethodInfo method = typeof(RuntimeLabHUD).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_hud, null);
        }

        // 현재 레이아웃의 입구 셀을 임시 캐릭터의 예상 월드 위치로 변환합니다.
        private Vector3 ExpectedEntrancePosition()
        {
            DungeonLayout layout = _generator.CurrentLayout;
            Vector3 local = layout.CellToLocalPosition(layout.Entrance, _settings.cellSize) + Vector3.up * 0.08f;
            return _generator.transform.TransformPoint(local);
        }

        // 프레임 입력 타이밍에 의존하지 않고 실제 비공개 이동 메서드의 점프 분기를 호출합니다.
        private static void InvokeCharacterMove(PrototypePlayerController player, Vector2 move, bool sprint, bool jump, float deltaTime)
        {
            MethodInfo method = typeof(PrototypePlayerController).GetMethod("MoveCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(player, new object[] { move, sprint, jump, deltaTime });
        }

        // 테스트가 만든 입력 장치, 캐릭터, 장면 오브젝트와 설정을 모두 정리합니다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PrototypePlayerController.DestroyActive();
            if (_generator != null) _generator.ClearGenerated();
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            if (_generatorObject != null) Object.Destroy(_generatorObject);
            if (_settings != null) Object.Destroy(_settings);
            yield return null;
            yield return null;
        }
    }
}
