using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine.UI;
using TMPro;

public class WaveGenerator : MonoBehaviour
{
    [Header("Wave Parameters")]
    public float waveScale;
    public float waveOffsetSpeed;
    public float waveHeight;

    [Header("Perlin Noise Parameters")]
    public float noiseFrequency = 1f;

    [Range(1, 8)]
    public int noiseOctaves = 1;

    [Range(1f, 4f)]
    public float noiseLacunarity = 2f;

    [Range(0f, 1f)]
    public float noisePersistence = 0.5f;


    [Header("References and Prefabs")]
    public MeshFilter waterMeshFilter;
    private Mesh waterMesh;

    //Private Mesh Job Properties
    NativeArray<int> hashVal;
    NativeArray<Vector3> gradient3D;
    NativeArray<Vector3> waterVertices;
    NativeArray<Vector3> waterNormals;

    //Job Handles
    UpdateMeshJob meshModificationJob;
    JobHandle meshModificationJobHandle;

    [Header("Parameter Modifiers UI")]
    public Slider waveScaleSlider;
    public Slider waveOffsetSpeedSlider;
    public Slider waveHeightSlider;
    public TextMeshProUGUI waveScaleTMP;
    public TextMeshProUGUI waveOffsetSpeedTMP;
    public TextMeshProUGUI waveHeightTMP;

    private void Start()
    {
        InitialiseData();
    }

    //This is where the appropriate mesh verticies are loaded in
    private void InitialiseData()
    {
        waveScaleTMP.text = waveScale.ToString();
        waveOffsetSpeedTMP.text = waveOffsetSpeed.ToString();
        waveHeightTMP.text = waveHeight.ToString();

        waterMesh = waterMeshFilter.mesh;

        //This allows Unity to make background modifications so that it can update the mesh quicker
        waterMesh.MarkDynamic();

        //The verticies will be reused throughout the life of the program so the Allocator has to be set to Persistent
        waterVertices = new NativeArray<Vector3>(waterMesh.vertices, Allocator.Persistent);
        waterNormals = new NativeArray<Vector3>(waterMesh.normals, Allocator.Persistent);

        //hash and other properties to calculate perlin noise value throughout the program
        hashVal = new NativeArray<int>(hash, Allocator.Persistent);
        gradient3D = new NativeArray<Vector3>(gradients3D, Allocator.Persistent);
    }

    private void Update()
    {
        //Creating a job and assigning the variables within the Job
        meshModificationJob = new UpdateMeshJob()
        {
            hashValues = hashVal,
            gradient3DS = gradient3D,
            vertices = waterVertices,
            normals = waterNormals,
            offsetSpeed = waveOffsetSpeed,
            time = Time.time,
            scale = waveScale,
            height = waveHeight,

            frequency = noiseFrequency,
            octaves = noiseOctaves,
            lacunarity = noiseLacunarity,
            persistence = noisePersistence
        };

        //Setup of the job handle
        meshModificationJobHandle = meshModificationJob.Schedule(waterVertices.Length, 64);
    }

    private void LateUpdate()
    {
        //Ensuring the completion of the job
        meshModificationJobHandle.Complete();

        //Set the vertices directly
        waterMesh.SetVertices(meshModificationJob.vertices);

        //Most expensive
        waterMesh.RecalculateNormals();
    }

    private void OnDestroy()
    {
        // make sure to Dispose any NativeArrays when you're done
        waterVertices.Dispose();
        waterNormals.Dispose();
    }

    [BurstCompile]
    private struct UpdateMeshJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<int> hashValues;

        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> gradient3DS;

        public NativeArray<Vector3> vertices;
        public NativeArray<Vector3> normals;

        [ReadOnly]
        private const int hashMask = 255;

        [ReadOnly]
        private const int gradientsMask3D = 15;

        [ReadOnly]
        public float offsetSpeed;

        [ReadOnly]
        public float time;

        [ReadOnly]
        public float scale;

        [ReadOnly]
        public float height;

        [ReadOnly]
        public float frequency;

        [ReadOnly]
        public int octaves;

        [ReadOnly]
        public float lacunarity;

        [ReadOnly]
        public float persistence;

        public void Execute(int i)
        {
            //Vertex values are always between -1 and 1 (facing partially upwards)
            if (normals[i].z > 0f)
            {
                var vertex = vertices[i];


                float noiseValue = Noise(vertex.x * scale + offsetSpeed * time, vertex.y * scale + offsetSpeed * time, vertex.z);

                vertices[i] = new Vector3(vertex.x, vertex.y, noiseValue * height + 0.3f);
            }
        }

        private float Noise(float x, float y, float z)
        {
            float3 pos = math.float3(x, y, z);
            return Sum(pos, frequency, octaves, lacunarity, persistence);
        }

        private float Sum(Vector3 point, float frequency, int octaves, float lacunarity, float persistence)
        {
            float sum = Perlin3D(point, frequency);
            float amplitude = 1f;
            float range = 1f;
            for (int o = 1; o < octaves; o++)
            {
                frequency *= lacunarity;
                amplitude *= persistence;
                range += amplitude;
                sum += Perlin3D(point, frequency) * amplitude;
            }
            return sum / range;
        }

        private float Dot(Vector3 g, float x, float y, float z)
        {
            return g.x * x + g.y * y + g.z * z;
        }

        private float Smooth(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }


        private float Perlin3D(Vector3 point, float frequency)
        {
            point *= frequency;
            int ix0 = Mathf.FloorToInt(point.x);
            int iy0 = Mathf.FloorToInt(point.y);
            int iz0 = Mathf.FloorToInt(point.z);
            float tx0 = point.x - ix0;
            float ty0 = point.y - iy0;
            float tz0 = point.z - iz0;
            float tx1 = tx0 - 1f;
            float ty1 = ty0 - 1f;
            float tz1 = tz0 - 1f;
            ix0 &= hashMask;
            iy0 &= hashMask;
            iz0 &= hashMask;
            int ix1 = ix0 + 1;
            int iy1 = iy0 + 1;
            int iz1 = iz0 + 1;

            int h0 = hashValues[ix0];
            int h1 = hashValues[ix1];
            int h00 = hashValues[h0 + iy0];
            int h10 = hashValues[h1 + iy0];
            int h01 = hashValues[h0 + iy1];
            int h11 = hashValues[h1 + iy1];
            Vector3 g000 = gradient3DS[hashValues[h00 + iz0] & gradientsMask3D];
            Vector3 g100 = gradient3DS[hashValues[h10 + iz0] & gradientsMask3D];
            Vector3 g010 = gradient3DS[hashValues[h01 + iz0] & gradientsMask3D];
            Vector3 g110 = gradient3DS[hashValues[h11 + iz0] & gradientsMask3D];
            Vector3 g001 = gradient3DS[hashValues[h00 + iz1] & gradientsMask3D];
            Vector3 g101 = gradient3DS[hashValues[h10 + iz1] & gradientsMask3D];
            Vector3 g011 = gradient3DS[hashValues[h01 + iz1] & gradientsMask3D];
            Vector3 g111 = gradient3DS[hashValues[h11 + iz1] & gradientsMask3D];

            float v000 = Dot(g000, tx0, ty0, tz0);
            float v100 = Dot(g100, tx1, ty0, tz0);
            float v010 = Dot(g010, tx0, ty1, tz0);
            float v110 = Dot(g110, tx1, ty1, tz0);
            float v001 = Dot(g001, tx0, ty0, tz1);
            float v101 = Dot(g101, tx1, ty0, tz1);
            float v011 = Dot(g011, tx0, ty1, tz1);
            float v111 = Dot(g111, tx1, ty1, tz1);

            float tx = Smooth(tx0);
            float ty = Smooth(ty0);
            float tz = Smooth(tz0);
            return Mathf.Lerp(
                Mathf.Lerp(Mathf.Lerp(v000, v100, tx), Mathf.Lerp(v010, v110, tx), ty),
                Mathf.Lerp(Mathf.Lerp(v001, v101, tx), Mathf.Lerp(v011, v111, tx), ty),
                tz);
        }
    }

    #region Perlin Noise Properties

    private int[] hash = {
        151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
        140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
        247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
        57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
        74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
        60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
        65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
        200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
        52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
        207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
        119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
        129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
        218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
        81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
        184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
        222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180,

        151,160,137, 91, 90, 15,131, 13,201, 95, 96, 53,194,233,  7,225,
        140, 36,103, 30, 69,142,  8, 99, 37,240, 21, 10, 23,190,  6,148,
        247,120,234, 75,  0, 26,197, 62, 94,252,219,203,117, 35, 11, 32,
        57,177, 33, 88,237,149, 56, 87,174, 20,125,136,171,168, 68,175,
        74,165, 71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
        60,211,133,230,220,105, 92, 41, 55, 46,245, 40,244,102,143, 54,
        65, 25, 63,161,  1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
        200,196,135,130,116,188,159, 86,164,100,109,198,173,186,  3, 64,
        52,217,226,250,124,123,  5,202, 38,147,118,126,255, 82, 85,212,
        207,206, 59,227, 47, 16, 58, 17,182,189, 28, 42,223,183,170,213,
        119,248,152,  2, 44,154,163, 70,221,153,101,155,167, 43,172,  9,
        129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
        218,246, 97,228,251, 34,242,193,238,210,144, 12,191,179,162,241,
        81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
        184, 84,204,176,115,121, 50, 45,127,  4,150,254,138,236,205, 93,
        222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180
    };

    

    private Vector3[] gradients3D = {
        new Vector3( 1f, 1f, 0f),
        new Vector3(-1f, 1f, 0f),
        new Vector3( 1f,-1f, 0f),
        new Vector3(-1f,-1f, 0f),
        new Vector3( 1f, 0f, 1f),
        new Vector3(-1f, 0f, 1f),
        new Vector3( 1f, 0f,-1f),
        new Vector3(-1f, 0f,-1f),
        new Vector3( 0f, 1f, 1f),
        new Vector3( 0f,-1f, 1f),
        new Vector3( 0f, 1f,-1f),
        new Vector3( 0f,-1f,-1f),

        new Vector3( 1f, 1f, 0f),
        new Vector3(-1f, 1f, 0f),
        new Vector3( 0f,-1f, 1f),
        new Vector3( 0f,-1f,-1f)
    };

    #endregion
    #region
    public void AdjustWaveScale()
    {
        waveScale = waveScaleSlider.value;
        waveScaleTMP.text = waveScale.ToString();
    }

    public void AdjustWaveOffsetSpeed()
    {
        waveOffsetSpeed = waveOffsetSpeedSlider.value;
        waveOffsetSpeedTMP.text = waveOffsetSpeed.ToString();
    }

    public void AdjustWaveHeight()
    {
        waveHeight = waveHeightSlider.value;
        waveHeightTMP.text = waveHeight.ToString();
    }
    #endregion
}