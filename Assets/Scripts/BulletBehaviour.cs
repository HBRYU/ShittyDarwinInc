using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletBehaviour : MonoBehaviour
{
    public int team;
    public float damage, lifeSpan;
    public SpriteRenderer sprite;

    private bool collided = false;

    public Collider2D Collider;
    public Collider2D ParentCollider;

    public float speed = 7.5f;

    private Rigidbody2D rb;
    
    private void Start()
    {
        Physics2D.IgnoreCollision(Collider, ParentCollider);
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        rb.velocity = transform.right * speed;
        lifeSpan -= Time.fixedDeltaTime;
        if(lifeSpan < 0f || !ParentCollider)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ShooterAgent") && !collided && other != ParentCollider)
        {
            var behaviour = other.GetComponent<ShooterBehaviour>();
            if (ParentCollider && behaviour.team == team)
            {
                Destroy(gameObject);
                collided = true;
                return;
            }
            
            behaviour.TakeDamage(damage);
            collided = true;
            if(ParentCollider)
                ParentCollider.GetComponent<ShooterBehaviour>().health += damage;
            Destroy(gameObject);
        }
    }
}
