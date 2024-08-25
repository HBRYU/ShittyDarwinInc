using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public float health, lifespan;
    private float lifespanTimer;
    public NeuralNetwork nn;
    public int rayCount;
    public float rayAngle;
    public float rayLength;
    public LayerMask rayLayer;
    private Rigidbody2D rb;
    public float rotSpeedMultiplier, speedMultiplier;

    public int computeTick = 2;
    private int computeTickCounter = 1;

    public bool initializeNn = true;
    public GameObject agentObj;

    private Visualizer visualizer;
    
    // Start is called before the first frame update
    void Start()
    {
        int inputCount = rayCount + 1;  // rays + health
        int outputCount = 2;  // direction + speed

        if (initializeNn)
        {
            nn = new NeuralNetwork("Rat", NeuralNetwork.NetworkType.Other, inputCount, outputCount);
            nn.Initialize(8, 12);
        }

        lifespanTimer = lifespan;

        rb = GetComponent<Rigidbody2D>();
        visualizer = GameObject.FindGameObjectWithTag("Canvas").GetComponent<Visualizer>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        HandleComputation();
        HandleHealth();
    }

    private void HandleComputation()
    {
        if (computeTickCounter < computeTick)
        {
            computeTickCounter++;
            return;
        }

        computeTickCounter = 1;
        float[] rayInputs = HandleRayCast();
        var inputArray = rayInputs.Concat(new float[] { health }).ToArray();
        var outputArray = nn.Compute(inputArray);
        var rotationalVelocity = outputArray[0] * 2f - 1f;  // -1 ~ 1
        var forwardVelocity = outputArray[1] * 2f - 1f;  // -1 ~ 1
        
        rb.angularVelocity = rotationalVelocity * rotSpeedMultiplier;
        var eulerAngles = transform.eulerAngles;
        rb.velocity = new Vector2(Mathf.Cos(eulerAngles.z * Mathf.Deg2Rad), Mathf.Sin(eulerAngles.z * Mathf.Deg2Rad)) * (forwardVelocity * speedMultiplier);
    }
    
    private float[] HandleRayCast()
    {
        var output = new float[rayCount];
        for (int i = 0; i < rayCount; i++)
        {
            var theta = (float)(transform.eulerAngles.z - rayAngle * 0.5 * (rayCount-1) + rayAngle * i) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            RaycastHit2D hit = Physics2D.Raycast((Vector2)transform.position + direction * 0.3f, direction, rayLength, rayLayer); // To ignore self collision
            Debug.DrawRay((Vector2)transform.position + direction * 0.3f, direction);
            output[i] = hit.collider ? hit.distance : -1f;
        }

        return output;
    }

    private void HandleHealth()
    {
        lifespanTimer -= Time.fixedDeltaTime;
        health = lifespanTimer / lifespan;
        if (health <= 0f)
        {
            if (visualizer.targetNeuralNetwok == nn)
                visualizer.Deselect();
            
            Destroy(gameObject);
        }

        if (health >= 2f)
        {
            Reproduce();
            lifespanTimer -= lifespan;
        }
    }

    public void Consume(float time)
    {
        lifespanTimer += time;
    }

    private void Reproduce()
    {
        var mutation = nn.CreateMutation();
        var offspring = Instantiate(agentObj, transform.position, transform.rotation);
        offspring.GetComponent<Agent>().nn = mutation;
    }
}
