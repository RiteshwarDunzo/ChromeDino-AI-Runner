using UnityEngine;
using System;

public class Spawner : MonoBehaviour
{
    [System.Serializable]
    public struct SpawnableObject
    {
        public GameObject prefab;
        [Range(0f, 1f)]
        public float spawnChance;
    }

    public SpawnableObject[] objects;
    public float minSpawnRate = 2.0f;
    public float maxSpawnRate = 3.5f;

    [Header("Training (optional)")]
    public bool deterministicMode = false;
    public int deterministicSeed = 12345;
    public float deterministicSpawnInterval = 2.75f;

    private System.Random deterministicRng;

    private void OnEnable()
    {
        if (deterministicMode)
        {
            deterministicRng = new System.Random(deterministicSeed);
            Invoke(nameof(Spawn), deterministicSpawnInterval);
        }
        else
        {
            Invoke(nameof(Spawn), UnityEngine.Random.Range(minSpawnRate, maxSpawnRate));
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void Spawn()
    {
        float spawnChance = deterministicMode
            ? (float)deterministicRng.NextDouble()
            : UnityEngine.Random.value;

        foreach (SpawnableObject obj in objects)
        {
            // Remove birds from the game: skip any prefab named "Bird".
            if (obj.prefab != null && obj.prefab.name.Contains("Bird"))
            {
                continue;
            }

            if (spawnChance < obj.spawnChance)
            {
                GameObject obstacle = Instantiate(obj.prefab);
                obstacle.transform.position += transform.position;
                break;
            }

            spawnChance -= obj.spawnChance;
        }

        if (deterministicMode)
        {
            Invoke(nameof(Spawn), deterministicSpawnInterval);
        }
        else
        {
            Invoke(nameof(Spawn), UnityEngine.Random.Range(minSpawnRate, maxSpawnRate));
        }
    }

    public void SetDeterministic(bool enabled, int seed)
    {
        deterministicMode = enabled;
        deterministicSeed = seed;
        deterministicRng = enabled ? new System.Random(seed) : null;
        CancelInvoke();
        if (isActiveAndEnabled)
        {
            OnEnable();
        }
    }

}
