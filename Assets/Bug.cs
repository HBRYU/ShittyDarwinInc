using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Random = UnityEngine.Random;

public class Bug : MonoBehaviour
{
    public float speed, sightRange, additionalLifespan;
    public LayerMask agentLayer;
    private bool _consumed = false;
    private Rigidbody2D _rb;
    public float splitTime;
    private float splitTimer;
    
    // Start is called before the first frame update
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        EntityManager.BugCount++;
        splitTimer = splitTime;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        splitTimer -= Time.fixedDeltaTime;
        if (splitTimer <= 0f && EntityManager.BugCount < 100)
        {
            splitTimer = splitTime;
            Instantiate(gameObject, transform.position, quaternion.identity);
        }
        
        
        
        var col = Physics2D.OverlapCircleAll(transform.position, sightRange, agentLayer);
        Vector3 sum = Vector3.zero;

        if (col.Length == 0)
        {
            var rad = Random.Range(0, 2 * Mathf.PI);
            _rb.velocity = _rb.velocity * 0.5f + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (0.5f * speed);
            return;
        }
        
        foreach (var collider in col)
        {
            sum += (collider.transform.position - transform.position) / col.Length;
        }

        _rb.velocity = -sum.normalized * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Agent") && !_consumed)
        {
            _consumed = true;
            other.GetComponent<Agent>().Consume(additionalLifespan);
            EntityManager.BugCount--;
            Destroy(gameObject);
        }
    }
}
