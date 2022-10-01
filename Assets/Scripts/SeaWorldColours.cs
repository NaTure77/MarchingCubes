using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteInEditMode]
public class SeaWorldColours : MonoBehaviour {
    public Material mat;
    [Range (0, 1)]
    public float fogDstMultiplier = 1;

    public Vector4 shaderParams;

    public MarchingCube_GPU.MeshGenerator meshGenerator;
    public Camera cam;

    public Gradient gradient;
    public float normalOffsetWeight;

    Texture2D texture;
    const int textureResolution = 50;

    void Init () {
        if (texture == null || texture.width != textureResolution) {
            texture = new Texture2D (textureResolution, 1, TextureFormat.RGBA32, false);
        }
    }

    void Awake () {
        Init ();
        UpdateTexture ();

        if (meshGenerator == null) {
            meshGenerator = FindObjectOfType< MarchingCube_GPU.MeshGenerator> ();
        }
        if (cam == null) {
            cam = FindObjectOfType<Camera> ();
        }

        mat.SetTexture ("ramp", texture);
        mat.SetVector("params",shaderParams);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = cam.backgroundColor;
        RenderSettings.fogEndDistance = meshGenerator.viewDistance * fogDstMultiplier * 10;
        Debug.Log("!!!!");
    }

    void UpdateTexture () {
        if (gradient != null) {
            Color[] colours = new Color[texture.width];
            for (int i = 0; i < textureResolution; i++) {
                Color gradientCol = gradient.Evaluate (i / (textureResolution - 1f));
                colours[i] = gradientCol;
            }

            texture.SetPixels (colours);
            texture.Apply ();
        }
    }
}