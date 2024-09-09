using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class FoodSpawn : MonoBehaviour
{
    public int globalLimit = 200;
    public GameObject foodObj;
    private int foodCount;
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

    // Update is called once per frame
    void FixedUpdate()
    {
        foodCount = GameObject.FindGameObjectsWithTag("Food").Length;
        if (foodCount < globalLimit)
            Instantiate(foodObj, new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY)) * 0.9f,
                quaternion.identity);
    }
}
