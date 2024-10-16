using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; // Import System.IO for file operations

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

    // 1. Configurable Number of Preserved Top Agents
    [Header("Preservation Settings")]
    [Tooltip("Number of top-performing agents to preserve each generation.")]
    public int preservedTopAgents = 4; // Number of top agents to preserve

    // 2. Dynamic Reproduction Ratio Settings
    [Header("Reproduction Ratio Settings")]
    [Tooltip("Initial ratio of mutants at generation 1.")]
    public float initialMutantRatio = 1f; // Ratio at generation 1
    [Tooltip("Final ratio of mutants at max generation.")]
    public float finalMutantRatio = 0f;   // Ratio at max generation
    [Tooltip("Generation at which mutant ratio reaches final value.")]
    public int maxGeneration = 1000;      // Generation at which mutant ratio reaches final value

    // 3. Dynamic Mutation Rates Settings
    [Header("Mutation Rate Settings")]
    [Tooltip("Initial connection mutation rate.")]
    public float initialConnectionMutationRate = 0.05f; // Initial connection mutation rate
    [Tooltip("Final connection mutation rate.")]
    public float finalConnectionMutationRate = 0.01f;   // Final connection mutation rate
    [Tooltip("Initial perceptron mutation rate.")]
    public float initialPerceptronMutationRate = 0.1f;  // Initial perceptron mutation rate
    [Tooltip("Final perceptron mutation rate.")]
    public float finalPerceptronMutationRate = 0.02f;   // Final perceptron mutation rate
    [Tooltip("Generation at which mutation rates reach final values.")]
    public int maxGenerationForMutationRate = 1000;     // Generation at which mutation rates reach final values

    // 4. Dynamic Spawn Area Size Settings
    [Header("Spawn Area Settings")]
    [Tooltip("Initial spawn box size.")]
    public float initialBoxSize = 0f;   // Initial spawn box size
    [Tooltip("Final spawn box size.")]
    public float finalBoxSize = 10f;    // Final spawn box size

    // 5. Logging Settings
    [Header("Logging Settings")]
    [Tooltip("Name of the log file.")]
    public string logFileName = "log.txt"; // Log file name

    private string logFilePath; // Path to the log file

    void Start()
    {
        // Initialize drones
        var drones = GameObject.FindGameObjectsWithTag("Drone");
        _behaviours = new DroneBehaviour[drones.Length];
        for (int i = 0; i < drones.Length; i++)
        {
            _behaviours[i] = drones[i].GetComponent<DroneBehaviour>();
        }
        episodeTimer = episodeTime;

        // Initialize Visualizer
        _visualizer = GameObject.FindGameObjectWithTag("Canvas").GetComponent<Visualizer>();

        // 2. Initialize Log File Path
        logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        InitializeLogFile();
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

        int droneCount = _behaviours.Length;
        List<(DroneBehaviour behaviour, float score)> rankedDrones = new List<(DroneBehaviour, float)>();

        float bestScore = float.NegativeInfinity;
        NeuralNetwork bestNetwork = null;
        float average = 0f;

        // Collect scores and identify the best network
        for (int i = 0; i < droneCount; i++)
        {
            var behaviour = _behaviours[i];
            float score = behaviour.score;
            rankedDrones.Add((behaviour, score));
            average += score / droneCount;

            if (score > bestScore)
            {
                bestScore = score;
                bestNetwork = behaviour.nn;
            }
        }

        Debug.Log("Generation " + generation + " - Average Score: " + average + ", Best Score: " + bestScore);

        // 3. Log the scores
        LogGenerationScores(generation, average, bestScore);

        // Save the best network
        SaveBestNetwork(bestNetwork);
        _visualizer.targetNeuralNetwork = bestNetwork;
        _visualizer.Deselect();
        _visualizer.Setup(bestNetwork);

        // Sort drones by score in descending order
        rankedDrones.Sort((a, b) => b.score.CompareTo(a.score));

        // 4. Calculate Current Mutation Ratios with Clamping
        float tRatio = Mathf.Clamp01((float)generation / maxGeneration);
        float mutantRatio = Mathf.Lerp(initialMutantRatio, finalMutantRatio, tRatio);

        float tMutationRate = Mathf.Clamp01((float)generation / maxGenerationForMutationRate);
        float connectionMutationRate = Mathf.Lerp(initialConnectionMutationRate, finalConnectionMutationRate, tMutationRate);
        float perceptronMutationRate = Mathf.Lerp(initialPerceptronMutationRate, finalPerceptronMutationRate, tMutationRate);

        // 5. Calculate Current Spawn Box Size with Clamping
        float tBoxSize = Mathf.Clamp01((float)generation / maxGeneration);
        float currentBoxSize = Mathf.Lerp(initialBoxSize, finalBoxSize, tBoxSize);

        // Initialize next generation networks
        List<NeuralNetwork> nextGenerationNns = new List<NeuralNetwork>();

        // 1. Preserve Top Agents
        int survivorsCount = Mathf.Min(preservedTopAgents, droneCount);
        for (int i = 0; i < survivorsCount; i++)
        {
            nextGenerationNns.Add(rankedDrones[i].behaviour.nn); // Preserve without mutation
        }

        int dronesLeft = droneCount - survivorsCount;
        if (dronesLeft > 0)
        {
            // 3. Include Preserved Agents in Reproduction Pool
            List<(DroneBehaviour behaviour, float score)> reproductionPool = rankedDrones;

            // Calculate offspring and mutant counts based on mutantRatio
            int mutantCount = Mathf.FloorToInt(mutantRatio * dronesLeft);
            int offspringCount = dronesLeft - mutantCount;

            // Determine how to split offspring among top quarters
            int quarterCount = offspringCount / 2; // Split offspring among first and second quarter
            if (quarterCount == 0 && offspringCount > 0)
                quarterCount = 1;

            // 2.1 First Quarter Reproduces Twice (Generates Two Offspring Each)
            for (int i = 0; i < quarterCount && nextGenerationNns.Count < droneCount; i++)
            {
                var nn = reproductionPool[i].behaviour.nn;
                var nn2 = reproductionPool[i + 1].behaviour.nn;
                var crossover = NeuralNetwork.GenerateCrossover(nn, nn2);
                nextGenerationNns.Add(nn.CreateMutation(connectionMutationRate, perceptronMutationRate, 8)); // First offspring
                if (nextGenerationNns.Count < droneCount)
                    nextGenerationNns.Add(crossover.CreateMutation(connectionMutationRate, perceptronMutationRate, 8)); // Second offspring
            }

            // 2.2 Second Quarter Reproduces Once (Generates One Offspring Each)
            for (int i = quarterCount; i < quarterCount * 2 && nextGenerationNns.Count < droneCount; i++)
            {
                var nn = reproductionPool[i].behaviour.nn;
                if (reproductionPool.Count <= i + 1)
                {
                    nextGenerationNns.Add(nn.CreateMutation(connectionMutationRate, perceptronMutationRate, 8)); // One offspring
                    continue;
                }
                var nn2 = reproductionPool[i + 1].behaviour.nn;
                nextGenerationNns.Add(NeuralNetwork.GenerateCrossover(nn, nn2).CreateMutation(connectionMutationRate, perceptronMutationRate, 8)); // One offspring
            }

            // 4. Fill Remaining Slots with Mutants (Option A: New Random Networks)
            while (nextGenerationNns.Count < droneCount)
            {
                NeuralNetwork mutant = new NeuralNetwork("DroneNN", NeuralNetwork.NetworkType.Other, bestNetwork.Inputs, bestNetwork.Outputs);
                mutant.Initialize(minHiddenNodes: 8, maxHiddenNodes: 16);
                nextGenerationNns.Add(mutant);
            }
        }
        else
        {
            // Edge Case: If all drones are preserved, fill with mutants
            while (nextGenerationNns.Count < droneCount)
            {
                NeuralNetwork mutant = new NeuralNetwork("DroneNN", NeuralNetwork.NetworkType.Other, bestNetwork.Inputs, bestNetwork.Outputs);
                mutant.Initialize(minHiddenNodes: 8, maxHiddenNodes: 16);
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
            Vector3 spawnPosition = GetRandomSpawnPosition(currentBoxSize);
            GameObject drone = Instantiate(droneObj, spawnPosition, Quaternion.identity);
            DroneBehaviour behaviour = drone.GetComponent<DroneBehaviour>();
            behaviour.nn = nextGenerationNns[i];
            behaviour.initializeNetwork = false; // Assuming the drone behavior checks this flag
            _behaviours[i] = behaviour;
        }

        // Update generation count in the Visualizer
        _visualizer.generation.text = "Generation: " + (++generation).ToString();
    }

    /// <summary>
    /// Generates a random spawn position within the current box size.
    /// The box size increases linearly from initialBoxSize to finalBoxSize over generations.
    /// </summary>
    /// <param name="currentBoxSize">The current size of the spawn box.</param>
    /// <returns>A Vector3 representing the spawn position.</returns>
    Vector3 GetRandomSpawnPosition(float currentBoxSize)
    {
        float x = Random.Range(-currentBoxSize, currentBoxSize);
        float y = Random.Range(-currentBoxSize, currentBoxSize);
        float z = 0f; // Assuming drones spawn at z = 0
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Initializes the log file by creating it anew, overwriting any existing file, and writing a header.
    /// </summary>
    void InitializeLogFile()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, false)) // 'false' to overwrite
            {
                writer.WriteLine("Drone Training Log");
                writer.WriteLine("Format: Generation n | Average score: x | Best score: x");
            }
            Debug.Log("Log file initialized at: " + logFilePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to initialize log file: " + ex.Message);
        }
    }

    /// <summary>
    /// Logs the average and best scores of the current generation to the log file.
    /// </summary>
    /// <param name="generationNumber">Current generation number.</param>
    /// <param name="averageScore">Average score of the generation.</param>
    /// <param name="bestScore">Best score of the generation.</param>
    void LogGenerationScores(int generationNumber, float averageScore, float bestScore)
    {
        string logEntry = $"Generation {generationNumber} | Average score: {averageScore:F2} | Best score: {bestScore:F2}";

        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, true)) // 'true' to append
            {
                writer.WriteLine(logEntry);
            }
            Debug.Log("Logged: " + logEntry);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to write to log file: " + ex.Message);
        }
    }

    void SaveBestNetwork(NeuralNetwork bestNetwork)
    {
        if (bestNetwork == null)
        {
            Debug.LogWarning("No best network found for this episode.");
            return;
        }

        string folderPath = "Assets/SavedNetworks";
        string fileName = "BestNetwork.asset";
        string assetPath = Path.Combine(folderPath, fileName);

#if UNITY_EDITOR
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            UnityEditor.AssetDatabase.CreateFolder("Assets", "SavedNetworks");
        }

        bestNetwork.SaveNetwork(assetPath);
        Debug.Log($"Best network saved at {assetPath}");
#else
        // Handle saving in builds if necessary
        Debug.LogWarning("Saving networks is only implemented for the Unity Editor.");
#endif
    }
}

// --- Code by ChatGPT (GPT-4) ---
