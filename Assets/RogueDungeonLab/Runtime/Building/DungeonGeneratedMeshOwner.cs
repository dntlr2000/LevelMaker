using System;
using UnityEngine;

namespace RogueDungeonLab
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class DungeonGeneratedMeshOwner : MonoBehaviour
    {
        [NonSerialized] private Mesh[] _ownedMeshes = Array.Empty<Mesh>();

        // 이 Geometry root가 독점 소유하는 런타임 합성 메시 참조를 기록합니다.
        public void Initialize(params Mesh[] meshes)
        {
            _ownedMeshes = meshes != null ? (Mesh[])meshes.Clone() : Array.Empty<Mesh>();
        }

        // 정확히 기록된 메시 참조만 필터·콜라이더에서 분리한 뒤 Unity 수명주기에 맞게 해제합니다.
        public void ReleaseOwnedMeshes()
        {
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (Owns(filters[i].sharedMesh)) filters[i].sharedMesh = null;
            }
            MeshCollider[] colliders = GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Owns(colliders[i].sharedMesh)) colliders[i].sharedMesh = null;
            }
            for (int i = 0; i < _ownedMeshes.Length; i++)
            {
                Mesh mesh = _ownedMeshes[i];
                if (mesh == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            _ownedMeshes = Array.Empty<Mesh>();
        }

        // 주어진 메시가 이 owner에 기록된 정확한 인스턴스인지 확인합니다.
        private bool Owns(Mesh mesh)
        {
            if (mesh == null) return false;
            for (int i = 0; i < _ownedMeshes.Length; i++)
            {
                if (_ownedMeshes[i] == mesh) return true;
            }
            return false;
        }
    }
}
