using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MarchingCube_GPU
{
    public class MeshGenerator : MonoBehaviour
    {
        public ComputeShader shader;
        ComputeBuffer triCount;
        ComputeBuffer trianglesBuffer;
       
        int kernelID = 0;
        private struct CSPARAM
        {
            public const string KERNEL = "March";
            public const string KERNEL2 = "March_NonInterpol";
            public const string RESULT = "Result";
            public const string triangles = "triangles";
            public const string points = "points";
            public const string isoLevel = "isoLevel";
            public const string mapSize = "numPointsPerAxis";
            public const string spacing = "spacing";
            public const string interpol = "interpol";
            public const int THREAD_NUMBER = 8;
        }
        private void Awake()
        {
            Application.targetFrameRate = 60;
        }
        private void OnValidate() 
        {
            if(trianglesBuffer == null)
            {
                return;
            }
        }

        int threadGroupNum;
        public void InitBuffers(int numPointsPerAxis, float boundSize, ComputeBuffer pointsBuffer)
        {
            int numVoxelsPerAxis = numPointsPerAxis - 1;
            int maxTriangleCount = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis * 5;
            trianglesBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 9, ComputeBufferType.Append);
            triCount = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

            shader.SetInt(CSPARAM.mapSize, numPointsPerAxis);
            shader.SetFloat (CSPARAM.spacing, boundSize / numVoxelsPerAxis);

            kernelID = shader.FindKernel(CSPARAM.KERNEL);
            shader.SetBuffer(kernelID, CSPARAM.points, pointsBuffer);
            shader.SetBuffer(kernelID, CSPARAM.triangles, trianglesBuffer);

            threadGroupNum = Mathf.CeilToInt(numPointsPerAxis * 1.0f / CSPARAM.THREAD_NUMBER);
        }

        public void ReleaseBuffers()
        {
            trianglesBuffer?.Release();
            triCount?.Release();
        }

        public void SetInterpolation(bool b)
        {
            shader.SetBool(CSPARAM.interpol, b);
        }
        public void SetIsoLevel(float l)
        {
            shader.SetFloat(CSPARAM.isoLevel, l);
        }

        public void March(Chunk chunk)
        {
            trianglesBuffer.SetCounterValue (0);
            
            shader.Dispatch(kernelID, threadGroupNum, threadGroupNum, threadGroupNum);

            //삼각형 개수 구하기.
            ComputeBuffer.CopyCount(trianglesBuffer,triCount,0);
            int[] triCountArray = {0};
            triCount.GetData(triCountArray);
            int numTris = triCountArray[0];

            //삼각형 배열 구하기
            Triangle[] triangles = new Triangle[numTris];
            trianglesBuffer.GetData(triangles,0,0,numTris);
            var vertices = new Vector3[numTris * 3];
              var meshTriangles = new int[numTris * 3];

            Parallel.For(0, numTris, (i) =>
              {
                //for (int i = 0; i < numTris; i++) {
                for (int j = 0; j < 3; j++)
                  {
                      meshTriangles[i * 3 + j] = i * 3 + j;
                      vertices[i * 3 + j] = triangles[i][j];
                  }
              });
            chunk.Set(vertices, meshTriangles);
            //UpdateMesh(vertices, meshTriangles);
        }
    }
    class Cube
    {
        public Vector3[] corners = new Vector3[8];

        public float[] values = new float[8];
        public void Set(int i, int j, int k, int m, float[] mapData)
        {
            corners[0] = new Vector3(i, j, k);
            corners[1] = new Vector3(i, j + 1, k);
            corners[2] = new Vector3(i + 1, j + 1, k);
            corners[3] = new Vector3(i + 1, j, k);

            corners[4] = new Vector3(i, j, k + 1);
            corners[5] = new Vector3(i, j + 1, k + 1);
            corners[6] = new Vector3(i + 1, j + 1, k + 1);
            corners[7] = new Vector3(i + 1, j, k + 1);

            values[0] = mapData[PosToIndex(i, j, k,m)];
            values[1] = mapData[PosToIndex(i, j + 1, k, m)];
            values[2] = mapData[PosToIndex(i + 1, j + 1, k, m)];
            values[3] = mapData[PosToIndex(i + 1, j, k, m)];

            values[4] = mapData[PosToIndex(i, j, k + 1, m)];
            values[5] = mapData[PosToIndex(i, j + 1, k + 1, m)];
            values[6] = mapData[PosToIndex(i + 1, j + 1, k + 1, m)];
            values[7] = mapData[PosToIndex(i + 1, j, k + 1, m)];
        }
        int PosToIndex(int x, int y, int z, int mapSize)
        {
            return z * mapSize * mapSize + y * mapSize + x;
        }
    }

    struct Triangle
    {
        #pragma warning disable 649 // disable unassigned variable warning
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 this [int i] {
            get {
                switch (i) {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }
}

