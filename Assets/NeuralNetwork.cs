using System;
using System.Collections.Generic;
using UnityEditor.MemoryProfiler;
using UnityEngine;

public class NeuralNetwork : MonoBehaviour
{
    public struct Perceptron
    {
        public Dictionary<Perceptron, float> Weights { get; }
        public float Bias { get; }

        public Perceptron(float bias)
        {
            Bias = bias;
        }

        public void AddConnection(Perceptron perceptron, float weight)
        {
            Weights[perceptron] = weight;
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

    // First index: layer depth, second index: perceptron in layer.
    public List<List<Perceptron>> Topology = new List<List<Perceptron>>(); // Initialize Topology

    public NeuralNetwork(string name, NetworkType type, int inputs, int outputs)
    {
        Name = name;
        Type = type;
        Inputs = inputs;
        Outputs = outputs;
    }

    public void Initialize()
    {
        Topology.Add(new List<Perceptron>(Inputs));
        Topology.Add(new List<Perceptron>(Outputs));
        for (int i = 0; i < Inputs; i++)
        {
            int numEdges = UnityEngine.Random.Range(0, Outputs);
            var connectionEnds = RandomSample0(numEdges, Outputs - 1);
            var thisPerceptron = new Perceptron(PolynomialRandom());
            Topology[0].Add(thisPerceptron);
            for (int j = 0; j < numEdges; j++)
            {
                thisPerceptron.AddConnection(Topology[1][connectionEnds[j]], PolynomialRandom() * 2f); // [-2f, 2f]
            }
        }
    }

    public float[] Compute(float[] inputArray)
    {
        if (inputArray.Length != Inputs)
            throw new ArgumentException("Input array length must match the number of inputs");
        float[] outputArray = new float[Outputs];

        // Initialize node values dictionary
        Dictionary<int, float> nodeValues = new Dictionary<int, float>();

        // Initialize input nodes
        for (int i = 0; i < Inputs; i++)
        {
            nodeValues[i] = inputArray[i];
        }

        // Process the network
        for (int layer = 0; layer < Topology.Count; layer++)
        {
            for (int nodeIndex = 0; nodeIndex < Topology[layer].Count; nodeIndex++)
            {
                int currentNode = GetGlobalNodeIndex(layer, nodeIndex);
                float bias = Topology[layer][nodeIndex].Bias;
                
                // Process current node first before passing signal (except input layer)
                if(layer > 0)
                    nodeValues[currentNode] = Activation(nodeValues[currentNode] + bias);
                if (layer == Topology.Count)
                    break;
                
                var currentConnections = Connections[layer][nodeIndex];
                var currentWeights = Topology[layer][nodeIndex].Weights;
                for (var i = 0; i < currentConnections.Length; i++)
                {
                    var connection = currentConnections[i];
                    if (!nodeValues.ContainsKey(GetGlobalNodeIndex(connection[0], connection[1])))
                    {
                        nodeValues[GetGlobalNodeIndex(connection[0], connection[1])] = 0f;
                    }
                    nodeValues[GetGlobalNodeIndex(connection[0], connection[1])] +=
                        nodeValues[currentNode] * currentWeights[i];
                }
                
            }
        }

        // Extract output values
        for (int i = 0; i < Outputs; i++)
        {
            int outputNodeIndex = GetGlobalNodeIndex(Topology.Count - 1, i);
            outputArray[i] = nodeValues[outputNodeIndex];
        }

        return outputArray;
    }

    // Helper method to get the global index of a node given its layer and local index
    private int GetGlobalNodeIndex(int layer, int localIndex)
    {
        int globalIndex = 0;
        for (int i = 0; i < layer; i++)
        {
            globalIndex += Topology[i].Count;
        }
        globalIndex += localIndex;
        return globalIndex;
    }


    public void SaveNetwork(string directory = "")
    {
        // Add the save functionality here
    }
}
