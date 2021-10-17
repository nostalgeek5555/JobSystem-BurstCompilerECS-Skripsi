using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using System.Threading.Tasks;
using Unity.Mathematics;

public class WaveGenerator : MonoBehaviour
{
    [Header("Wave Parameters")]
    public float waveScale;
    public float waveOffsetSpeed;
    public float waveHeight;

    [Header("References and Prefabs")]
    public MeshFilter waterMeshFilter;
    private Mesh waterMesh;

    //Private Mesh Job Properties
    NativeArray<Vector3> waterVertices;
    NativeArray<Vector3> waterNormals;

    //Job Handles
    UpdateMeshJob meshModificationJob;
    JobHandle meshModificationJobHandle;

    private void Start()
    {
        InitialiseData();
    }

    //This is where the appropriate mesh verticies are loaded in
    private void InitialiseData()
    {
        waterMesh = waterMeshFilter.mesh;

        //This allows Unity to make background modifications so that it can update the mesh quicker
        waterMesh.MarkDynamic();

        //The verticies will be reused throughout the life of the program so the Allocator has to be set to Persistent
        waterVertices = new NativeArray<Vector3>(waterMesh.vertices, Allocator.Persistent);
        waterNormals = new NativeArray<Vector3>(waterMesh.normals, Allocator.Persistent);
    }

    private void Update()
    {
        //Creating a job and assigning the variables within the Job
        meshModificationJob = new UpdateMeshJob()
        {
            vertices = waterVertices,
            normals = waterNormals,
            offsetSpeed = waveOffsetSpeed,
            time = Time.time,
            scale = waveScale,
            height = waveHeight
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
        public NativeArray<Vector3> vertices;
        public NativeArray<Vector3> normals;

        [ReadOnly]
        public float offsetSpeed;

        [ReadOnly]
        public float time;

        [ReadOnly]
        public float scale;

        [ReadOnly]
        public float height;

        public void Execute(int i)
        {
            //Vertex values are always between -1 and 1 (facing partially upwards)
            if (normals[i].z > 0f)
            {
                var vertex = vertices[i];

                float noiseValue = Noise(vertex.x * scale + offsetSpeed * time, vertex.y * scale + offsetSpeed * time);

                vertices[i] = new Vector3(vertex.x, vertex.y, noiseValue * height + 0.3f);
            }
        }

        private float Noise(float x, float y)
        {
            float2 pos = math.float2(x, y);
            return noise.snoise(pos);
        }
    }
}