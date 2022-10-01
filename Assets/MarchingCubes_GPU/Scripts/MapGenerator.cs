using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCube_GPU
{
    public class MapGenerator : MonoBehaviour
    {
        public Transform player;
        public Material material1;
        public Material material2;
        public ComputeShader shader;
        public bool PerlinFlow = false;

        [Header("Perlin Noise Setting")]
        public float scale = 20f;
        public Vector3 offset = new Vector3();

        [Header("Brush Setting")]
        public float brushSpeed = 1;
        public int brushSize = 10;
        int _brushSize = 10;

        [Header("Voxel Settings")]

        [Range(2, 128)]
        public int numPointsPerAxis = 50;

        int numVoxelPerAxis = 0;

        int numPointsInChunk = 0;
        public float boundsSize = 20;
        public float isoLevel = 0.5f;
        public bool interpol = false;

        //public int numPointsPerAxis = 100;

        [Space(10)]
        ComputeBuffer dataBuffer;
        MeshGenerator meshGenerator;
        public static MapGenerator instance;


        int kernelID0 = 0;
        int kernelID1 = 0;
        int kernelID2 = 0;
        int kernelID3 = 0;

        int threadGroupNum = 0;
        private struct CSPARAM
        {
            public const string KERNEL0 = "Generate";
            public const string KERNEL1 = "Generate3D";
            public const string KERNEL2 = "UseBrush";
            public const string KERNEL3 = "GenerateSphere";
            public const string RESULT = "Result";
            public const string points = "points";
            public const string mapSize = "numPointsPerAxis";
            public const string offset = "offset";
            public const string scale = "scale";
            public const int THREAD_NUMBER = 8;

            public const string brushValue = "brushValue";
            public const string brushSize = "brushSize";
            public const string spacing = "spacing";
            public const string chunkIdx3d = "chunkIdx3d";
            public const string nestedChunkPos = "nestedChunkPos"; //chunkIdx3d * (numPointsPerAxis - 1)
            public const string nestedBrushPos = "nestedBrushPos";
        }
        void Awake()
        {
            instance = this;
            meshGenerator = GetComponent<MeshGenerator>();
            
            InitKernelID();
            InitCSParams();
            InitBrush();
            InitChunk();
            StartCoroutine(UpdateChunks());

        }
        IEnumerator a = null;
        public void OnValidate()
        {
            if(Application.isPlaying)
            {

                if(dataBuffer != null)
                {
                    meshGenerator.SetInterpolation(interpol);
                    meshGenerator.SetIsoLevel(isoLevel);
                    if (numPointsPerAxis * numPointsPerAxis * numPointsPerAxis != numPointsInChunk)
                    {
                        ReleaseBuffers();
                        InitCSParams();
                        InitBrush();
                    }
                    else if (brushSize != _brushSize)
                    {
                        _brushSize = brushSize;
                        InitBrush();
                    }
                    
                    if(a != null) StopCoroutine(a);
                    a = RefreshAllChunks();
                    StartCoroutine(a);
                    //MakePerlin2D();
                }
            }
        }
        int PosToIndex(int x, int y, int z)
        {
            return z * numPointsPerAxis * numPointsPerAxis + y * numPointsPerAxis + x;
        }
        void InitKernelID()
        {
            kernelID0 = shader.FindKernel(CSPARAM.KERNEL0);
            kernelID1 = shader.FindKernel(CSPARAM.KERNEL1);
            kernelID2 = shader.FindKernel(CSPARAM.KERNEL2);
            kernelID3 = shader.FindKernel(CSPARAM.KERNEL3);
        }
        void InitCSParams()
        {
            numVoxelPerAxis = numPointsPerAxis - 1;
            numPointsInChunk = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            threadGroupNum = Mathf.CeilToInt(numPointsPerAxis * 1.0f / CSPARAM.THREAD_NUMBER);

            //Compute Shader 기본 변수 설정
            meshGenerator.SetInterpolation(interpol);
            meshGenerator.SetIsoLevel(isoLevel);
            shader.SetInt(CSPARAM.mapSize, numPointsPerAxis);
            shader.SetFloat(CSPARAM.spacing, boundsSize / (numPointsPerAxis - 1));
            
            //Compute shader 버퍼 생성
            dataBuffer = new ComputeBuffer(numPointsInChunk, sizeof(float));
            meshGenerator.InitCSParams(numPointsPerAxis, boundsSize, dataBuffer);
            shader.SetBuffer(kernelID1, CSPARAM.points, dataBuffer);
            //shader.SetBuffer(kernelID1, CSPARAM.points, dataBuffer);

            //jChunkData = new NativeArray<float>(numPointsInChunk, Allocator.TempJob);


        }
        void ReleaseBuffers()
        {
            dataBuffer.Release();
            meshGenerator.ReleaseBuffers();
            //jChunkData.Dispose();
        }

        void OnDestroy()
        {
            ReleaseBuffers();
        }

        //public Vector3Int chunkNum;

        public GameObject chunkHolder;
        Dictionary<Vector3Int,Chunk> chunks = null;// = new Dictionary<Vector3Int, Chunk>();
        List<Vector3Int> viewShape;
        public int viewDisatnce = 6;
       
        void InitChunk()
        {
            viewShape = new List<Vector3Int>();
            chunks = new Dictionary<Vector3Int, Chunk>();
            for (int i = -viewDisatnce; i < viewDisatnce; i++)
                for(int j = -viewDisatnce; j < viewDisatnce; j++)
                    for(int k = -viewDisatnce; k < viewDisatnce; k++)
                        if(Mathf.Sqrt(i * i + j * j + k * k) <= viewDisatnce)
                            viewShape.Add(new Vector3Int(i,j,k));
                
            viewShape.Sort((A,B)=>
            {
                return A.magnitude.CompareTo(B.magnitude);
            });

        }
        IEnumerator RefreshAllChunks()
        {                    
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            int ms = 16;

            foreach (Chunk chunk in chunks.Values)
            {
                chunk.transform.position = (Vector3)chunk.position * boundsSize;
                chunk.initMapData(numPointsInChunk);
                //dataBuffer.SetData(chunk.mapData);
                MakePerlin3D(chunk);
                meshGenerator.March(chunk);
                if (sw.ElapsedMilliseconds > ms)
                {
                    yield return null;
                    sw.Restart();
                }
            }
        }
        List<Vector3Int> recycledList = new List<Vector3Int>();
        List<Vector3Int> newChunkList = new List<Vector3Int>();
        List<Chunk> updateList = new List<Chunk>();
        public IEnumerator UpdateChunks()
        {
            Vector3 playerPos_before =  Vector3.zero;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            int ms = 16;
            while(true)
            {
                if(player.position != playerPos_before)
                {
                    playerPos_before = player.position;
                    Vector3 playerPos = player.position * numPointsPerAxis / boundsSize; 
                    Vector3Int pivotChunkIdx3d = new Vector3Int(Mathf.RoundToInt(playerPos.x), Mathf.RoundToInt(playerPos.y), Mathf.RoundToInt(playerPos.z)) / numPointsPerAxis;
                    recycledList.AddRange(chunks.Keys);
                    for(int i = 0; i < viewShape.Count; i++)
                    {
                        if (sw.ElapsedMilliseconds > ms)
                        {
                            yield return null;
                            sw.Restart();
                        }
                        Vector3Int chunkIdx3d = pivotChunkIdx3d + viewShape[i];
                        if(chunks.ContainsKey(chunkIdx3d))
                        {
                            recycledList.Remove(chunkIdx3d);
                        }
                        else
                        {
                            newChunkList.Add(chunkIdx3d);
                        }
                    }
                    Chunk chunk;
                    for(int i = 0; i < newChunkList.Count; i++)
                    {
                        if (sw.ElapsedMilliseconds > ms)
                        {
                            yield return null;
                            sw.Restart();
                        }
                        if(recycledList.Count <= i)
                        {
                            chunk = GenerateChunk(newChunkList[i],numPointsInChunk);
                        }
                        else 
                        {
                            chunk = chunks[recycledList[i]];
                            chunks.Remove(recycledList[i]);
                            chunk.position = newChunkList[i];
                        }
                        chunks.Add(newChunkList[i],chunk);
                        MakePerlin3D(chunk);
                        //while (!handle.IsCompleted) yield return null;
                    }
                    recycledList.Clear();
                    newChunkList.Clear();
                }
                yield return null;
            }
        }
        

        Chunk GenerateChunk(Vector3Int pos, int mapSize)
        {
            GameObject newChunk = new GameObject($"{pos.x},{pos.y},{pos.z}");
            newChunk.transform.SetParent(chunkHolder.transform);
            Chunk c = newChunk.AddComponent<Chunk>();

            if((pos.x + pos.y + pos.z) % 2 == 0)
                c.Init(pos, mapSize,material1);
            else
                c.Init(pos, mapSize,material2);
            return c;
        }

       
        List<Vector3Int> brushShape = new List<Vector3Int>();
        List<float> brushDist = new List<float>();
         void InitBrush()
        {
            brushShape.Clear();
            brushDist.Clear();

            float spacing = boundsSize / (numPointsPerAxis - 1);
            int brushSize_relative = Mathf.CeilToInt(brushSize / spacing);
            for (int i = -brushSize_relative; i < brushSize_relative; i++)
                for(int j = -brushSize_relative; j < brushSize_relative; j++)
                    for(int k = -brushSize_relative; k < brushSize_relative; k++)
                        if(Mathf.Sqrt(i * i + j * j + k * k) <= brushSize_relative)
                            brushShape.Add(new Vector3Int(i,j,k));

            brushShape.Sort((A,B)=>
            {
                return A.magnitude.CompareTo(B.magnitude);
            });
            for(int i = 0; i < brushShape.Count; i++)
            {
                brushDist.Add(brushSize_relative - brushShape[i].magnitude);
            }

            shader.SetInt(CSPARAM.brushSize,brushSize);
        }

        Vector3Int PosToChunkIdx3D(Vector3Int p)
        {
            return PosToChunkIdx3D(p.x,p.y,p.z);
        }
        Vector3Int PosToChunkIdx3D(float x, float y, float z)
        {
            Vector3Int result = new Vector3Int(Mathf.FloorToInt(x/numPointsPerAxis),Mathf.FloorToInt(y/numPointsPerAxis),Mathf.FloorToInt(z/numPointsPerAxis));
            return result;
        }

         Vector3Int PosToChunkIdx3D_nested(Vector3Int p)
        {
             Vector3Int result = new Vector3Int(Mathf.FloorToInt(p.x/numVoxelPerAxis),Mathf.FloorToInt(p.y/numVoxelPerAxis),Mathf.FloorToInt(p.z/numVoxelPerAxis));
            return result;
        }
        Vector3Int PosToChunkGap(Vector3Int pos, Vector3Int pivotChunkIdx3d)
        {
            Vector3Int chunkIdx3d = PosToChunkIdx3D(pos);
            Vector3Int chunkIdx3d_modified = PosToChunkIdx3D(pos + chunkIdx3d - pivotChunkIdx3d);

            //갭을 둔 후에 다른 청크로 가게 될 경우.
            while(chunkIdx3d != chunkIdx3d_modified)
            {
                chunkIdx3d = chunkIdx3d_modified;
                chunkIdx3d_modified = PosToChunkIdx3D(pos + chunkIdx3d - pivotChunkIdx3d);
            }
            return chunkIdx3d_modified;
        }

        public void SetNeighborChunk(Vector3Int localPos, Vector3Int chunkIdx3d, float value)
        {
            Vector3Int DI = Vector3Int.zero; // 대각일 경우 처리.
            Vector3Int chunkIdx3d_Temp;
            int cnt = 0;
            int localIndex = 0;
            Chunk chunk;

            void SetData()
            {
                if (chunks.ContainsKey(chunkIdx3d_Temp) && localIndex < numPointsInChunk && localIndex >= 0)
                {
                    chunk = chunks[chunkIdx3d_Temp];
                    chunk.mapData[localIndex] = value;
                    if (!updateList.Contains(chunk)) updateList.Add(chunk);
                }
            }
            if(localPos.x == numVoxelPerAxis)
            {
                cnt++;
                DI.x++;
                chunkIdx3d_Temp = chunkIdx3d;
                chunkIdx3d_Temp.x++;
                localIndex = PosToIndex(0,localPos.y,localPos.z);
                SetData();
            }
            if(localPos.y == numVoxelPerAxis)
            {
                cnt++;
                DI.y++;
                chunkIdx3d_Temp = chunkIdx3d;
                chunkIdx3d_Temp.y++;
                localIndex = PosToIndex(localPos.x,0,localPos.z);
                    
                SetData();
            }
            if(localPos.z == numVoxelPerAxis)
            {
                cnt++;
                DI.z++;
                chunkIdx3d_Temp = chunkIdx3d;
                chunkIdx3d_Temp.z++;
                localIndex = PosToIndex(localPos.x,localPos.y,0);
                SetData();
            }

            if(localPos.x == 0)
            {
                cnt++;
                DI.x--;
                chunkIdx3d_Temp = chunkIdx3d;
                chunkIdx3d_Temp.x--;
                localIndex = PosToIndex(numVoxelPerAxis,localPos.y,localPos.z);
                SetData();
            }
            if(localPos.y == 0)
            {
                cnt++;
                DI.y--;
                chunkIdx3d_Temp = chunkIdx3d;
                chunkIdx3d_Temp.y--;
                localIndex = PosToIndex(localPos.x,numVoxelPerAxis,localPos.z);
                SetData();
            }
            if(localPos.z == 0)
            {
                cnt++;
                DI.z--;
                chunkIdx3d_Temp = chunkIdx3d;
                chunkIdx3d_Temp.z--;
                localIndex = PosToIndex(localPos.x,localPos.y,numVoxelPerAxis);
                SetData();
            }
            if(cnt >= 2)
            {
                chunkIdx3d_Temp = chunkIdx3d + DI;
                int x = DI.x == 0 ? localPos.x : DI.x > 0 ? 0 : numVoxelPerAxis;
                int y = DI.y == 0 ? localPos.y : DI.y > 0 ? 0 : numVoxelPerAxis;
                int z = DI.z == 0 ? localPos.z : DI.z > 0 ? 0 : numVoxelPerAxis;
                localIndex = PosToIndex(x,y,z);
                SetData();
                if(cnt == 3)
                {
                    //xy
                    chunkIdx3d_Temp.z = chunkIdx3d.z;
                    localIndex = PosToIndex(x,y,localPos.z);
                    SetData();

                    //yz
                    chunkIdx3d_Temp.z = chunkIdx3d.z + DI.z;
                    chunkIdx3d_Temp.x = chunkIdx3d.x;
                    localIndex = PosToIndex(localPos.x,y,z);
                    SetData();

                    //xz
                    chunkIdx3d_Temp.x = chunkIdx3d.x + DI.x;
                    chunkIdx3d_Temp.y = chunkIdx3d.y;
                    localIndex = PosToIndex(x,localPos.y,z);
                    SetData();
                }
            }
        }
        public void UseBrush(Vector3 point, bool EraseMode)
        {      
            float brushValue = Time.deltaTime * brushSpeed * isoLevel;
            float max = 1;
            if(EraseMode) brushValue *= -1;
            point *= numPointsPerAxis / boundsSize; // spacing
            Vector3Int pos = new Vector3Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.z));

            Vector3Int pivotChunkIdx3d = PosToChunkIdx3D(pos); // 센터가 포함되어있는 청크의 인덱스.
            //Task.Run (()=>
            //{
                for(int i = 0; i < brushShape.Count; i++)
                {
                    Vector3Int p = pos + brushShape[i];
                    //Vector3Int chunkIdx3d = p / numVoxelPerAxis;
                    
                    Vector3Int chunkIdx3d = PosToChunkIdx3D(p);
                    Vector3Int chunkIdx3d_modified = PosToChunkIdx3D(p + chunkIdx3d - pivotChunkIdx3d);

                    //갭을 둔 후에 다른 청크로 가게 될 경우.
                    while(chunkIdx3d != chunkIdx3d_modified)
                    {
                        chunkIdx3d = chunkIdx3d_modified;
                        chunkIdx3d_modified = PosToChunkIdx3D(p + chunkIdx3d - pivotChunkIdx3d);
                    }
                    p += chunkIdx3d - pivotChunkIdx3d;


                    if(!chunks.ContainsKey(chunkIdx3d)) continue;

                    Chunk chunk = chunks[chunkIdx3d];
                    if (!updateList.Contains(chunk)) 
                    {
                        updateList.Add(chunk);
                    }
                    Vector3Int localPos = p - chunkIdx3d * numPointsPerAxis;
                    int localIndex = PosToIndex(localPos.x,localPos.y,localPos.z);
                    float chunkValue = chunk.mapData[localIndex] + (max - chunk.mapData[localIndex]) * brushDist[i] * brushValue;
                    chunk.mapData[localIndex] = chunkValue;
                    SetNeighborChunk(localPos,chunkIdx3d,chunkValue);
                }
                foreach(Chunk chunk in updateList)
                {
                    dataBuffer.SetData(chunk.mapData);
                    meshGenerator.March(chunk);
                }
                updateList.Clear();
            //});
        }

         public void MakePerlin2D(Chunk chunk)
        {
            chunk.transform.position = (Vector3)chunk.position * boundsSize;
            //shader.SetVector(CSPARAM.offset, offset + (Vector3)chunk.position * offsetGap);
            shader.SetVector(CSPARAM.nestedChunkPos,(Vector3)chunk.position * numVoxelPerAxis);
            shader.SetFloat(CSPARAM.scale, scale);
            shader.Dispatch(kernelID0, threadGroupNum, threadGroupNum, threadGroupNum);
            dataBuffer.GetData(chunk.mapData, 0, 0, chunk.mapData.Length);
            meshGenerator.March(chunk);
        }
        public void MakePerlin3D(Chunk chunk)
        {
            chunk.transform.position = (Vector3)chunk.position * boundsSize;
            //shader.SetVector(CSPARAM.offset, offset + (Vector3)chunk.position * offsetGap);
            shader.SetVector(CSPARAM.nestedChunkPos,(Vector3)chunk.position * numVoxelPerAxis);
            shader.SetFloat(CSPARAM.scale, scale);
            shader.Dispatch(kernelID1, threadGroupNum, threadGroupNum, threadGroupNum);
            dataBuffer.GetData(chunk.mapData, 0, 0, chunk.mapData.Length);
            meshGenerator.March(chunk);
        }

        public void MakeSphere(Chunk chunk,float offsetGap)
        {
            chunk.transform.position = (Vector3)chunk.position * boundsSize;
            shader.SetVector(CSPARAM.nestedChunkPos,(Vector3)chunk.position * numVoxelPerAxis);
            shader.Dispatch(kernelID3, threadGroupNum, threadGroupNum, threadGroupNum);
            dataBuffer.GetData(chunk.mapData, 0, 0, chunk.mapData.Length);
            meshGenerator.March(chunk);
        }
    }
}
