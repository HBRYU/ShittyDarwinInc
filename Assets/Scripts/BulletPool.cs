using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    public Transform bulletParent;
    public GameObject bulletObj;
    public int maxInstanceCount = 400;
    public int initialInstanceCount = 200;
    static int activeInstaceCount = 0;

    private void Awake()
    {
        for (int i = 0; i < initialInstanceCount; i++)
        {
            var bullet = Instantiate(bulletObj, bulletParent);
            bullet.SetActive(false);
        }
    }


    void ManageObjCount()
    {
        while (activeInstaceCount > bulletParent.childCount - 64)
        {
            var bullet = Instantiate(bulletObj, bulletParent);
            bullet.SetActive(false);
        }
    }

    Transform GetInactiveBullet()
    {
        for (int i = 0; i < bulletParent.childCount; i++)
        {
            var child = bulletParent.GetChild(i);
            if (!child.gameObject.activeSelf)
                return child;
        }

        return transform;
    }

    public void SpawnBullet(Vector3 pos, Quaternion rot, Collider2D parentCollider, int team, Color color)
    {
        ManageObjCount();

        var bullet = GetInactiveBullet();
        bullet.gameObject.SetActive(true);
        bullet.position = pos;
        bullet.rotation = rot;
        var behaviour = bullet.GetComponent<BulletBehaviour>();
        behaviour.ResetLifeSpan();
        behaviour.ParentCollider = parentCollider;
        behaviour.team = team;
        behaviour.sprite.color = color;
        behaviour.collided = false;
        bullet.gameObject.layer = LayerMask.NameToLayer("Bullet" + team);
        bullet.GetChild(0).gameObject.layer = LayerMask.NameToLayer("Bullet" + team);
        Physics2D.IgnoreCollision(behaviour.Collider, parentCollider);
        activeInstaceCount++;
    }

    public static void Despawn()
    {
        activeInstaceCount--;
    }
}
