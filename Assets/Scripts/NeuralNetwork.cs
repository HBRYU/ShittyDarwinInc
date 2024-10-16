using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class NeuralNetwork
{
    public class Perceptron
    {
        public Dictionary<Perceptron, float> Weights { get; }
        public float Bias;
        public float Value;
        public int Label = 0;  // 0 is set as default =~= no label

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
        float x = Random.Range(0f, 1f);
        return Mathf.Pow(x - 0.5f, degree) * Mathf.Pow(2f, degree);
    }

    static List<int> RandomSample0(int sampleCount, int maxInclusive)
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
            int index = Random.Range(0, maxIndex + 1);
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

    // Activation functions
    public readonly Func<float, float> Activation = x => 1.0f / (1.0f + (float)Math.Exp(-x)); // Sigmoid
    public readonly Func<float, float> InputActivation = x => 1.0f / (1.0f + (float)Math.Exp(-x)); // Sigmoid
    public readonly Func<float, float> OutputActivation = x => 1.0f / (1.0f + (float)Math.Exp(-x)); // Sigmoid

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
        int nodeCount = Random.Range(minHiddenNodes, maxHiddenNodes + 1) + Inputs + Outputs;
        for (int i = 0; i < nodeCount; i++)
        {
            perceptrons.Add(new Perceptron(PolynomialRandom()));
        }

        // Input layer connections
        for (int i = 0; i < Inputs; i++)
        {
            perceptrons[i].Label = i + 1;
            List<int> forwardConnectionIndices = RandomSample0(Random.Range(1, nodeCount - Inputs), nodeCount - Inputs - 1);
            for (int j = 0; j < forwardConnectionIndices.Count; j++)
            {
                forwardConnectionIndices[j] += Inputs;
                perceptrons[i].AddConnection(perceptrons[forwardConnectionIndices[j]], PolynomialRandom() * 2f);
            }
        }

        // Hidden layers connections
        for (int i = Inputs; i < nodeCount - Outputs; i++)
        {
            perceptrons[i].Label = Random.Range(Inputs + 1, 999);  // Assign random int labels for new perceptrons -> direct offsprings should inherit
            List<int> forwardConnectionIndices = RandomSample0(Random.Range(1, nodeCount - i - 1), nodeCount - i - 1);
            for (int j = 0; j < forwardConnectionIndices.Count; j++)
            {
                forwardConnectionIndices[j] += i;
                if (forwardConnectionIndices[j] == i) continue;
                perceptrons[i].AddConnection(perceptrons[forwardConnectionIndices[j]], PolynomialRandom() * 2f);
            }
        }
        
        // Set sub 0 labels for output layer
        for (int i = nodeCount - Outputs, tempLabel = -1; i < nodeCount; i++)
        {
            perceptrons[i].Label = tempLabel--;
        }

        PruneDeadEndPerceptrons();
        SetWeightCost();
    }

    private void PruneDeadEndPerceptrons()
    {
        HashSet<Perceptron> activePerceptrons = new HashSet<Perceptron>();

        // Add output perceptrons
        for (int i = perceptrons.Count - Outputs; i < perceptrons.Count; i++)
        {
            activePerceptrons.Add(perceptrons[i]);
        }

        // Add input perceptrons
        for (int i = 0; i < Inputs; i++)
        {
            activePerceptrons.Add(perceptrons[i]);
        }

        // Propagate backwards
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

        // Remove inactive perceptrons
        perceptrons = perceptrons.Where(p => activePerceptrons.Contains(p)).ToList();

        // Clean up weights
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

    public void Compute(float[] inputArray, ref float[] refOutputArray)
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
            refOutputArray[i] = OutputActivation(perceptrons[perceptrons.Count - Outputs + i].Value);
        }

        void ClearValues()
        {
            foreach (var perceptron in perceptrons)
            {
                perceptron.Value = 0f;
            }
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

    public NeuralNetwork CreateMutation(float connectionMutationChance=0.05f, float perceptronMutationChance=0.1f, int minHiddenNodes=0)
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
            perceptronCopy.Label = perceptron.Label;
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
            foreach (var key in perceptron.Weights.Keys.ToList())
            {
                // #1. Weight mutation
                if (Random.value < connectionMutationChance)
                {
                    perceptron.Weights[key] += PolynomialRandom();
                }

                // #2. Sever connection mutation
                if (Random.value < connectionMutationChance && perceptron.Weights.Count > 1)
                {
                    perceptron.Weights.Remove(key);
                }
            }

            // #3. Bias mutation
            if (Random.value < perceptronMutationChance)
            {
                perceptron.Bias += PolynomialRandom();
            }

            // #4. Add connection mutation
            if (Random.value < perceptronMutationChance)
            {
                var thisIndex = mutation.perceptrons.IndexOf(perceptron);
                var potentialConnections = mutation.perceptrons.GetRange(thisIndex + 1, mutation.perceptrons.Count - thisIndex - 1);

                if (potentialConnections.Count > 0)
                {
                    var newConnection = potentialConnections[Random.Range(0, potentialConnections.Count)];
                    perceptron.AddConnection(newConnection, PolynomialRandom() * 2f);
                }
            }
        }

        // #5. Add new hidden perceptron mutation
        if (Random.value < perceptronMutationChance)
        {
            AddNewHiddenPerceptron(mutation, perceptronMutationChance);
        }

        // #6. Destroy hidden perceptron mutation (set min hidden nodes to force network scale)
        if (Random.value < perceptronMutationChance && mutation.perceptrons.Count > minHiddenNodes + Inputs + Outputs)
        {
            DestroyRandomHiddenPerceptron(mutation);
        }

        // Prune dead-end perceptrons after mutations
        mutation.PruneDeadEndPerceptrons();

        return mutation;
    }

    private void AddNewHiddenPerceptron(NeuralNetwork network, float perceptronMutationChance)
    {
        var newPerceptron = new Perceptron(PolynomialRandom())
        {
            Label = Random.Range(Inputs + 1, 999)
        };
        network.perceptrons.Insert(Random.Range(Inputs, network.perceptrons.Count - Outputs + 1), newPerceptron);
        var newIndex = network.perceptrons.IndexOf(newPerceptron);

        // Add random connections from existing perceptrons to the new one
        int newInConnectionCount = Random.Range(1, newIndex);
        var newInConnections = RandomSample0(newInConnectionCount, newIndex-1);
        foreach (var i in newInConnections)
        {
            network.perceptrons[i].AddConnection(newPerceptron, PolynomialRandom() * 2f);
        }

        // Add random connections from the new perceptron to existing perceptrons
        int newOutConnectionCount = Random.Range(1, network.perceptrons.Count - newIndex - 1);
        var newOutConnections = RandomSample0(newOutConnectionCount, network.perceptrons.Count - newIndex - 2);
        foreach (var i in newOutConnections)
        {
            newPerceptron.AddConnection(network.perceptrons[i+newIndex+1], PolynomialRandom() * 2f);
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
        int hiddenEndIndex = network.perceptrons.Count - Outputs - 1;
        int perceptronToRemoveIndex = Random.Range(hiddenStartIndex, hiddenEndIndex + 1);

        var perceptronToRemove = network.perceptrons[perceptronToRemoveIndex];
        network.perceptrons.RemoveAt(perceptronToRemoveIndex);

        // Remove all connections to this perceptron
        foreach (var perceptron in network.perceptrons)
        {
            if (perceptron.Weights.ContainsKey(perceptronToRemove))
            {
                perceptron.Weights.Remove(perceptronToRemove);
            }
        }
    }

    public void SaveNetwork(string assetPath)
    {
        NeuralNetworkData networkData = ScriptableObject.CreateInstance<NeuralNetworkData>();
        networkData.Name = Name;
        networkData.Type = Type;
        networkData.Inputs = Inputs;
        networkData.Outputs = Outputs;
        networkData.Perceptrons = new List<NeuralNetworkData.PerceptronData>();

        // Map perceptrons to indices for saving connections
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

    /// <summary>
    /// For genetic crossover of two networks
    /// </summary>
    /// <param name="network"></param>
    /// <returns></returns>
    public static (int, float)[][] GetGene(NeuralNetwork network)
    {
        (int, float)[][] gene = new  (int, float)[network.perceptrons.Count][];

        for (int i = 0; i < network.perceptrons.Count; i++)
        {
            var perceptron = network.perceptrons[i];
            gene[i] = new (int, float)[perceptron.Weights.Count];
            int j = 0;
            foreach (var connection in perceptron.Weights)
            {
                gene[i][j] = (network.perceptrons.IndexOf(connection.Key), connection.Value);
                j++;
            }
        }

        return gene;
    }

    public static NeuralNetwork GenerateCrossover(NeuralNetwork network1, NeuralNetwork network2)
    {
        var gene1 = GetGene(network1);
        var gene2 = GetGene(network2);

        // Flip a coin
        var coin = Random.value < 0.5f;  // true -> use gene1 for output
        
        var baseGene = coin ? gene1 : gene2;
        var subGene = coin ? gene2 : gene1;
        
        NeuralNetwork baseNetwork = coin ? network1 : network2;
        NeuralNetwork subNetwork = coin ? network2 : network1;

        bool baseGeneFlag = Random.value < 0.5f;  // Reference base gene if true
        float switchProbability = 1.5f / (subGene.Length - baseNetwork.Outputs);  // Probability of switching from base gene to sub gene, for each perceptron
        var crossoverGene = new (int, float)[baseGene.Length][];
        var baseGeneFlags = new bool[baseGene.Length];
        
        // Loop before output layers of sub gene
        for (int i = 0; i < Mathf.Min(subGene.Length, baseGene.Length) - baseNetwork.Outputs; i++)
        {
            if (Random.value < switchProbability)
                baseGeneFlag = !baseGeneFlag;

            crossoverGene[i] = baseGeneFlag ? baseGene[i] : subGene[i];
            baseGeneFlags[i] = baseGeneFlag;
        }

        for (int i = Mathf.Min(subGene.Length, baseGene.Length) - baseNetwork.Outputs; i < baseGene.Length; i++)
        {
            crossoverGene[i] = baseGene[i];
            baseGeneFlags[i] = true;
        }
        
        // Build network from gene
        NeuralNetwork crossoverNetwork =
            new NeuralNetwork(baseNetwork.Name, baseNetwork.Type, baseNetwork.Inputs, baseNetwork.Outputs);
        
        // Debug.Log(crossoverGene.Length);
        
        for (int i = 0; i < crossoverGene.Length; i++)
        {
            NeuralNetwork referenceNetwork = baseGeneFlags[i] ? baseNetwork : subNetwork;
            Perceptron newPerceptron = new Perceptron(referenceNetwork.perceptrons[i].Bias);
            newPerceptron.Label = referenceNetwork.perceptrons[i].Label;
            crossoverNetwork.perceptrons.Add(newPerceptron);
        }

        for (int i = 0; i < crossoverGene.Length; i++)
        {
            NeuralNetwork referenceNetwork = baseGeneFlags[i] ? baseNetwork : subNetwork;
            foreach ((int, float) connection in crossoverGene[i])
            {

                bool labelFound = false;
                Perceptron connectPerceptron = crossoverNetwork.perceptrons[0];
                int refLabel = referenceNetwork.perceptrons[connection.Item1].Label;
                foreach (var perceptron in crossoverNetwork.perceptrons)
                {
                    if (perceptron.Label == refLabel)
                    {
                        // Found perceptron in crossover with matching label
                        labelFound = true;
                        connectPerceptron = perceptron;
                        break;
                    }
                }
                
                // Case 1: no label exists in new network
                if (!labelFound && connection.Item1 < crossoverNetwork.perceptrons.Count)
                    crossoverNetwork.perceptrons[i].AddConnection(crossoverNetwork.perceptrons[connection.Item1], connection.Item2);
                // Case 2: matching label found
                else if (crossoverNetwork.perceptrons.IndexOf(connectPerceptron) > i)
                {
                    // Debug.Log("Label found");
                    crossoverNetwork.perceptrons[i].AddConnection(connectPerceptron, connection.Item2);
                }
            }
        }
        
        crossoverNetwork.PruneDeadEndPerceptrons();

        return crossoverNetwork;
    }
}

