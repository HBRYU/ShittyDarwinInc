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
    
    //public readonly Func<float, float> Activation = (x) => Mathf.Max(0f, x);  // ReLU
    public readonly Func<float, float> Activation = (x) => 1.0f / (1.0f + (float)Math.Exp(-x));  // Sigmoid
    public readonly Func<float, float> InputActivation = (x) => 1.0f / (1.0f + (float)Math.Exp(-x));  // Sigmoid
    public readonly Func<float, float> OutputActivation = (x) => 1.0f / (1.0f + (float)Math.Exp(-x));  // Sigmoid

    private static float connectionMutationChance = 0.05f;
    private static float perceptronMutationChance = 0.1f;
    
    public int WeightCost { get; private set; }

    public List<Perceptron> perceptrons = new List<Perceptron>();
    private List<Perceptron> computeList;
    float[] outputArray;
    
    
    public NeuralNetwork(string name, NetworkType type, int inputs, int outputs)
    {
        Name = name;
        Type = type;
        Inputs = inputs;
        Outputs = outputs;
        computeList = new List<Perceptron>(Inputs);
        outputArray = new float[Outputs];
    }

    public void Initialize(int minHiddenNodes = 2, int maxHiddenNodes = 6)
    {
        perceptrons = new List<Perceptron>();
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
        SetWeightCost();
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
        
        // Add all input perceptrons
        for (int i = 0; i < Inputs; i++)
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

    void SetWeightCost()
    {
        WeightCost = 0;
        foreach (var perceptron in perceptrons)
        {
            WeightCost += perceptron.Weights.Count;
        }
    }


    public float[] Compute(float[] inputArray)
    {
        ClearValues();
        for (int i = 0; i < Inputs; i++)
        {
            perceptrons[i].Input(inputArray[i]);
        
            foreach (var key in perceptrons[i].Weights.Keys)
            {
                key.Input(InputActivation(perceptrons[i].Value));
                computeList.Add(key);
            }
        }

        while (computeList.Count > 0)
        {
            var head = computeList[0];
            computeList.RemoveAt(0);
            foreach (var key in head.Weights.Keys)
            {
                key.Input(Activation(head.Value + head.Bias) * head.Weights[key]);
                if (!computeList.Contains(key))
                {
                    computeList.Add(key);
                }
            }
        }

        
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
                float weightMutationChance = connectionMutationChance;
                if (Random.value < weightMutationChance)
                {
                    perceptron.Weights[key] += PolynomialRandom();
                }

                // #2. Sever connection mutation
                float severConnectionChance = connectionMutationChance;
                if (Random.value < severConnectionChance && perceptron.Weights.Count > 1)
                {
                    perceptron.Weights.Remove(key);
                }
            }

            // #3. Bias mutation
            float biasMutationChance = perceptronMutationChance;
            if (Random.value < biasMutationChance)
            {
                perceptron.Bias += PolynomialRandom();
            }

            // #4. Add connection mutation
            float addConnectionChance = perceptronMutationChance;
            if (Random.value < addConnectionChance)
            {
                var thisIndex = mutation.perceptrons.IndexOf(perceptron);
                var potentialConnections = mutation.perceptrons.GetRange(thisIndex + 1, mutation.perceptrons.Count - thisIndex - 1).ToList();

                
                if (potentialConnections.Count > 0)
                {
                    var newConnection = potentialConnections[Random.Range(0, potentialConnections.Count)];
                    perceptron.AddConnection(newConnection, PolynomialRandom() * 2f);
                }
            }
        }

        // #5. Add new hidden perceptron (+ connections) mutation
        float addPerceptronChance = perceptronMutationChance;
        if (Random.value < addPerceptronChance)
        {
            AddNewHiddenPerceptron(mutation);
        }

        // #6. Destroy hidden perceptron mutation
        float destroyPerceptronChance = perceptronMutationChance;
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
        network.perceptrons.Insert(Random.Range(Inputs, network.perceptrons.Count - Outputs), newPerceptron);  // Insert before output perceptrons
        var newIndex = network.perceptrons.IndexOf(newPerceptron);
        
        // Add random connections from existing perceptrons to the new one
        foreach (var perceptron in network.perceptrons.GetRange(0, newIndex))
        {
            if (Random.value < perceptronMutationChance && perceptron != newPerceptron)
            {
                perceptron.AddConnection(newPerceptron, PolynomialRandom() * 2f);
            }
        }

        // Add random connections from the new perceptron to existing perceptrons
        // In AddNewHiddenPerceptron method
        foreach (var perceptron in network.perceptrons.GetRange(newIndex + 1, network.perceptrons.Count - newIndex - 1))
        {
            if (Random.value < perceptronMutationChance && perceptron != newPerceptron)
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

    public void SaveNetwork(string assetPath)  // Praise Sam Altman
    {
        NeuralNetworkData networkData = ScriptableObject.CreateInstance<NeuralNetworkData>();
        networkData.Name = Name;
        networkData.Type = Type;
        networkData.Inputs = Inputs;
        networkData.Outputs = Outputs;
        networkData.Perceptrons = new List<NeuralNetworkData.PerceptronData>();

        // Map perceptrons to indices for saving connections
        // This reduces computation! Take O(n) before looping over perceptrons
        Dictionary<Perceptron, int> perceptronToIndex = new Dictionary<Perceptron, int>();
        for (int i = 0; i < perceptrons.Count; i++)
        {
            perceptronToIndex[perceptrons[i]] = i;
        }

        // Save perceptron data
        foreach (var perceptron in perceptrons)
        {
            NeuralNetworkData.PerceptronData perceptronData = new NeuralNetworkData.PerceptronData();
            perceptronData.Bias = perceptron.Bias;
            perceptronData.ConnectedPerceptronIndices = new List<int>();
            perceptronData.Weights = new List<float>();

            foreach (var connection in perceptron.Weights)
            {
                perceptronData.ConnectedPerceptronIndices.Add(perceptronToIndex[connection.Key]);
                perceptronData.Weights.Add(connection.Value);
            }

            networkData.Perceptrons.Add(perceptronData);
        }

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.CreateAsset(networkData, assetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        UnityEngine.Debug.Log("Network saved to " + assetPath);
#else
    // Handle saving in builds if necessary
    UnityEngine.Debug.LogWarning("SaveNetwork is only implemented for the Unity Editor.");
#endif
    }
    
    public static NeuralNetwork LoadNetwork(NeuralNetworkData networkData)
    {
        NeuralNetwork network = new NeuralNetwork(networkData.Name, networkData.Type, networkData.Inputs, networkData.Outputs);
        network.perceptrons = new List<Perceptron>();

        // Create perceptrons
        foreach (var perceptronData in networkData.Perceptrons)
        {
            Perceptron perceptron = new Perceptron(perceptronData.Bias);
            network.perceptrons.Add(perceptron);
        }

        // Map indices to perceptrons for establishing connections
        List<Perceptron> indexToPerceptron = network.perceptrons;

        // Reconstruct connections
        for (int i = 0; i < networkData.Perceptrons.Count; i++)
        {
            var perceptronData = networkData.Perceptrons[i];
            var perceptron = network.perceptrons[i];

            for (int j = 0; j < perceptronData.ConnectedPerceptronIndices.Count; j++)
            {
                int connectedIndex = perceptronData.ConnectedPerceptronIndices[j];
                float weight = perceptronData.Weights[j];
                perceptron.AddConnection(indexToPerceptron[connectedIndex], weight);
            }
        }

        // Set weight cost and other necessary initialization
        network.SetWeightCost();

        return network;
    }
}
