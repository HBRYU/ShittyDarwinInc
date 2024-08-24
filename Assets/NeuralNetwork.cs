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
        public float Bias { get; }

        public float value;

        public Perceptron(float bias)
        {
            const int weightCapacity = 4;
            Weights = new Dictionary<Perceptron, float>(weightCapacity);
            Bias = bias;
            value = 0f;
        }

        public void AddConnection(Perceptron perceptron, float weight)
        {
            Weights[perceptron] = weight;
        }

        public void Input(float inputValue)
        {
            value += inputValue;
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
        Debug.Log("Initializing model");
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
            perceptrons[i].Input(inputArray[i]);
            
            foreach (var key in perceptrons[i].Weights.Keys)
            {
                key.Input(InputActivation(perceptrons[i].value));
                computeQueue.Enqueue(key);
            }
        }

        while (computeQueue.Count > 0)
        {
            var head = computeQueue.Dequeue();
            foreach (var key in head.Weights.Keys)
            {
                key.Input(Activation(head.value + head.Bias) * head.Weights[key]);
                computeQueue.Enqueue(key);
            }
        }

        float[] outputArray = new float[Outputs];
        for (int i = 0; i < Outputs; i++)
        {
            outputArray[i] = OutputActivation(perceptrons[perceptrons.Count - Outputs + i].value);
        }

        return outputArray;

        void ClearValues()
        {
            foreach (var perceptron in perceptrons)
            {
                perceptron.value = 0f;
            }
        }
    }

    public void SaveNetwork(string directory = "")
    {
        // Add the save functionality here
    }
}
