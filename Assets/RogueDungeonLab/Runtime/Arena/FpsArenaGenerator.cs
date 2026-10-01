using System;
using UnityEngine;

namespace RogueDungeonLab
{
    [DisallowMultipleComponent]
    public sealed class FpsArenaGenerator : MonoBehaviour
    {
        public const string GeneratedRootName = "__FpsArena_Generated";
        public FpsArenaSettings settings;
        public int seed = 73125;
        public FpsArenaRecipe recipe = FpsArenaRecipe.CreateFlexible();
        public FpsArenaContentCatalog contentCatalog;
        public FpsArenaContentCatalog ActiveCatalog { get { return settings != null ? settings.contentCatalog : contentCatalog; } }
        public bool generateOnPlay = true;
        [SerializeField, HideInInspector] private FpsArenaRecipe builtRecipe;
        [SerializeField, HideInInspector] private int builtSeed;
        [SerializeField, HideInInspector] private FpsArenaCatalogSnapshot builtCatalogSnapshot;
        private FpsArenaLayout currentLayout;
        private GameObject generatedRoot;
        private bool restorePending = true;
        private void OnEnable() { RestoreBuiltLayout(); }
        private void OnValidate() { restorePending = true; }
        private void RestoreBuiltLayout()
        {
            restorePending = false;
            Transform existing = transform.Find(GeneratedRootName);
            generatedRoot = existing != null ? existing.gameObject : null;
            currentLayout = existing != null && builtRecipe != null ? FpsArenaPlanner.GenerateSnapshot(builtRecipe, builtSeed, builtCatalogSnapshot) : null;
        }
        public FpsArenaLayout CurrentLayout
        {
            get { if (restorePending || generatedRoot == null) RestoreBuiltLayout(); return currentLayout; }
        }
        public GameObject GeneratedRoot
        {
            get { if (restorePending || generatedRoot == null) RestoreBuiltLayout(); return generatedRoot; }
        }
        public event Action<FpsArenaLayout> Generated;
        private void Start() { if (generateOnPlay) Generate(); }
        public FpsArenaLayout Plan()
        { return FpsArenaPlanner.Generate(settings != null ? settings.recipe : recipe, settings != null ? settings.seed : seed, ActiveCatalog); }
        [ContextMenu("FPS 아레나 생성")]
        public void Generate()
        {
            var layout = Plan();
            GameObject candidate = FpsArenaSceneBuilder.Build(layout, transform, ActiveCatalog);
            ReplaceGenerated(candidate, layout);
        }
        // The editor owns Undo when calling this API with a constructed candidate.
        public void ReplaceGenerated(GameObject candidate, FpsArenaLayout layout)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != candidate && child.name == GeneratedRootName)
                { child.SetActive(false); if (Application.isPlaying) Destroy(child); else DestroyImmediate(child); }
            }
            candidate.name = GeneratedRootName;
            builtRecipe = layout.Recipe.Normalized(); builtSeed = layout.Seed;
            builtCatalogSnapshot = layout.CatalogSnapshot != null ? layout.CatalogSnapshot.Clone() : null;
            generatedRoot = candidate; currentLayout = layout; restorePending = false;
            candidate.SetActive(true);
            if (Generated != null) Generated(layout);
        }
    }
}
