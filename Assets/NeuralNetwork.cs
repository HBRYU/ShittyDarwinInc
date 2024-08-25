using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.MemoryProfiler;
using UnityEngine;
using Random = UnityEngine.Random;

public class NeuralNetwork
{
    public class Perceptron
    {
        public Dictionary<Perceptron, float> Weights { get; }
        public float Bias;

        public float Value;

        public Perceptron(float bias)
        {
            const int weightCapacity = 4;
            Weights = new Dictionary<Perceptron, float>(weightCapacity);
            Bias = bias;
            Value = 0f;
        }

        public void AddConnection(Perceptron perceptron, float weight)
        {
            Weights[perceptron] = weight;
        }

        public void Input(float inputValue)
        {
            Value += inputValue;
        }
    }
    
    static float PolynomialRandom(int degree = 3)
    {
        float x = UnityEngine.Random.Range(0f, 1f);
        return Mathf.Pow(x - 0.5f, degree) * Mathf.Pow(2f, degree);
    }

    static List<int> RandomSample0(int sampleCount, int maxInclusive) // O(nlogn)
    {
        List<int> population = new List<int>(maxInclusive + 1);
        for (int i = 0; i <= maxInclusive; i++)
        {
            population.Add(i);
        }
        List<int> sample = new List<int>(sampleCount);
        int maxIndex = maxInclusive;
        for (int i = 0; i < sampleCount; i++)
        {
            int index = UnityEngine.Random.Range(0, maxIndex + 1); // Include maxIndex
            sample.Add(population[index]);
            population.RemoveAt(index);
            maxIndex--;
        }
        sample.Sort();
        return sample;
    }

    public readonly string Name;
    public enum NetworkType
    {
        FullyConnected,
        Neat,
        Recursive,
        Other,
    }
    public readonly NetworkType Type;
    public readonly int Inputs, Outputs;
    public readonly Func<float, float> Activation = (x) => Mathf.Max(0f, x);  // ReLU
    public readonly Func<float, float> InputActivation = (x) => 1.0f / (1.0f + (float)Math.Exp(-x));  // Sigmoid
    public readonly Func<float, float> OutputActivation = (x) => 1.0f / (1.0f + (float)Math.Exp(-x));  // Sigmoid
    
    public List<Perceptron> perceptrons = new List<Perceptron>();

    public NeuralNetwork(string name, NetworkType type, int inputs, int outputs)
    {
        Name = name;
        Type = type;
        Inputs = inputs;
        Outputs = outputs;
    }

    public void Initialize(int minHiddenNodes = 2, int maxHiddenNodes = 6)
    {
        // Debug.Log("Initializing model");
        int nodeCount = Random.Range(minHiddenNodes, maxHiddenNodes) + Inputs + Outputs;
        for (int i = 0; i < nodeCount; i++)
        {
            perceptrons.Add(new Perceptron(PolynomialRandom()));
        }

        
        // Input layer
        for (int i = 0; i < Inputs; i++)
        {
            List<int> forwardConnectionIndices = RandomSample0(Random.Range(0, nodeCount - Inputs), nodeCount - Inputs - 1);
            for (int j = 0; j < forwardConnectionIndices.Count; j++)
            {
                forwardConnectionIndices[j] += Inputs;
                perceptrons[i].AddConnection(perceptrons[forwardConnectionIndices[j]], PolynomialRandom()*2f);
            }
        }
        
        // Hidden layers
        for (int i = Inputs; i < nodeCount - Outputs; i++)
        {
            List<int> forwardConnectionIndices = RandomSample0(Random.Range(1, nodeCount - i - 1), nodeCount - i - 1);
            for (int j = 0; j < forwardConnectionIndices.Count; j++)
            {
                forwardConnectionIndices[j] += i;
                if(forwardConnectionIndices[j]==i) continue;
                perceptrons[i].AddConnection(perceptrons[forwardConnectionIndices[j]], PolynomialRandom()*2f);
            }
        }

        PruneDeadEndPerceptrons();
    }
    
    
    private void PruneDeadEndPerceptrons()  // by GPT-4o
    {
        // Step 1: Identify active perceptrons that lead to outputs
        HashSet<Perceptron> activePerceptrons = new HashSet<Perceptron>();

        // Start by adding all output perceptrons
        for (int i = perceptrons.Count - Outputs; i < perceptrons.Count; i++)
        {
            activePerceptrons.Add(perceptrons[i]);
        }

        // Propagate backwards to find all perceptrons that lead to outputs
        bool foundNewActive;
        do
        {
            foundNewActive = false;
            foreach (var perceptron in perceptrons)
            {
                if (activePerceptrons.Contains(perceptron))
                {
                    continue;
                }

                foreach (var connectedPerceptron in perceptron.Weights.Keys)
                {
                    if (activePerceptrons.Contains(connectedPerceptron))
                    {
                        activePerceptrons.Add(perceptron);
                        foundNewActive = true;
                        break;
                    }
                }
            }
        } while (foundNewActive);

        // Step 2: Remove dead-end perceptrons
        perceptrons = perceptrons.Where(p => activePerceptrons.Contains(p)).ToList();

        // Optionally: Clean up weights in active perceptrons to remove connections to pruned perceptrons
        foreach (var perceptron in perceptrons)
        {
            List<Perceptron> keysToRemove = perceptron.Weights.Keys.Where(k => !activePerceptrons.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                perceptron.Weights.Remove(key);
            }
        }
    }


    public float[] Compute(float[] inputArray)
    {
        ClearValues();
        
        Queue<Perceptron> computeQueue = new Queue<Perceptron>(Inputs);
        for (int i = 0; i < Inputs; i++)
        {
            try
            {
                perceptrons[i].Input(inputArray[i]);
            }
            catch (IndexOutOfRangeException e)
            {
                Debug.LogError(e);
                Debug.LogWarning(perceptrons.Count);
                Debug.LogWarning(inputArray.Length);
                Debug.LogWarning(i);
            }
            
            foreach (var key in perceptrons[i].Weights.Keys)
            {
                key.Input(InputActivation(perceptrons[i].Value));
                computeQueue.Enqueue(key);
            }
        }

        while (computeQueue.Count > 0)
        {
            var head = computeQueue.Dequeue();
            foreach (var key in head.Weights.Keys)
            {
                key.Input(Activation(head.Value + head.Bias) * head.Weights[key]);
                computeQueue.Enqueue(key);
            }
        }

        float[] outputArray = new float[Outputs];
        for (int i = 0; i < Outputs; i++)
        {
            outputArray[i] = OutputActivation(perceptrons[perceptrons.Count - Outputs + i].Value);
        }

        return outputArray;

        void ClearValues()
        {
            foreach (var perceptron in perceptrons)
            {
                perceptron.Value = 0f;
            }
        }
    }

    public NeuralNetwork CreateMutation()
    {
        var mutation = new NeuralNetwork(Name, Type, Inputs, Outputs);

        // Create a mapping from original perceptrons to their copies
        Dictionary<Perceptron, Perceptron> perceptronMapping = new Dictionary<Perceptron, Perceptron>();

        // Deep copy the perceptrons
        foreach (var perceptron in perceptrons)
        {
            var perceptronCopy = new Perceptron(perceptron.Bias);
            perceptronMapping[perceptron] = perceptronCopy;
        }

        // Deep copy the weights and connections
        foreach (var perceptron in perceptrons)
        {
            var perceptronCopy = perceptronMapping[perceptron];
            foreach (var connection in perceptron.Weights)
            {
                var connectedPerceptronCopy = perceptronMapping[connection.Key];
                perceptronCopy.AddConnection(connectedPerceptronCopy, connection.Value);
            }
        }

        // Assign the deep copied perceptrons to the mutation's perceptrons list
        mutation.perceptrons = perceptronMapping.Values.ToList();

        // Apply mutations
        foreach (var perceptron in mutation.perceptrons)
        {
            foreach (var key in perceptron.Weights.Keys.ToList())  // Using ToList() to avoid modifying the collection during iteration
            {
                // #1. Weight mutation
                float weightMutationChance = 0.01f;
                if (Random.value < weightMutationChance)
                {
                    perceptron.Weights[key] += PolynomialRandom();
                }

                // #2. Sever connection mutation
                float severConnectionChance = 0.01f;
                if (Random.value < severConnectionChance && perceptron.Weights.Count > 1)
                {
                    perceptron.Weights.Remove(key);
                }
            }

            // #3. Bias mutation
            float biasMutationChance = 0.01f;
            if (Random.value < biasMutationChance)
            {
                perceptron.Bias += PolynomialRandom();
            }

            // #4. Add connection mutation
            float addConnectionChance = 0.01f;
            if (Random.value < addConnectionChance)
            {
                var potentialConnections = mutation.perceptrons.Except(perceptron.Weights.Keys).Except(new[] { perceptron }).ToList();
                if (potentialConnections.Count > 0)
                {
                    var newConnection = potentialConnections[Random.Range(0, potentialConnections.Count)];
                    perceptron.AddConnection(newConnection, PolynomialRandom() * 2f);
                }
            }
        }

        // #5. Add new hidden perceptron (+ connections) mutation
        float addPerceptronChance = 0.005f;
        if (Random.value < addPerceptronChance)
        {
            AddNewHiddenPerceptron(mutation);
        }

        // #6. Destroy hidden perceptron mutation
        float destroyPerceptronChance = 0.005f;
        if (Random.value < destroyPerceptronChance)
        {
            DestroyRandomHiddenPerceptron(mutation);
        }

        // Prune dead-end perceptrons after mutations
        mutation.PruneDeadEndPerceptrons();

        return mutation;
    }

    private void AddNewHiddenPerceptron(NeuralNetwork network)
    {
        var newPerceptron = new Perceptron(PolynomialRandom());
        network.perceptrons.Insert(network.perceptrons.Count - Outputs, newPerceptron);  // Insert before output perceptrons

        // Add random connections from existing perceptrons to the new one
        foreach (var perceptron in network.perceptrons)
        {
            if (Random.value < 0.5f && perceptron != newPerceptron)
            {
                perceptron.AddConnection(newPerceptron, PolynomialRandom() * 2f);
            }
        }

        // Add random connections from the new perceptron to existing perceptrons
        foreach (var perceptron in network.perceptrons)
        {
            if (Random.value < 0.5f && perceptron != newPerceptron)
            {
                newPerceptron.AddConnection(perceptron, PolynomialRandom() * 2f);
            }
        }
    }

    private void DestroyRandomHiddenPerceptron(NeuralNetwork network)
    {
        if (network.perceptrons.Count <= Inputs + Outputs + 1)
        {
            // Not enough perceptrons to destroy any hidden ones
            return;
        }

        int hiddenStartIndex = Inputs;
        int hiddenEndIndex = network.perceptrons.Count - Outputs;
        int perceptronToRemoveIndex = Random.Range(hiddenStartIndex, hiddenEndIndex);

        var perceptronToRemove = network.perceptrons[perceptronToRemoveIndex];
        network.perceptrons.Remove(perceptronToRemove);

        // Remove all connections to this perceptron
        foreach (var perceptron in network.perceptrons)
        {
            if (perceptron.Weights.ContainsKey(perceptronToRemove))
            {
                perceptron.Weights.Remove(perceptronToRemove);
            }
        }
    }

    public void SaveNetwork(string directory = "")
    {
        // Add the save functionality here
    }
}
