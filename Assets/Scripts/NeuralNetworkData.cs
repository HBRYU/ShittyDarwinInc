using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NeuralNetworkData", menuName = "Neural Network/Network Data")]
public class NeuralNetworkData : ScriptableObject
{
    [System.Serializable]
    public class PerceptronData
    {
        public float Bias;
        public List<int> ConnectedPerceptronIndices;
        public List<float> Weights;
    }

    public string Name;
    public NeuralNetwork.NetworkType Type;
    public int Inputs;
    public int Outputs;
    public List<PerceptronData> Perceptrons;
}
