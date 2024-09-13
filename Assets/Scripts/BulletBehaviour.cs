using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletBehaviour : MonoBehaviour
{
    public int team;
    public float damage, lifeSpan;
    [HideInInspector] public float lifeSpanTimer;
    public SpriteRenderer sprite;

    [HideInInspector]
    public bool collided = false;

    public Collider2D Collider;
    public Collider2D ParentCollider;

    public float speed = 7.5f;

    private Rigidbody2D rb;
    
    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        Physics2D.IgnoreCollision(Collider, ParentCollider);
        rb = GetComponent<Rigidbody2D>();
        lifeSpanTimer = lifeSpan;
    }

    private void FixedUpdate()
    {
        rb.velocity = transform.right * speed;
        lifeSpanTimer -= Time.fixedDeltaTime;
        if (lifeSpanTimer < 0f || !ParentCollider)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ShooterAgent") && !collided && other != ParentCollider)
        {
            var behaviour = other.GetComponent<ShooterBehaviour>();
            bool enemyDead = behaviour.TakeDamage(damage);
            collided = true;
            if (ParentCollider)
            {
                var parentBehaviour = ParentCollider.GetComponent<ShooterBehaviour>();
                parentBehaviour.health += damage;
                parentBehaviour.score += enemyDead ? 3f : 1f;
                Physics2D.IgnoreCollision(Collider, ParentCollider, false);
            }
            Despawn();
        }
    }

    public void ResetLifeSpan()
    {
        lifeSpanTimer = lifeSpan;
    }

    void Despawn()
    {
        gameObject.SetActive(false);
        BulletPool.Despawn();
    }
}
