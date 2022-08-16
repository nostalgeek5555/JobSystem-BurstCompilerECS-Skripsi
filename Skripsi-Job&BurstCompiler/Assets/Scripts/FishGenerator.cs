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
    [Header("Compiler Mode Controllers")]
    public MODE mode;
    
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

    //Handbuilt fish generator properties
    private Vector3[] velocitiesHbuilt;
    private Transform[] fishTransformHbuilt;

    [Header("Fish Generator Params Modifier UI")]
    public Slider swimSpeedSlider;
    public Slider turnSpeedSlider;
    public TextMeshProUGUI swimSpeedTMP;
    public TextMeshProUGUI turnSpeedTMP;

    private void Start()
    {
        swimSpeedTMP.text = swimSpeed.ToString();
        turnSpeedTMP.text = turnSpeed.ToString();

        if (mode == MODE.USE_DOTS)
        {
            velocities = new NativeArray<Vector3>(amountOfFish, Allocator.Persistent);
            transformAccessArray = new TransformAccessArray(amountOfFish);

            for (int i = 0; i < amountOfFish; i++)
            {
                float distanceX = Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);
                float distanceZ = Random.Range(-spawnBounds.z / 2, spawnBounds.z / 2);

                //Spawn off the ground at a height and in a random X and Z position without affecting height
                Vector3 spawnPoint = (transform.position + Vector3.up * spawnHeight) + new Vector3(distanceX, 0, distanceZ);

                //Creating transform and transform access at a spawn point
                Transform t = Instantiate(objectPrefab, spawnPoint, Quaternion.identity);
                transformAccessArray.Add(t);
            }
        }
        
        else
        {
            velocitiesHbuilt = new Vector3[amountOfFish];
            fishTransformHbuilt = new Transform[amountOfFish];

            for (int i = 0; i < amountOfFish; i++)
            {
                float distanceX = Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);
                float distanceZ = Random.Range(-spawnBounds.z / 2, spawnBounds.z / 2);

                //Spawn off the ground at a height and in a random X and Z position without affecting height
                Vector3 spawnPoint = (transform.position + Vector3.up * spawnHeight) + new Vector3(distanceX, 0, distanceZ);

                //Creating transform and transform access at a spawn point
                Transform t = Instantiate(objectPrefab, spawnPoint, Quaternion.identity);
                fishTransformHbuilt[i] = t;
            }
        }

        
    }

    private void Update()
    {
        if (mode == MODE.USE_DOTS)
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
        
        else
        {
            FishBehaviorUpdate();
        }
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


    #region HANDBUILT
    private void FishBehaviorUpdate()
    {
        for (int i = 0; i < fishTransformHbuilt.Length; i++)
        {
            Vector3 currentVelocity = velocitiesHbuilt[i];
            random randomGen = new random((uint)(i * Time.time + 1 + System.DateTimeOffset.Now.Millisecond));

            fishTransformHbuilt[i].position += fishTransformHbuilt[i].localToWorldMatrix.MultiplyVector(new Vector3(0, 0, 1)) * swimSpeed * Time.deltaTime * randomGen.NextFloat(0.3f, 1.0f);

            if (currentVelocity != Vector3.zero)
            {
                fishTransformHbuilt[i].rotation = Quaternion.Lerp(fishTransformHbuilt[i].rotation, Quaternion.LookRotation(currentVelocity), turnSpeed * Time.deltaTime);
            }

            Vector3 currentPosition = fishTransformHbuilt[i].position;

            bool randomise = true;
            if (currentPosition.x > waterObject.position.x + spawnBounds.x / 2 || currentPosition.x < waterObject.position.x - spawnBounds.x / 2 || currentPosition.z > waterObject.position.z + spawnBounds.z / 2 || currentPosition.z < waterObject.position.z - spawnBounds.z / 2)
            {
                Vector3 internalPosition = new Vector3(waterObject.position.x + randomGen.NextFloat(-spawnBounds.x / 2, spawnBounds.x / 2) / 1.3f, 0, waterObject.position.z + randomGen.NextFloat(-spawnBounds.z / 2, spawnBounds.z / 2) / 1.3f);
                currentVelocity = (internalPosition - currentPosition).normalized;
                velocitiesHbuilt[i] = currentVelocity;
                fishTransformHbuilt[i].rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(currentVelocity), turnSpeed * Time.deltaTime * 2);
                randomise = false;
            }

            if (randomise)
            {
                if (randomGen.NextInt(0, swimChangeFrequency) <= 2)
                {
                    velocitiesHbuilt[i] = new Vector3(randomGen.NextFloat(-1f, 1f), 0, randomGen.NextFloat(-1f, 1f));
                }
            }
        }
        
    }
    #endregion



    #region USING DOTS
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

    #endregion
    private void OnDestroy()
    {
        if (mode == MODE.USE_DOTS)
        {
            transformAccessArray.Dispose();
            velocities.Dispose();
        }
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

    public enum MODE
    {
        HANDBUILT = 0,
        USE_DOTS = 1
    }
}