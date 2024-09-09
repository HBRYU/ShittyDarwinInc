using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShooterBehaviour : MonoBehaviour
{
    public int team;
    public float maxHealth, health, lifespan;
    public float score;
    public int ammo;
    public float reloadTime;
    private float reloadTimer;
    public GameObject bulletObj;
    private float lifespanTimer;
    public NeuralNetwork nn;
    public int rayCount;
    public float rayAngle;
    public float rayLength;
    public LayerMask rayLayer;
    private Rigidbody2D rb;
    public float rotSpeedMultiplier, speedMultiplier;

    public int computeTick = 4;
    private int computeTickCounter = 1;

    public bool initializeNn = true;
    public GameObject agentObj;

    private Visualizer visualizer;

    public float weightCostLifeReductionCoeff, mobilityLifeReductionCoeff;

    private Color color;

    private float[] prevOutput;
    
    // Start is called before the first frame update
    void Start()
    {
        health = maxHealth;
        color = GetComponent<SpriteRenderer>().color;
        gameObject.layer = LayerMask.NameToLayer("Agent" + team);
        
        int outputCount = 3;  // direction + speed + fire gun
        int inputCount = rayCount * 2 + outputCount;  // rays w/ type + health + ammo + previous output

        prevOutput = new float[outputCount];

        if (initializeNn)
        {
            print(inputCount);
            nn = new NeuralNetwork("Shooter", NeuralNetwork.NetworkType.Other, inputCount, outputCount);
            nn.Initialize(4, 8);
        }

        lifespanTimer = lifespan;

        rb = GetComponent<Rigidbody2D>();
        visualizer = GameObject.FindGameObjectWithTag("Canvas").GetComponent<Visualizer>();
        //WarManager.TeamSurvivors[team].Add(gameObject);

        RemoveLayerFromMask(ref rayLayer, LayerMask.NameToLayer("Agent" + team));
        RemoveLayerFromMask(ref rayLayer, LayerMask.NameToLayer("Bullet" + team));
    }
    
    void RemoveLayerFromMask(ref LayerMask mask, int layer)
    {
        // Remove the layer from the mask using bitwise AND and complement (~)
        mask.value &= ~(1 << layer);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        reloadTimer -= Time.fixedDeltaTime;
        
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
        var inputArray = rayInputs.Concat(new float[] { health, ammo }).ToArray().Concat(prevOutput).ToArray();
        var outputArray = nn.Compute(inputArray);
        var rotationalVelocity = outputArray[0] * 2f - 1f;  // -1 ~ 1
        var forwardVelocity = outputArray[1] * 2f - 1f;  // -1 ~ 1
        bool fire = outputArray[2] >= 0f;
        
        rb.angularVelocity = rotationalVelocity * rotSpeedMultiplier;
        var eulerAngles = transform.eulerAngles;
        rb.velocity = new Vector2(Mathf.Cos(eulerAngles.z * Mathf.Deg2Rad), Mathf.Sin(eulerAngles.z * Mathf.Deg2Rad)) * (forwardVelocity * speedMultiplier);

        if(fire)
            HandleGun();

        for (int i = 0; i < outputArray.Length; i++)
        {
            prevOutput[i] = outputArray[i];
        }

        
    }
    
    private float[] HandleRayCast()
    {
        var output = new float[rayCount * 2];
        for (int i = 0; i < rayCount; i++)
        {
            var theta = (float)(transform.eulerAngles.z - rayAngle * 0.5 * (rayCount-1) + rayAngle * i) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            RaycastHit2D hit = Physics2D.Raycast((Vector2)transform.position + direction * 0.75f, direction, rayLength, rayLayer); // To ignore self collision
            Debug.DrawRay((Vector2)transform.position + direction * 0.75f, direction);
            output[i] = hit.collider ? hit.distance / rayLength : -1f;
            output[i + rayCount] = hit.collider ? (hit.collider.CompareTag(gameObject.tag) ? -1f : 1f) : 0f;  // -1 if enemy, 1 if bullet, 0 if none
        }
        return output;
    }

    private void HandleHealth()
    {

        float reduction = Time.fixedDeltaTime * (1 + rb.velocity.sqrMagnitude * mobilityLifeReductionCoeff + weightCostLifeReductionCoeff * nn.WeightCost);
        
        health -= maxHealth * reduction / lifespan;

        score = health;  // Temporary
        
        if (health <= 0f)
        {
            Die();
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
    }

    public void Die()
    {
        if (visualizer != null && visualizer.targetNeuralNetwok == nn)
            visualizer.Deselect();
            
        //WarManager.TeamSurvivors[team].Remove(gameObject);
        Destroy(gameObject);
    }

    void HandleGun()
    {
        //print("Handle Gun Called");
        
        if (reloadTimer > 0f || ammo <= 0)
            return;

        reloadTimer = reloadTime;
        ammo--;
        var bullet = Instantiate(bulletObj, transform.position, transform.rotation);
        bullet.layer = LayerMask.NameToLayer("Bullet" + team);
        bullet.transform.GetChild(0).gameObject.layer = LayerMask.NameToLayer("Bullet" + team);
        //print("Bullet instantiated");
        var behaviour = bullet.GetComponent<BulletBehaviour>();
        behaviour.team = team;
        behaviour.sprite.color = color;
        behaviour.ParentCollider = GetComponent<CircleCollider2D>();
        //bullet.GetComponent<Rigidbody2D>().velocity = bullet.transform.forward * 10f;
    }
}
