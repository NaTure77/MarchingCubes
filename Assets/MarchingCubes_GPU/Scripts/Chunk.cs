using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    // Start is called before the first frame update

    public Vector3Int position;

    public MeshFilter meshFilter;
    public MeshCollider meshCollider;
    public MeshRenderer meshRenderer;

    public float[] mapData;

    public void Init(Vector3Int pos, int mapSize, Material mat)
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = mat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        if (meshFilter.sharedMesh == null)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }
        position = pos;
        mapData = new float[mapSize];
    }

    public void Set(Vector3[] vertices, int[] triangles)
    {
        //meshFilter.sharedMesh = new Mesh();
        meshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Mesh mesh = meshFilter.sharedMesh;
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        meshFilter.sharedMesh.RecalculateNormals();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
    }
    public void Destroy()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += () =>
            {
                DestroyImmediate(gameObject, false);
            };

        else
            Destroy(gameObject);
                
    }
}
