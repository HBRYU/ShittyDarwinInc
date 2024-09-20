using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneBehaviour : MonoBehaviour
{
    public bool useRealTime;
    public float score;
    public Transform targetTransform;

    public Transform thrusterRTransform, thrusterLTransform;
    public Transform bodyTransform;  // for calculating moment of inertia
    
    public NeuralNetwork nn;
    public bool initializeNetwork = true;

    public float deltaTime = 0.02f;
    public float maxSpeedCap = 20f, maxAngularSpeedCap = 20f;
    private Vector2 _velocity = new Vector2(0f, 0f);
    private float _angularVelocity = 0f;
    public float mass, thrustForce;
    private float _thrusterROutput, _thrusterRAngle;
    private float _thrusterLOutput, _thrusterLAngle;
    private Vector2 _acceleration = new Vector2(0f, 0f);

    private float[] inputArray;
    private float[] outputArray;

    private float distanceCovered = 0f;


    private readonly Dictionary<string, int> _outputMap = new Dictionary<string, int>()
    {
        { "thrusterR", 0 },
        { "thrusterRAngle", 1 },
        { "thrusterL", 2 },
        { "thrusterLAngle", 3 },
    };
    
    // Start is called before the first frame update
    void Start()
    {
        // delta position to target(2) + velocity(2) + rotation & angular velocity (2)
        int inputs = 2 + 2 + 2;
        inputArray = new float[inputs];
        // thruster R output + angle (2) + thruster L " (2)
        int outputs = 2 + 2;
        outputArray = new float[outputs];
        
        if (initializeNetwork)
            InitializeNetwork(inputs, outputs);

        targetTransform = GameObject.FindGameObjectWithTag("GM").GetComponent<DroneTrainer>().targetTransform;
    }

    void InitializeNetwork(int inputs, int outputs)
    {
        nn = new NeuralNetwork("Drone", NeuralNetwork.NetworkType.Neat, inputs, outputs);
        nn.Initialize(minHiddenNodes: 4, maxHiddenNodes: 7);
    }
    
    // Update is called once per frame
    void Update()
    {
        if (useRealTime)
            return;
        HandleComputation();
        HandlePhysics();
        HandleScore();
    }
    
    void FixedUpdate()
    {
        if (!useRealTime)
            return;
        HandleComputation();
        HandlePhysics();
        HandleScore();
    }


    void HandleComputation()
    {
        Vector2 deltaPos = targetTransform.position - transform.position;
        inputArray = new[]
        {
            deltaPos.x, deltaPos.y, _velocity.x, _velocity.y, transform.eulerAngles.z * Mathf.Deg2Rad,
            _angularVelocity
        };

        const float clamp = 10f;
        
        for (int i = 0; i < inputArray.Length; i++)
        {
            inputArray[i] = Mathf.Clamp(inputArray[i], -clamp, clamp);
        }
        
        // print(inputArray.Length);
        // print(nn.Inputs);
        outputArray = nn.Compute(inputArray);
        
        _thrusterROutput = outputArray[0];
        
        _thrusterRAngle = outputArray[1];
        
        _thrusterLOutput = outputArray[2];
        
        _thrusterLAngle = outputArray[3];
    }

    void HandlePhysics()
    {
        // Process nn outputs to force and angle units
        _thrusterROutput *= thrustForce;
        _thrusterLOutput *= thrustForce;

        const float angleCoeff = Mathf.PI / 3f;
        _thrusterRAngle = ((_thrusterRAngle * 2f) - 1f) * angleCoeff; // to (-angleCoeff, angleCoeff)
        _thrusterLAngle = ((_thrusterLAngle * 2f) - 1f) * angleCoeff;

        HandleGraphics(_thrusterRAngle * Mathf.Rad2Deg, _thrusterLAngle * Mathf.Rad2Deg);
        
        float bodyAngle = transform.localEulerAngles.z * Mathf.Deg2Rad;
        
        // Direction of force (world direction)
        Vector2 thrusterRDirection = new Vector2(Mathf.Cos(_thrusterRAngle + bodyAngle + Mathf.PI / 2f),
            Mathf.Sin(_thrusterRAngle + bodyAngle + Mathf.PI / 2f));
        Vector2 thrusterLDirection = new Vector2(Mathf.Cos(_thrusterLAngle + bodyAngle + Mathf.PI / 2f),
            Mathf.Sin(_thrusterLAngle + bodyAngle + Mathf.PI / 2f));
        
        // Apply thruster forces
        _acceleration += thrusterRDirection * _thrusterROutput / mass;
        _acceleration += thrusterLDirection * _thrusterLOutput / mass;

        // Apply torque
        Vector3 torque =
            Vector3.Cross(thrusterRTransform.position - transform.position, thrusterRDirection * _thrusterROutput) +
            Vector3.Cross(thrusterLTransform.position - transform.position, thrusterLDirection * _thrusterLOutput);

        var localScale = bodyTransform.localScale;
        float I = (1f / 12f) * mass * (localScale.x * localScale.x +
                                       localScale.y * localScale.y);  // moment of inertia

        float alpha = torque.z / I;  // angular acceleration
        
        // Apply physics
        _acceleration += Vector2.down * 9.81f / mass;  // gravity
        _velocity += _acceleration * deltaTime;
        if (_velocity.sqrMagnitude > maxSpeedCap * maxSpeedCap)
            _velocity = _velocity.normalized * maxSpeedCap;
        _angularVelocity += alpha * deltaTime;
        if (_angularVelocity > maxAngularSpeedCap)
            _angularVelocity = maxAngularSpeedCap;
        else if (_angularVelocity < -maxAngularSpeedCap)
            _angularVelocity = -maxAngularSpeedCap;
        
        transform.position += (Vector3)_velocity * deltaTime;
        distanceCovered += _velocity.magnitude * deltaTime;
        transform.localEulerAngles += new Vector3(0f, 0f, _angularVelocity * Mathf.Rad2Deg * deltaTime);
    }

    void HandleGraphics(float rAngle, float lAngle)
    {
        thrusterRTransform.localEulerAngles = new Vector3(0f, 0f, rAngle);
        thrusterLTransform.localEulerAngles = new Vector3(0f, 0f, lAngle);
    }

    void HandleScore()
    {
        //score = -(Vector3.SqrMagnitude(transform.position - targetTransform.position) * distanceCovered) / 100f;
        score += -(Vector3.Distance(transform.position, targetTransform.position)) / 512f;
    }
}
