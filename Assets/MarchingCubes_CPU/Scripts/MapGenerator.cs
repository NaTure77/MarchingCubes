using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarchingCube_CPU
{
    public class MapGenerator : MonoBehaviour
    {
        float brushSpeed = 2;
        public bool PerlinFlow = false;
        public int mapSize = 50;
        float[,,] mapData;
        public float scale = 20f;
        public Vector3 offset = new Vector3();
        MeshGenerator meshGenerator;

        public static MapGenerator instance;

        void Awake()
        {
            instance = this;
            meshGenerator = GetComponent<MeshGenerator>();
            mapData = MakePerlin2D(scale,offset);
        }
        private void Update()
        {
            if(PerlinFlow)
            {
                offset.z += Time.deltaTime * 0.5f;
                mapData = MakePerlin2D(scale,offset);
                meshGenerator.March(mapData);
            }
        }
        void OnValidate()
        {
            if(meshGenerator == null)
            {
                 meshGenerator = GetComponent<MeshGenerator>();            
            }
            mapData = MakePerlin2D(scale,offset);
            meshGenerator.March(mapData);

        }
        bool IsIn(Vector3Int v)
        {
            return (v.x >= 0 && v.x < mapSize && v.y >= 0 && v.y < mapSize && v.z >= 0 && v.z < mapSize);
        }
        public void UseBrush(Vector3 p, bool EraseMode)
        {
            //Vector3 p = brush.position;
            Vector3Int pos = new Vector3Int((int)Mathf.Round(p.x),(int)Mathf.Round(p.y),(int)Mathf.Round(p.z));
            if(!IsIn(pos)) return;
            Vector3Int checker = new Vector3Int();
            for(int i = -5; i < 5; i++)
                for(int j = -5; j < 5; j++)
                    for(int k = -5; k < 5; k++)
                    {
                        float dist = Mathf.Sqrt(i * i + j * j + k * k);
                        if( dist <= 5)
                        {
                            checker.x = pos.x + i;
                            checker.y = pos.y + j;
                            checker.z = pos.z + k;
                            if(IsIn(checker))
                            {
                                if(EraseMode)
                                {
                                    if(mapData[checker.x,checker.y,checker.z] > 0)
                                    mapData[checker.x,checker.y,checker.z] -= Time.deltaTime * meshGenerator.isoLevel * brushSpeed;
                                }
                                 
                                else if(mapData[checker.x,checker.y,checker.z] <= meshGenerator.isoLevel + 10)
                                    mapData[checker.x,checker.y,checker.z] += Time.deltaTime * meshGenerator.isoLevel * brushSpeed;
                            }
                            
                        }
                    }
            meshGenerator.March(mapData);
        }

        float[,,] MakePerlin2D(float scale, Vector3 offset)
        {
            float[,,] mapData = new float[mapSize, mapSize, mapSize];

            int halfMapSize = mapSize / 2;
            for (int i = 0; i < mapSize; i++)
                for (int k = 0; k < mapSize; k++)
                {
                    float sample = Mathf.PerlinNoise(i * 0.1f * scale + offset.x, k * 0.1f * scale + offset.z);// * density * mapSize.y;
                    for (int j = 1; j < mapSize; j++)
                    {
                        mapData[i, j, k] = sample * 50 / (j)  + offset.y;
                    }
                }
            return MakeFloor(mapData, meshGenerator.isoLevel);
        }

        float[,,] MakeBasicMesh(float isoLevel)
        {
                float[,,] mapData = new float[mapSize, mapSize, mapSize];
                int halfMapSize = mapSize / 2;
                for (int i = 0; i < mapSize; i++)
                for (int k = 0; k < mapSize; k++)
                    for (int j = 0; j < halfMapSize; j++)
                    {
                        mapData[i, j, k] = isoLevel + 1;
                    }
            return mapData;
        }

        float[,,] MakeFloor(float[,,] map, float isoLevel)
        {
            for (int i = 0; i < mapSize; i++)
                for (int k = 0; k < mapSize; k++)
                    map[i,0,k] = isoLevel + 1;
            return map;
        }

        float[,,] MakePerlin3D(float scale, Vector3 offset)
        {
            float[,,] mapData = new float[mapSize, mapSize, mapSize];

            for (int i = 0; i < mapSize; i++)
                for (int j = 0; j < mapSize; j++)
                {
                    for (int k = 0; k < mapSize; k++)
                    {
                        float x = (float)i / 10 * scale + offset.x;
                        float y = (float)j / 10 * scale + offset.y;
                        float z = (float)k / 10 * scale + offset.z;
                        float xy = Mathf.PerlinNoise(x, y);
                        float xz = Mathf.PerlinNoise(x, z);
                        float yz = Mathf.PerlinNoise(y, z);
                        float yx = Mathf.PerlinNoise(y, x);
                        float zx = Mathf.PerlinNoise(z, x);
                        float zy = Mathf.PerlinNoise(z, y);
                        mapData[i, j, k] = (xy + xz + yz + yx + zx + zy) / 6f;
                    }
                }
            return mapData;
        }
    }
}
