using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneTrainer : MonoBehaviour
{
    public Transform targetTransform;
    public float episodeTime = 20f;
    public float dt = 0.02f;
    public bool useRealTime;
    private float _dt;

    private float episodeTimer;
    public GameObject droneObj;

    private DroneBehaviour[] _behaviours;
    private Visualizer _visualizer;

    public int generation = 1;

    void Start()
    {
        // Find all existing drones in the scene
        var drones = GameObject.FindGameObjectsWithTag("Drone");
        _behaviours = new DroneBehaviour[drones.Length];
        for (int i = 0; i < drones.Length; i++)
        {
            _behaviours[i] = drones[i].GetComponent<DroneBehaviour>();
        }
        episodeTimer = episodeTime;
        _visualizer = GameObject.FindGameObjectWithTag("Canvas").GetComponent<Visualizer>();
    }

    void Update()
    {
        _dt = dt;
        if (useRealTime)
            _dt = Time.deltaTime;
        episodeTimer -= _dt;
        if (episodeTimer <= 0f)
        {
            EndEpisode();
        }
    }

    void EndEpisode()
    {
        episodeTimer = episodeTime;

        // Collect scores and neural networks from all drones
        int droneCount = _behaviours.Length;
        List<(DroneBehaviour behaviour, float score)> rankedDrones = new List<(DroneBehaviour, float)>();

        // Track the best network
        float bestScore = float.NegativeInfinity;
        NeuralNetwork bestNetwork = null;
        float average = 0f;

        for (int i = 0; i < droneCount; i++)
        {
            var behaviour = _behaviours[i];
            float score = behaviour.score; // Use actual score, which may be negative
            rankedDrones.Add((behaviour, score));
            average += score / droneCount;

            // Check if this drone has the best score
            if (score > bestScore)
            {
                bestScore = score;
                bestNetwork = behaviour.nn;
            }
        }
        
        print("Average score: " + average);

        // Save the best network (overwriting the previous one)
        SaveBestNetwork(bestNetwork);
        _visualizer.targetNeuralNetwok = bestNetwork;
        _visualizer.Deselect();
        _visualizer.Setup(bestNetwork);

        // Sort drones by score in descending order
        rankedDrones.Sort((a, b) => b.score.CompareTo(a.score));

        // Initialize next generation networks
        List<NeuralNetwork> nextGenerationNns = new List<NeuralNetwork>();

        // 1. Top 4 agents survive to the next episode unchanged
        int survivorsCount = Mathf.Min(4, droneCount);
        for (int i = 0; i < survivorsCount; i++)
        {
            nextGenerationNns.Add(rankedDrones[i].behaviour.nn); // No mutation, unchanged
        }

        // Remove top 4 agents from consideration for reproduction
        int dronesLeft = droneCount - survivorsCount;
        if (dronesLeft > 0)
        {
            List<(DroneBehaviour behaviour, float score)> remainingDrones = rankedDrones.GetRange(survivorsCount, dronesLeft);

            // Calculate quarter count based on remaining drones
            int quarterCount = dronesLeft / 4;
            if (quarterCount == 0 && dronesLeft > 0)
                quarterCount = 1; // Ensure at least one drone in quarter if possible

            // 2. First quarter (excluding top 4 agents) reproduces twice
            for (int i = 0; i < quarterCount && nextGenerationNns.Count < droneCount; i++)
            {
                var nn = remainingDrones[i].behaviour.nn;
                nextGenerationNns.Add(nn.CreateMutation()); // First offspring
                nextGenerationNns.Add(nn.CreateMutation()); // Second offspring
            }

            // 3. Second quarter reproduces once
            for (int i = quarterCount; i < quarterCount * 2 && nextGenerationNns.Count < droneCount; i++)
            {
                var nn = remainingDrones[i].behaviour.nn;
                nextGenerationNns.Add(nn.CreateMutation()); // One offspring
            }

            // 4. Fill remaining slots with mutants
            while (nextGenerationNns.Count < droneCount)
            {
                var randomIndex = Random.Range(0, dronesLeft);
                var nn = remainingDrones[randomIndex].behaviour.nn;
                var mutant = nn.CreateMutation();
                mutant.Initialize(); // Reinitialize for diversity
                nextGenerationNns.Add(mutant);
            }
        }
        else
        {
            // If there are no drones left after survivors, fill the rest with mutants
            while (nextGenerationNns.Count < droneCount)
            {
                // Create new random neural networks
                NeuralNetwork mutant = new NeuralNetwork("DroneNN", NeuralNetwork.NetworkType.Other, bestNetwork.Inputs, bestNetwork.Outputs);
                mutant.Initialize();
                nextGenerationNns.Add(mutant);
            }
        }

        // Destroy all existing drones
        for (int i = 0; i < _behaviours.Length; i++)
        {
            Destroy(_behaviours[i].gameObject);
        }

        // Spawn new drones with the offspring neural networks
        _behaviours = new DroneBehaviour[droneCount];
        for (int i = 0; i < droneCount; i++)
        {
            // Instantiate a new drone at a random position
            Vector3 spawnPosition = GetRandomSpawnPosition();
            GameObject drone = Instantiate(droneObj, spawnPosition, Quaternion.identity);
            DroneBehaviour behaviour = drone.GetComponent<DroneBehaviour>();
            behaviour.nn = nextGenerationNns[i];
            behaviour.initializeNetwork = false; // Assuming the drone behavior checks this flag
            _behaviours[i] = behaviour;
        }

        _visualizer.generation.text = "Generation: " + (++generation).ToString();
    }

    Vector3 GetRandomSpawnPosition()
    {
        const float boxSize = 0f;
        // Define your spawn area here
        // For example, within a certain range on the X and Z axes
        float x = Random.Range(-boxSize, boxSize);
        float y = Random.Range(-boxSize, boxSize);
        float z = 0f; // Assuming drones spawn at y = 1
        return new Vector3(x, y, z);
    }

    void SaveBestNetwork(NeuralNetwork bestNetwork)
    {
        if (bestNetwork == null)
        {
            Debug.LogWarning("No best network found for this episode.");
            return;
        }

        // Define the path for saving the network (always overwrite the same file)
        string folderPath = "Assets/SavedNetworks";
        string fileName = "BestNetwork.asset"; // Single file, always overwritten
        string assetPath = System.IO.Path.Combine(folderPath, fileName);

#if UNITY_EDITOR
        // Ensure the folder exists
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "SavedNetworks");
        }

        // Save (overwrite) the network
        bestNetwork.SaveNetwork(assetPath);
        Debug.Log($"Best network saved at {assetPath}");
#else
        // Handle saving in builds if necessary
        Debug.LogWarning("Saving networks is only implemented for the Unity Editor.");
#endif
    }
}
