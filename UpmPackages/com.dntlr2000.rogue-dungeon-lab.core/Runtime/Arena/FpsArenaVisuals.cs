using System.Collections.Generic;
using UnityEngine;
namespace RogueDungeonLab
{
    // Persist colors rather than transient prototype Material objects, restoring after scene/domain reload.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FpsArenaVisuals : MonoBehaviour
    {
        [SerializeField] private Renderer[] surfaces = new Renderer[0];
        [SerializeField] private Color[] colors = new Color[0];
        public void Capture()
        {
            var selected = new List<Renderer>(); var selectedColors = new List<Color>();
            foreach (var surface in GetComponentsInChildren<Renderer>(true))
            {
                bool authored = false;
                foreach (var identity in surface.GetComponentsInParent<FpsArenaContentIdentity>(true))
                    if (!string.IsNullOrEmpty(identity.ContentKey)) { authored = true; break; }
                if (authored) continue;
                Material material = surface.sharedMaterial; if (material == null) continue;
                selected.Add(surface); selectedColors.Add(material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color);
            }
            surfaces = selected.ToArray(); colors = selectedColors.ToArray();
        }
        private void OnEnable()
        {
            for (int i = 0; i < surfaces.Length && i < colors.Length; i++)
                if (surfaces[i] != null) surfaces[i].sharedMaterial = PrototypeMaterials.ForColor(colors[i]);
        }
    }
}
