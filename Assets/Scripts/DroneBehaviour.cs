using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneBehaviour : MonoBehaviour
{
    public bool useRealTime;
    public float score;
    //public Transform targetTransform;  // Deprecated
    
    // Referencing Pezzza's Work
    // Collecting destination tokens
    public Vector3[] destinations;  // To be set in inspector
    private int _currentDestinationIndex = 0;
    public float tokenCollectTime = 0.5f;
    public float tokenCollectRange = 0.5f;
    private float _tokenCollectTimer;

    public Transform thrusterRTransform, thrusterLTransform;
    public Transform bodyTransform;  // for calculating moment of inertia
    public Transform thrusterRExhaust, thrusterLExhaust;
    
    public NeuralNetwork nn;
    public bool initializeNetwork = true;

    public float deltaTime = 0.02f, g=9.81f;
    [Range(0f, 1f)] public float thrusterThreshold;
    public float dampingFactor = 0.98f, angularDampingFactor = 0.98f; 
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

    [Header("Score System Settings")] public float scoreOnTokenCollected = 100f;
    public float scoreDistanceWeight, scoreDirectionWeight, scoreStabilityWeight;
    


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
        // delta position to target(2) + velocity(2) + rotation (sin + cos) & angular velocity (3)
        int inputs = 2 + 2 + 3;
        inputArray = new float[inputs];
        // thruster R output + angle (2) + thruster L " (2)
        int outputs = 2 + 2;
        outputArray = new float[outputs];
        
        if (initializeNetwork)
            InitializeNetwork(inputs, outputs);

        // targetTransform = GameObject.FindGameObjectWithTag("GM").GetComponent<DroneTrainer>().targetTransform;

        _tokenCollectTimer = tokenCollectTime;
        _currentDestinationIndex = Random.Range(0, destinations.Length);
    }

    void InitializeNetwork(int inputs, int outputs)
    {
        nn = new NeuralNetwork("Drone", NeuralNetwork.NetworkType.Neat, inputs, outputs);
        nn.Initialize(minHiddenNodes: 8, maxHiddenNodes: 16);
    }
    
    void Loop()
    {
        HandleComputation();
        HandlePhysics();
        HandleScore();
        HandleDestination();
    }
    
    void Update()
    {
        if (useRealTime)
            return;
        Loop();
    }
    
    void FixedUpdate()
    {
        if (!useRealTime)
            return;
        Loop();
    }


    void HandleComputation()
    {
        Vector3 destination = destinations[_currentDestinationIndex];
        Vector2 deltaPos = destination - transform.position;
        
        // Referencing Pezzza's Work: https://www.youtube.com/watch?v=hQ4ryudP4j8
        // Use sin cos rot instead of plain rad 
        float theta = (transform.eulerAngles.z * Mathf.Deg2Rad);
        
        inputArray = new[]
        {
            deltaPos.x * 0.5f, deltaPos.y * 0.5f, _velocity.x, _velocity.y, Mathf.Sin(theta), Mathf.Cos(theta),
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
        
        _thrusterROutput = outputArray[_outputMap["thrusterR"]];
        
        _thrusterRAngle = outputArray[_outputMap["thrusterRAngle"]];
        
        _thrusterLOutput = outputArray[_outputMap["thrusterL"]];
        
        _thrusterLAngle = outputArray[_outputMap["thrusterLAngle"]];
    }

    void HandlePhysics()
    {
        // Process nn outputs to force and angle units
        /*_thrusterROutput *= thrustForce;
        _thrusterLOutput *= thrustForce;*/
        // Cutoff low output to allow room for control
        _thrusterROutput = Mathf.Clamp01(_thrusterROutput - thrusterThreshold) * (1f/(1-thrusterThreshold)) * thrustForce;
        _thrusterLOutput = Mathf.Clamp01(_thrusterLOutput - thrusterThreshold) * (1f/(1-thrusterThreshold)) * thrustForce;

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
        _acceleration += Vector2.down * g;  // gravity
        _velocity += _acceleration * deltaTime;
        _velocity *= new Vector2(dampingFactor, dampingFactor);  // This works now apparently. Multiply by element.
        if (_velocity.sqrMagnitude > maxSpeedCap * maxSpeedCap)
            _velocity = _velocity.normalized * maxSpeedCap;
        _angularVelocity += alpha * deltaTime;
        _angularVelocity *= angularDampingFactor;
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

        thrusterRExhaust.localScale = new Vector3(1, _thrusterROutput * 2f / thrustForce, 1f);
        thrusterLExhaust.localScale = new Vector3(1, _thrusterLOutput * 2f / thrustForce, 1f);

        thrusterRExhaust.localPosition = new Vector3(0f, -0.45f - _thrusterROutput / thrustForce, 0f);
        thrusterLExhaust.localPosition = new Vector3(0f, -0.45f - _thrusterLOutput / thrustForce, 0f);
    }

    void HandleDestination()
    {
        Vector2 destination = destinations[_currentDestinationIndex];
        if (Vector3.Distance(transform.position, destination) <= tokenCollectRange)
        {
            _tokenCollectTimer -= deltaTime;
            score += 5f * deltaTime;
            if (_tokenCollectTimer <= 0f)
            {
                SetNewDestination();
                _tokenCollectTimer = tokenCollectTime;
            }
        }

        void SetNewDestination()
        {
            _currentDestinationIndex = Random.Range(0, destinations.Length);
            score += 100f;  // Arbitrary
        }
    }

    void HandleScore()
    {
        //score = -(Vector3.SqrMagnitude(transform.position - targetTransform.position) * distanceCovered) / 100f;
        //score += -(Vector3.Distance(transform.position, targetTransform.position)) / 512f;
        
        Vector3 destination = destinations[_currentDestinationIndex];

        score += Vector2.Dot(_velocity.normalized, (destination - transform.position).normalized) * deltaTime * scoreDirectionWeight;
        score += -(Vector3.Distance(transform.position, destination)) * deltaTime * scoreDistanceWeight;

        // Punish upside down drones
        score -= Mathf.Sin(transform.eulerAngles.z * Mathf.Deg2Rad) * deltaTime * scoreStabilityWeight;
        
    }
}
