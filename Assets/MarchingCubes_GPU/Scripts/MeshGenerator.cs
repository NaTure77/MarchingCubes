using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System;
using System.Runtime.InteropServices;
using System.IO;

namespace MarchingCube_GPU
{
    public class MeshGenerator : MonoBehaviour
    {
        public float viewDistance = 37f;
        public ComputeShader shader;
        ComputeBuffer triCount;
        ComputeBuffer trianglesBuffer;
       
        
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

        
        int kernelID = 0;
        int threadGroupNum;

        int[] triangleOrder;

        public void InitCSParams(int numPointsPerAxis, float boundSize, ComputeBuffer pointsBuffer)
        {
            int numVoxelsPerAxis = numPointsPerAxis - 1;
            int maxTriangleCount = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis * 5;

            triangleOrder = new int[maxTriangleCount];
            for(int i = 0; i < maxTriangleCount; i++) triangleOrder[i] = i;

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


        MakeMeshArray job;
        JobHandle handle;
        
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
            Triangle[] t = new Triangle[numTris];
            Vector3[] vertices = new Vector3[numTris * 3];
            int[] meshTriangles = new int[numTris * 3];
            Array.Copy(triangleOrder,meshTriangles,numTris * 3);
            trianglesBuffer.GetData(t,0,0,numTris);
            
            Parallel.For(0, numTris, (i) =>
            {
                int triIdx = i * 3;
                vertices[triIdx] = t[i].a;
                vertices[triIdx + 1] = t[i].b;
                vertices[triIdx + 2] = t[i].c;
            });
            
            /*NativeArray<int> tri = new NativeArray<int>(numTris * 3, Allocator.TempJob);
            job.meshTriangles = tri;
            handle = job.Schedule(numTris * 3,64); 10, 5  0~ 4, 5 ~
            handle.Complete();
            tri.Dispose();*/
            chunk.Set(vertices,meshTriangles);
            
            //StartCoroutine(CoMakeMeshArray(chunk,vertices,numTris));
            
            
        }
    }
    struct MakeMeshArray : IJobParallelFor
    {
        public NativeArray<int> meshTriangles;
        public void Execute(int i)
        {
            meshTriangles[i + 10] = i;
        }
    }
    struct Triangle
    {
        
        #pragma warning disable 649 // disable unassigned variable warning
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
    }
}

