using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using UnityEngine.Jobs;


using math = Unity.Mathematics.math;
using random = Unity.Mathematics.Random;

public class FishGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform waterObject;
    public Transform objectPrefab;

    [Header("Spawn Settings")]
    public int amountOfFish;
    public Vector3 spawnBounds;
    public float spawnHeight;
    public int swimChangeFrequency;

    [Header("Settings")]
    public float swimSpeed;
    public float turnSpeed;

    private PositionUpdateJob positionUpdateJob;
    private JobHandle positionUpdateJobHandle;

    private NativeArray<Vector3> velocities;
    private TransformAccessArray transformAccessArray;

    [Header("Fish Generator Params Modifier UI")]
    public Slider swimSpeedSlider;
    public Slider turnSpeedSlider;
    public TextMeshProUGUI swimSpeedTMP;
    public TextMeshProUGUI turnSpeedTMP;

    private void Start()
    {
        swimSpeedTMP.text = swimSpeed.ToString();
        turnSpeedTMP.text = turnSpeed.ToString();

        velocities = new NativeArray<Vector3>(amountOfFish, Allocator.Persistent);
        transformAccessArray = new TransformAccessArray(amountOfFish);

        for (int i = 0; i < amountOfFish; i++)
        {
            float distanceX = Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);
            float distanceZ = Random.Range(-spawnBounds.z / 2, spawnBounds.z / 2);

            //Spawn off the ground at a height and in a random X and Z position without affecting height
            Vector3 spawnPoint = (transform.position + Vector3.up * spawnHeight) + new Vector3(distanceX, 0, distanceZ);

            //Creating transform and transform access at a spawn point
            Transform t = (Transform)Instantiate(objectPrefab, spawnPoint, Quaternion.identity);
            transformAccessArray.Add(t);
        }
    }

    private void Update()
    {
        //Setting parameters of position update
        positionUpdateJob = new PositionUpdateJob()
        {
            objectVelocities = velocities,
            jobDeltaTime = Time.deltaTime,
            swimSpeed = this.swimSpeed,
            turnSpeed = this.turnSpeed,
            time = Time.time,
            swimChangeFrequency = this.swimChangeFrequency,
            center = waterObject.position,
            bounds = spawnBounds,
            seed = System.DateTimeOffset.Now.Millisecond
        };

        positionUpdateJobHandle = positionUpdateJob.Schedule(transformAccessArray);
    }

    private void LateUpdate()
    {
        positionUpdateJobHandle.Complete();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position + Vector3.up * spawnHeight, spawnBounds);
    }

    [BurstCompile]
    struct PositionUpdateJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> objectVelocities;

        public Vector3 bounds;
        public Vector3 center;

        public float jobDeltaTime;
        public float time;
        public float swimSpeed;
        public float turnSpeed;

        public float seed;

        public int swimChangeFrequency;

        public void Execute(int i, TransformAccess transform)
        {
            Vector3 currentVelocity = objectVelocities[i];
            random randomGen = new random((uint)(i * time + 1 + seed));

            transform.position += transform.localToWorldMatrix.MultiplyVector(new Vector3(0, 0, 1)) * swimSpeed * jobDeltaTime * randomGen.NextFloat(0.3f, 1.0f);

            if (currentVelocity != Vector3.zero)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(currentVelocity), turnSpeed * jobDeltaTime);
            }

            Vector3 currentPosition = transform.position;

            bool randomise = true;
            if (currentPosition.x > center.x + bounds.x / 2 || currentPosition.x < center.x - bounds.x / 2 || currentPosition.z > center.z + bounds.z / 2 || currentPosition.z < center.z - bounds.z / 2)
            {
                Vector3 internalPosition = new Vector3(center.x + randomGen.NextFloat(-bounds.x / 2, bounds.x / 2) / 1.3f, 0, center.z + randomGen.NextFloat(-bounds.z / 2, bounds.z / 2) / 1.3f);
                currentVelocity = (internalPosition - currentPosition).normalized;
                objectVelocities[i] = currentVelocity;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(currentVelocity), turnSpeed * jobDeltaTime * 2);
                randomise = false;
            }

            if (randomise)
            {
                if (randomGen.NextInt(0, swimChangeFrequency) <= 2)
                {
                    objectVelocities[i] = new Vector3(randomGen.NextFloat(-1f, 1f), 0, randomGen.NextFloat(-1f, 1f));
                }
            }
        }
    }


    private void OnDestroy()
    {
        transformAccessArray.Dispose();
        velocities.Dispose();
    }

    public void AdjustSwimSpeed()
    {
        swimSpeed = swimSpeedSlider.value;
        swimSpeedTMP.text = swimSpeed.ToString();
    }

    public void AdjustTurnSpeed()
    {
        turnSpeed = turnSpeedSlider.value;
        turnSpeedTMP.text = turnSpeed.ToString();
    }
}