using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class NeuralNetwork : MonoBehaviour
{
    static float PolynomialRandom(int degree = 3)
    {
        float x = Random.Range(0f, 1f);
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
            int index = Random.Range(0, maxIndex + 1); // Include maxIndex
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

    public class Perceptron
    {
        public List<float> Weights;
        public float Bias;

        public Perceptron(List<float> weights, float bias)
        {
            Weights = weights;
            Bias = bias;
        }
    }

    // First index: layer depth, second index: perceptron in layer.
    public List<List<Perceptron>> Topology = new List<List<Perceptron>>(); // Initialize Topology
    public int[][][][] Connections;  // Connection from i to j

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
        Connections = new int[1][][][]; // Initialize Connections array
        Connections[0] = new int[Inputs][][];
        for (int i = 0; i < Inputs; i++)
        {
            int edges = Random.Range(0, Outputs);
            var thisPerceptron = new Perceptron(new List<float>(new float[edges]), PolynomialRandom()); // Initialize Weights correctly
            Topology[0].Add(thisPerceptron);
            Connections[0][i] = new int[edges][]; // Initialize nested array
            var connectionEnds = RandomSample0(edges, Outputs - 1);
            for (int j = 0; j < edges; j++)
            {
                Connections[0][i][j] = new[] { 1, connectionEnds[j] };
                thisPerceptron.Weights[j] = PolynomialRandom() * 2f; // [-2f, 2f]
            }
        }
    }

    public float[] Compute(float[] inputArray)
    {
        // Add the computation logic here
        return new float[Outputs];
    }

    public void SaveNetwork(string directory = "")
    {
        // Add the save functionality here
    }
}
