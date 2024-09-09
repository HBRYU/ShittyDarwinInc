using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class NeuralNetworkTester : MonoBehaviour
{
    private NeuralNetwork neuralNetwork;

    void Start()
    {
        // Create a simple neural network
        string networkName = "TestNetwork";
        NeuralNetwork.NetworkType networkType = NeuralNetwork.NetworkType.FullyConnected;
        int inputNodes = 2;
        int outputNodes = 4;

        neuralNetwork = new NeuralNetwork(networkName, networkType, inputNodes, outputNodes);

        // Initialize the network
        neuralNetwork.Initialize();
    }

    private void FixedUpdate()
    {
        GetComponent<Rigidbody2D>().MovePosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
        // Test the network with some sample input
        float[] sampleInput = { transform.position.x, transform.position.y };
        float[] output = neuralNetwork.Compute(sampleInput);

        // Print the output
        Debug.Log("Network Output:");
        for (int i = 0; i < output.Length; i++)
        {
            Debug.Log($"Output {i + 1}: {output[i]}");
        }
    }
}
