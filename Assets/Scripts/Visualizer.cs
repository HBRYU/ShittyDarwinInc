using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Visualizer : MonoBehaviour
{
    public GameObject sampleTarget;
    public NeuralNetwork targetNeuralNetwok;
    public Vector2 topRight;
    public float bezelRatio = 0.1f;
    public GameObject nodeObj, edgeObj;
    private List<Node> nodes = new List<Node>();

    private Dictionary<NeuralNetwork.Perceptron, Node> nodeDictionary = new Dictionary<NeuralNetwork.Perceptron, Node>();
    private List<Edge> edges = new List<Edge>();
    public RectTransform panelTransform;

    public LayerMask agentLayer;

    public bool useGenerationPanel = true;
    public TextMeshProUGUI generation;
    
    // Start is called before the first frame update
    void Start()
    {
        Setup(sampleTarget.GetComponent<DroneBehaviour>().nn);
        generation.gameObject.SetActive(useGenerationPanel);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Render();
    }

    private void Update()
    {
        HandleAgentSelection();
    }

    public void Setup(NeuralNetwork neuralNetwork)
    {
        int inputs = neuralNetwork.Inputs, outputs = neuralNetwork.Outputs;
        float nodeScale = topRight.y * 2f / inputs;
        float imageSize = nodeScale * (1f - bezelRatio);
        imageSize = imageSize > 50f ? 50f : imageSize;

        var perceptrons = neuralNetwork.perceptrons;
        List<NeuralNetwork.Perceptron> inputPerceptrons = new List<NeuralNetwork.Perceptron>(inputs);
        List<NeuralNetwork.Perceptron> outputPerceptrons = new List<NeuralNetwork.Perceptron>(outputs);

        // Input Layer
        for (int i = 0; i < inputs; i++)
        {
            inputPerceptrons.Add(perceptrons[i]);
            float y = inputs <= 1 ? 0f : topRight.y * ((inputs - i - 1f) / (inputs - 1f)) - topRight.y * (i / (inputs - 1f));
            var obj = Instantiate(this.nodeObj, panelTransform);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(imageSize, imageSize);
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-topRight.x, y);
            nodes.Add(new Node(perceptrons[i], Color.yellow, Color.grey, obj));
        }
        
        // Output Layer
        for (int i = 0; i < outputs; i++)
        {
            outputPerceptrons.Add(perceptrons[perceptrons.Count - outputs + i]);
            float y = outputs <= 1 ? 0f : topRight.y * ((outputs - i - 1f) / (outputs - 1f)) - topRight.y * (i / (outputs - 1f));
            var obj = Instantiate(this.nodeObj, panelTransform);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(imageSize, imageSize);
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(topRight.x, y);
            nodes.Add(new Node(perceptrons[perceptrons.Count - outputs + i], Color.yellow, Color.grey, obj));
        }
        
        // Hidden Layers
        float minX = -topRight.x + nodeScale;
        float maxX = -minX;

        for (int i = inputs; i < perceptrons.Count - outputs; i++)
        {
            var obj = Instantiate(this.nodeObj, panelTransform);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(imageSize, imageSize);
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(Random.Range(minX, maxX), Random.Range(-topRight.y, topRight.y));
            nodes.Add(new Node(perceptrons[i], Color.yellow, Color.grey, obj));
        }

        // Create dictionary to setup edges
        foreach (var node in nodes)
        {
            nodeDictionary.Add(node.perceptron, node);
        }
        
        // Setup Edges
        foreach (Node node in nodes)
        {
            foreach (var connection in node.perceptron.Weights)
            {
                float weight = connection.Value;
                var terminal = connection.Key;
                var obj = Instantiate(edgeObj, panelTransform);
                var edge = new Edge(node, nodeDictionary[terminal], Color.yellow, Color.grey, obj);
                edges.Add(edge);
            }
        }
    }

    public void Render()
    {
        foreach (var node in nodes)
        {
            node.Render();
        }

        foreach (var edge in edges)
        {
            edge.Render();
        }
    }

    class Node
    {
        public NeuralNetwork.Perceptron perceptron;

        private Color positiveMaxColor, negativeMaxColor;
        private GameObject obj;
        private Image image;

        private RectTransform rectTransform;

        public Node(NeuralNetwork.Perceptron perceptron, Color positiveMaxColor, Color negativeMaxColor, GameObject obj)
        {
            this.perceptron = perceptron;
            this.positiveMaxColor = positiveMaxColor;
            this.negativeMaxColor = negativeMaxColor;
            this.obj = obj;
            image = obj.GetComponent<Image>();
            rectTransform = obj.GetComponent<RectTransform>();
        }

        public void Render()
        {
            var t = perceptron.Value >= 1f ? 1f : (perceptron.Value < -1f ? 0f : ((perceptron.Value + 1f) / 2f));
            image.color = Color.Lerp(negativeMaxColor, positiveMaxColor, t);
            
            // node size
            var scale = Mathf.Abs(perceptron.Value) < 0.5f ? Vector3.one * 0.5f 
                : Mathf.Abs(perceptron.Value) <= 1f ? Vector3.one * Mathf.Abs(perceptron.Value)
                : Vector3.one;
            scale.z = 1f;
            var scaleT = 0.5f;
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, scale, scaleT);
        }

        public GameObject GetGameObject()
        {
            return obj;
        }

        public void DestroyNode()
        {
            if (obj != null)
            {
                image = null;
                Destroy(obj);
                obj = null;  // Clear the reference to the GameObject
            }
        }
    }

    class Edge
    {
        private Node origin, terminal;
        private Color positiveMaxColor, negativeMaxColor;
        private float value;

        private GameObject obj;
        private Image image;

        public Edge(Node origin, Node terminal, Color positiveMaxColor, Color negativeMaxColor, GameObject obj)
        {
            this.origin = origin;
            this.terminal = terminal;
            this.positiveMaxColor = positiveMaxColor;
            this.negativeMaxColor = negativeMaxColor;
            this.obj = obj;
            obj.SetActive(true);
            image = obj.GetComponent<Image>();
            value = 0f;

            // Set the initial position and rotation of the line
            SetPosition();
        }

        public void SetPosition()
        {
            // Get the RectTransforms of the origin and terminal nodes
            RectTransform originRect = origin.GetGameObject().GetComponent<RectTransform>();
            RectTransform terminalRect = terminal.GetGameObject().GetComponent<RectTransform>();

            // Calculate the midpoint between the origin and terminal
            Vector2 midpoint = (originRect.anchoredPosition + terminalRect.anchoredPosition) / 2f;

            // Set the position of the line to the midpoint
            RectTransform lineRect = obj.GetComponent<RectTransform>();
            lineRect.anchoredPosition = midpoint;

            // Calculate the distance between the origin and terminal
            float distance = Vector2.Distance(originRect.anchoredPosition, terminalRect.anchoredPosition);

            // Set the width of the line (its length) to match the distance between the nodes
            lineRect.sizeDelta = new Vector2(distance, lineRect.sizeDelta.y);

            // Calculate the angle of rotation based on the direction between the origin and terminal
            Vector2 direction = terminalRect.anchoredPosition - originRect.anchoredPosition;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Apply the rotation
            lineRect.rotation = Quaternion.Euler(0, 0, angle);
        }

        public void Render()
        {
            value = origin.perceptron.Value;
            var t = value >= 1f ? 1f : (value < -1f ? 0f : ((value + 1f) / 2f));
            var defaultWidth = 5f;
            var width = Mathf.Abs(value) * defaultWidth;
            width = width <= 1f ? 1f : width;
            width = width > defaultWidth ? defaultWidth : width;

            // Set the color of the line based on the value
            image.color = Color.Lerp(negativeMaxColor, positiveMaxColor, t);

            // Set the thickness of the line
            RectTransform lineRect = obj.GetComponent<RectTransform>();
            float widthT = 0.5f;
            lineRect.sizeDelta = Vector2.Lerp(lineRect.sizeDelta, new Vector2(lineRect.sizeDelta.x, width), widthT);
        }

        public void DestroyEdge()
        {
            if (obj != null)
            {
                image = null;
                Destroy(obj);
                obj = null;  // Clear the reference to the GameObject
            }
        }
    }

    void HandleAgentSelection()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var col = Physics2D.OverlapCircleAll(mousePosition, 10f, agentLayer);

        if (col.Length == 0)
            return;

        float closestDistance = 3f;
        Collider2D closestCollider = col[0];
        foreach (var collider in col)
        {
            var d = Vector2.Distance(collider.transform.position, mousePosition);
            if (d < closestDistance)
            {
                closestDistance = d;
                closestCollider = collider;
            }
        }

        // Select closest agent
        var agent = closestCollider.GetComponent<DroneBehaviour>();

        Deselect(); // Clear current visualization

        Setup(agent.nn);

        targetNeuralNetwok = agent.nn;
    }

    public void Deselect()
    {
        // Destroy all edges
        foreach (var edge in edges)
        {
            if (edge != null)
            {
                edge.DestroyEdge();
            }
        }
        edges.Clear();
    
        // Destroy all nodes
        foreach (var node in nodes)
        {
            if (node != null)
            {
                node.DestroyNode();
            }
        }
        nodes.Clear();
        nodeDictionary.Clear();

        // Destroy all child objects of the panel immediately
        for (int i = panelTransform.childCount - 1; i >= 0; i--)
        {
            if (panelTransform.GetChild(i) != null)
            {
                Destroy(panelTransform.GetChild(i).gameObject);
            }
        }
    }
}
