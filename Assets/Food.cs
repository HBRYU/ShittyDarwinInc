using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Food : MonoBehaviour
{
    public bool available = true;
    public float additionalLifespan;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Agent") && available)
        {
            available = false;
            other.GetComponent<Agent>().Consume(additionalLifespan);
            Destroy(gameObject);
        }
    }
}
