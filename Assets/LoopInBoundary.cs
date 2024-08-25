using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;

public class LoopInBoundary : MonoBehaviour
{
    private float maxX, minX, maxY, minY;
    void Start()
    {
        Transform wallsParent = GameObject.FindGameObjectWithTag("Walls").transform;
        Transform[] walls = new Transform[4];
        for (int i = 0; i < wallsParent.childCount; i++)
        {
            walls[i] = wallsParent.GetChild(i);
        }
        
        maxX = Mathf.NegativeInfinity;
        maxY = Mathf.NegativeInfinity;
        minX = Mathf.Infinity;
        minY = Mathf.Infinity;

        foreach (var wall in walls)
        {
            var pos = wall.position;
            if (maxX < pos.x)
                maxX = pos.x;
            if (maxY < pos.y)
                maxY = pos.y;
            if (minX > pos.x)
                minX = pos.x;
            if (minY > pos.y)
                minY = pos.y;
        }
    }

    void FixedUpdate()
    {
        if (transform.position.x > maxX)
            transform.position = new Vector3(minX + 1f, transform.position.y);
        if (transform.position.y > maxY)
            transform.position = new Vector3(transform.position.x, minY + 1f);
        if (transform.position.x < minX)
            transform.position = new Vector3(maxX - 1f, transform.position.y);
        if (transform.position.y < minY)
            transform.position = new Vector3(transform.position.x, maxY - 1f);
    }
}
