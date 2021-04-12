using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;

namespace MarchingCube_GPU
{
    public class MapGenerator : MonoBehaviour
    {
        public Material material1;
        public Material material2;
        public ComputeShader shader;
        public bool PerlinFlow = false;
        
        float[] mapData;

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
        public float boundsSize = 20;
        public float isoLevel = 0.5f;
        public bool interpol = false;

        //public int numPointsPerAxis = 100;

        [Space(10)]
        ComputeBuffer dataBuffer;
        MeshGenerator meshGenerator;

        ComputeBuffer brushS;
        ComputeBuffer brushD;
        public static MapGenerator instance;


        int kernelID = 0;
        int kernelID2 = 0;

        int threadGroupNum1 = 0;
        int threadGroupNum2 = 0;
        private struct CSPARAM
        {
            public const string KERNEL = "Generate";
            public const string KERNEL2 = "UseBrush";
            public const string RESULT = "Result";
            public const string points = "points";
            public const string mapSize = "numPointsPerAxis";
            public const string offset = "offset";
            public const string scale = "scale";
            public const int THREAD_NUMBER = 8;

            public const string brushShape = "brushShape";
            public const string brushDist = "brushDist";
            public const string brushPos = "brushPos";
            public const string brushValue = "brushValue";
            public const string brushSize = "brushSize";
            public const string brushLength = "brushLength";
            public const string brushMax = "brushMax";
            public const string spacing = "spacing";
            public const string chunkPos = "chunkPos";
        }



        void Init()
        {
            meshGenerator = GetComponent<MeshGenerator>();
            kernelID = shader.FindKernel(CSPARAM.KERNEL);
            kernelID2 = shader.FindKernel(CSPARAM.KERNEL2);
        }
        void Awake()
        {
            instance = this;
            Init();
            InitBrush();
            InitBuffers();
            //MakePerlin2D2();
            MakeFloor();
            //DrawClock();
            Debug.Log(chunks[0].mapData.Length * sizeof(float));
        }
        private void Update()
        {
            if(PerlinFlow)
            {
                offset.z += Time.deltaTime * 0.5f;
                MakePerlin2D();
            }
        }
        public void OnValidate()
        {
            if(meshGenerator == null) Init();

            meshGenerator.SetInterpolation(interpol);
            meshGenerator.SetIsoLevel(isoLevel);
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += UpdateChunks;
            else
                UpdateChunks();
            if (!Application.isPlaying)
            {
                InitBuffers();
                MakePerlin2D();
                ReleaseBuffers();
            }
            else
            {
                if(dataBuffer != null)
                {
                    if (numPointsPerAxis * numPointsPerAxis * numPointsPerAxis != dataBuffer.count)
                    {
                        ReleaseBuffers();
                        InitBuffers();
                        InitBrush();
                    }
                    else if (brushSize != _brushSize)
                    {
                        _brushSize = brushSize;
                        InitBrush();
                    }
                    foreach (Chunk chunk in chunks)
                    {
                        chunk.transform.position = (Vector3)chunk.position * boundsSize;
                        dataBuffer.SetData(chunk.mapData);
                        meshGenerator.March(chunk);
                    }
                    //MakePerlin2D();
                }
            }
        }
        int PosToIndex(int x, int y, int z)
        {
            return z * numPointsPerAxis * numPointsPerAxis + y * numPointsPerAxis + x;
        }
        void InitBuffers()
        {
            int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;

            dataBuffer = new ComputeBuffer(numPoints, sizeof(float));

            foreach(Chunk chunk in chunks) chunk.mapData = new float[numPoints];
            meshGenerator.InitBuffers(numPointsPerAxis, boundsSize, dataBuffer);
            shader.SetBuffer(kernelID2, CSPARAM.points, dataBuffer);
            shader.SetInt(CSPARAM.mapSize, numPointsPerAxis);
            shader.SetBuffer(kernelID, CSPARAM.points, dataBuffer);

            float spacing = boundsSize / (numPointsPerAxis - 1);
            shader.SetFloat(CSPARAM.spacing, spacing);

            threadGroupNum1 = Mathf.CeilToInt(numPointsPerAxis * 1.0f / CSPARAM.THREAD_NUMBER);
        }
        void ReleaseBuffers()
        {
            dataBuffer.Release();
            meshGenerator.ReleaseBuffers();
        }

        void OnDestroy()
        {
            if (brushS != null)
            {
                brushS.Release();
                brushD.Release();
            }
            ReleaseBuffers();
        }

        public Vector3Int chunkNum;

        public GameObject chunkHolder;
        public List<Chunk> chunks;
        public Chunk selectedChunk;
        Queue<Chunk> availableChunk;

        public void UpdateChunks()
        {

            List<Chunk> oldChunks = chunks;
            chunks = new List<Chunk>();

            int mapSize = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            for(int i = 0; i < chunkNum.x; i++)
                for (int j = 0; j < chunkNum.y; j++)
                    for (int k = 0; k < chunkNum.z; k++)
                    {
                        Vector3Int newPos = new Vector3Int(i, j, k);
                        bool recycled = false;
                        for(int l = 0; l < oldChunks.Count; l++)
                        {
                            if(oldChunks[l].position == newPos)
                            {
                                recycled = true;
                                chunks.Add(oldChunks[l]);
                                oldChunks.RemoveAt(l);
                                break;
                            }
                        }
                        if(!recycled)
                        {
                            chunks.Add(GenerateChunk(newPos, mapSize));
                        }
                    }
            //MakePerlin2D();
             foreach (Chunk chunk in oldChunks) chunk.Destroy();
            
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

        void SetChunk()
        {
            Chunk c = availableChunk.Dequeue();
        }

        public void DrawClock()
        {
            int clockRadius = 50;
            Clock clock = new Clock(clockRadius);
            
            char[,] clockDisplay = clock.GetDisplay();
            int width = clockRadius * 2 + 1;
            Vector3Int startPos = chunkNum * numPointsPerAxis / 2;
            startPos.x -= clockRadius;
            startPos.y -= clockRadius;
            startPos.z -= 10;

            List<Chunk> updateList = new List<Chunk>();
            List<int> idxList = new List<int>();

            for(int i = 0; i < clockDisplay.Length; i++)
            {
                int x = i % width;
                int y = i / width;

                Vector3Int p = new Vector3Int(startPos.x + x, startPos.y + y, startPos.z);
                int chunkIdx = PosToChunkIdx(p.x,p.y,p.z);
                if (chunkIdx < chunks.Count && chunkIdx >= 0)
                {
                    Chunk chunk = chunks[chunkIdx];
                    Vector3Int localPos = p - chunk.position * numPointsPerAxis;
                    int localIndex = PosToIndex(localPos.x,localPos.y,localPos.z);
                    updateList.Add(chunk);
                    idxList.Add(localIndex);
                }
            }
            
            void printMethod()
            {
                float subValue = Time.deltaTime * isoLevel * brushSpeed * 16;
                Parallel.For(0, clockDisplay.Length, (i) =>
                {
                    int x = i % width;
                    int y = i / width;
                    Chunk chunk = updateList[i];
                    int localIndex = idxList[i];
                    if (clockDisplay[x, width - y - 1] != ' ')
                    {
                        chunk.mapData[localIndex] = isoLevel + 1;
                    }
                    else chunk.mapData[localIndex] = Mathf.Max(chunk.mapData[localIndex] - subValue, 0);
                });
                foreach(Chunk chunk in updateList.Distinct().ToList())
                {
                    dataBuffer.SetData(chunk.mapData);
                    meshGenerator.March(chunk);
                }
                
            }
            StartCoroutine(clock.ClockCoroutine(0.1d,printMethod));
        }

        int PosToChunkIdx(int x, int y, int z)
        {
            return x / numPointsPerAxis * chunkNum.y * chunkNum.z + y / numPointsPerAxis * chunkNum.z + z / numPointsPerAxis;
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
            {
                for(int j = -brushSize_relative; j < brushSize_relative; j++)
                {
                    for(int k = -brushSize_relative; k < brushSize_relative; k++)
                    {
                        float dist = Mathf.Sqrt(i * i + j * j + k * k);
                        if(dist <= brushSize_relative)
                        {
                            brushShape.Add(new Vector3Int(i,j,k));
                            brushDist.Add(brushSize_relative / (dist + 1));
                        }
                    }
                }
            }
            if (brushS != null)
            {
                brushS.Release();
                brushD.Release();
            }
            /*brushS = new ComputeBuffer(brushShape.Count, sizeof(int));
            brushD = new ComputeBuffer(brushDist.Count, sizeof(float));
            brushS.SetData(brushShape.ToArray());
            brushD.SetData(brushDist.ToArray());
            shader.SetInt(CSPARAM.brushSize, brushSize_relative * 2);
            Debug.Log(brushSize_relative * 2);
            shader.SetInt(CSPARAM.brushLength, brushShape.Count);
            shader.SetBuffer(kernelID2, CSPARAM.brushShape, brushS);
            shader.SetBuffer(kernelID2, CSPARAM.brushDist, brushD);
            threadGroupNum2 = Mathf.CeilToInt((brushSize_relative * 2f) / CSPARAM.THREAD_NUMBER);*/

        }
        public void UseBrush(Vector3 point, bool EraseMode)
        {
            float brushValue = Time.deltaTime * isoLevel * brushSpeed;
            if(EraseMode) brushValue *= -1;

            int numVoxelPerAxis = numPointsPerAxis - 1;
            point *= numPointsPerAxis / boundsSize; // spacing

           // point += (Vector3Int.one * brushSize) / chunkNum * numPointsPerAxis;
            int numPointsInChunk = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            Vector3Int pos = new Vector3Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.z));
            //int chunkIdx = PosToChunkIdx(pos.x, pos.y, pos.z);
            //Chunk chunk = chunks[chunkIdx];
            List<Chunk> updateList = new List<Chunk>();

            Vector3Int chunkIdx3dCenter = pos / numPointsPerAxis; // 센터가 포함되어있는 청크의 인덱스.
            int chunkIdxCenter = PosToChunkIdx(pos.x,pos.y,pos.z);
            for(int i = 0; i < brushShape.Count; i++)
            {
                Vector3Int p = pos + brushShape[i];
                int chunkIdx = PosToChunkIdx(p.x, p.y, p.z);    
                Vector3Int chunkIdx3d = p / numPointsPerAxis;
                Vector3Int gap = chunkIdx3d - chunkIdx3dCenter;
                //청크간 갭 두기.
                Vector3Int p_temp = p + gap;
                int chunkIdx_temp = PosToChunkIdx(p_temp.x,p_temp.y,p_temp.z);

                //갭을 둔 후에 다른 청크로 가게 될 경우.
                while(chunkIdx != chunkIdx_temp)
                {
                    chunkIdx = chunkIdx_temp;
                    chunkIdx3d = p_temp / numPointsPerAxis;
                    gap = chunkIdx3d - chunkIdx3dCenter;
                    p_temp = p + gap;
                    chunkIdx_temp = PosToChunkIdx(p_temp.x,p_temp.y,p_temp.z);
                }
                if (chunkIdx >= chunks.Count || chunkIdx < 0) continue;
                p = p_temp;
                Chunk chunk = chunks[chunkIdx];
                Vector3Int localPos = p - chunk.position * numPointsPerAxis;//new Vector3Int(p.x - (int)chunk.position.x * numPointsPerAxis, p.y - (int)chunk.position.y * numPointsPerAxis, p.z - (int)chunk.position.z * numPointsPerAxis);
                
                int localIndex = PosToIndex(localPos.x,localPos.y,localPos.z);

                if(localPos.x >= numPointsPerAxis || localPos.y >= numPointsPerAxis || localPos.z >= numPointsPerAxis || 
                   localPos.x < 0 || localPos.y < 0 || localPos.z < 0) continue;
                chunk.mapData[localIndex] += brushValue * brushDist[i];

                float chunkValue = chunk.mapData[localIndex];
                if (!updateList.Contains(chunk)) updateList.Add(chunk);


                Vector3Int DI = p; // 대각일 경우 처리.
                int cnt = 0;
                if(localPos.x == numVoxelPerAxis && chunkIdx3d.x < chunkNum.x - 1)
                {
                    cnt++;
                    DI.x = p.x + 1;
                    chunkIdx = PosToChunkIdx(DI.x, p.y, p.z);
                    localIndex = PosToIndex(0,localPos.y,localPos.z);
                        
                    if (chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                }
                if(localPos.y == numVoxelPerAxis  && chunkIdx3d.y < chunkNum.y - 1)
                {
                    cnt++;
                    DI.y = p.y + 1;
                    chunkIdx = PosToChunkIdx(p.x, DI.y, p.z);
                    localIndex = PosToIndex(localPos.x,0,localPos.z);
                        
                    if (chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                }
                if(localPos.z == numVoxelPerAxis && chunkIdx3d.z < chunkNum.z - 1)
                {
                    cnt++;
                    DI.z = p.z + 1;
                    chunkIdx = PosToChunkIdx(p.x, p.y, DI.z);
                    localIndex = PosToIndex(localPos.x,localPos.y,0);
                        
                    if (chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                }

                if(localPos.x == 0 && chunkIdx3d.x > 0)
                {
                    cnt++;
                    DI.x = p.x - 1;
                    chunkIdx = PosToChunkIdx(DI.x, p.y, p.z);
                    localIndex = PosToIndex(numVoxelPerAxis,localPos.y,localPos.z);
                        
                    if (chunkIdx >= 0 && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                }
                if(localPos.y == 0 && chunkIdx3d.y > 0)
                {
                    cnt++;
                    DI.y = p.y - 1;
                    chunkIdx = PosToChunkIdx(p.x, DI.y, p.z);
                    localIndex = PosToIndex(localPos.x,numVoxelPerAxis,localPos.z);
                        
                    if (chunkIdx >= 0 && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                }
                if(localPos.z == 0 && chunkIdx3d.z > 0)
                {
                    cnt++;
                    DI.z = p.z - 1;
                    chunkIdx = PosToChunkIdx(p.x, p.y, DI.z);
                    localIndex = PosToIndex(localPos.x,localPos.y,numVoxelPerAxis);
                        
                    if (chunkIdx >= 0 && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                }
                if(cnt >= 2)
                {
                    chunkIdx = PosToChunkIdx(DI.x,DI.y, DI.z);
                    int x = DI.x == p.x ? localPos.x : DI.x > p.x ? 0 : numVoxelPerAxis;
                    int y = DI.y == p.y ? localPos.y : DI.y > p.y ? 0 : numVoxelPerAxis;
                    int z = DI.z == p.z ? localPos.z : DI.z > p.z ? 0 : numVoxelPerAxis;
                    localIndex = PosToIndex(x,y,z);
                    if (chunkIdx >= 0 && chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                    {
                        chunk = chunks[chunkIdx];
                        chunk.mapData[localIndex] = chunkValue;
                        if (!updateList.Contains(chunk)) updateList.Add(chunk);
                    }
                    if(cnt == 3)
                    {
                        //xy
                        chunkIdx = PosToChunkIdx(DI.x, DI.y, p.z);
                        localIndex = PosToIndex(x,y,localPos.z);
                        if (chunkIdx >= 0 && chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                        {
                            chunk = chunks[chunkIdx];
                            chunk.mapData[localIndex] = chunkValue;
                            if (!updateList.Contains(chunk)) updateList.Add(chunk);
                        }

                        //yz
                        chunkIdx = PosToChunkIdx(p.x, DI.y, DI.z);
                        localIndex = PosToIndex(localPos.x,y,z);
                        if (chunkIdx >= 0 && chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                        {
                            chunk = chunks[chunkIdx];
                            chunk.mapData[localIndex] = chunkValue;
                            if (!updateList.Contains(chunk)) updateList.Add(chunk);
                        }

                        //xz
                        chunkIdx = PosToChunkIdx(DI.x, p.y, DI.z);
                        localIndex = PosToIndex(x,localPos.y,z);
                        if (chunkIdx >= 0 && chunkIdx < chunks.Count && localIndex < numPointsInChunk && localIndex >= 0)
                        {
                            chunk = chunks[chunkIdx];
                            chunk.mapData[localIndex] = chunkValue;
                            if (!updateList.Contains(chunk)) updateList.Add(chunk);
                        }
                    }
                }
            }
            foreach(Chunk chunk in updateList)
            {
                dataBuffer.SetData(chunk.mapData);
                meshGenerator.March(chunk);
            }

        }
        public void MakePerlin2D()
        {
            float offsetGap = boundsSize * scale;
            foreach(Chunk chunk in chunks)
            {
                chunk.transform.position = (Vector3)chunk.position * boundsSize;
                shader.SetVector(CSPARAM.offset, offset + (Vector3)chunk.position * offsetGap);
                shader.SetVector(CSPARAM.chunkPos,(Vector3)chunk.position);
                shader.SetFloat(CSPARAM.scale, scale);
                shader.Dispatch(kernelID, threadGroupNum1, threadGroupNum1, threadGroupNum1);
                dataBuffer.GetData(chunk.mapData, 0, 0, chunk.mapData.Length);
                meshGenerator.March(chunk);
            }
            
        }
        void MakePerlin2D2()
        {
            float offsetGap = boundsSize * scale;
            int halfMapSize = numPointsPerAxis / 2;
            float spacing = boundsSize / (numPointsPerAxis - 1);
            foreach(Chunk chunk in chunks)
            {
                chunk.transform.position = (Vector3)chunk.position * boundsSize;
                Vector3 off = offset + (Vector3)chunk.position * offsetGap;
                for (int i = 0; i < numPointsPerAxis; i++)
                    for (int k = 0; k < numPointsPerAxis; k++)
                    {
                        float sample = Mathf.PerlinNoise(i * scale * spacing + off.x, k * scale * spacing + off.z);// * density * mapSize.y;
                        for (int j = 1; j < numPointsPerAxis; j++)
                        {
                            float val = sample * 50 / (j * spacing + chunk.position.y * numPointsPerAxis * spacing + offset.y + 1);
                            if(val < isoLevel / 2) val = 0;
                            //else if (val > isoLevel) val = isoLevel;
                            chunk.mapData[PosToIndex(i, j, k)] = val;
                            //50 / (j)  + off.y;
                        }
                    }
                dataBuffer.SetData(chunk.mapData);
                meshGenerator.March(chunk);
            }
        }
        void MakePerlin3D()
        {
            float offsetGap = boundsSize * scale;
            float spacing = boundsSize / (numPointsPerAxis - 1);
            foreach(Chunk chunk in chunks)
            {
                Vector3 off = offset + (Vector3)chunk.position * offsetGap * numPointsPerAxis;
                for (int i = 0; i < numPointsPerAxis; i++)
                    for (int j = 0; j < numPointsPerAxis; j++)
                    {
                        for (int k = 0; k < numPointsPerAxis; k++)
                        {
                            float x = i * spacing * scale + off.x;
                            float y = j * spacing * scale + off.y;
                            float z = k * spacing * scale + off.z;
                            float xy = Mathf.PerlinNoise(x, y);
                            float xz = Mathf.PerlinNoise(x, z);
                            float yz = Mathf.PerlinNoise(y, z);
                            float yx = Mathf.PerlinNoise(y, x);
                            float zx = Mathf.PerlinNoise(z, x);
                            float zy = Mathf.PerlinNoise(z, y);
                            chunk.mapData[PosToIndex(i,j,k)] = (xy + xz + yz + yx + zx + zy) / 6f;
                        }
                    }
                dataBuffer.SetData(chunk.mapData);
                meshGenerator.March(chunk);
            }
        }
        void MakeFloor()
        {
            int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            foreach(Chunk chunk in chunks)
            {
                chunk.transform.position = (Vector3)chunk.position * boundsSize;
                if(chunk.position.y == 0)
                {
                    for(int i = 0; i < numPointsPerAxis; i++)
                        for(int j = 0; j < numPointsPerAxis; j++)
                        {
                            chunk.mapData[PosToIndex(i,0,j)] = isoLevel + 1;
                        }
                }
                else chunk.mapData = new float[numPoints];
                dataBuffer.SetData(chunk.mapData);
                meshGenerator.March(chunk);
            }
        }
    }
}
