using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Food : MonoBehaviour
{
    public bool available = true;
    public float additionalLifespan;
    public float foodLifespan = 64f;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Agent") && available)
        {
            available = false;
            other.GetComponent<Agent>().Consume(additionalLifespan);
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        foodLifespan -= Time.fixedDeltaTime;
        if (foodLifespan <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
