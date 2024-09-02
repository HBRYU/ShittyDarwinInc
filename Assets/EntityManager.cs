using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class EntityManager : MonoBehaviour
{
    public static int AgentGlobalCapacity = 256;
    public static int AgentCount = 0;
    public GameObject agentObj;

    public static void AddAgent()
    {
        AgentCount++;
    }

    public static void DestroyAgent()
    {
        AgentCount--;
    }

    public static bool AgentSpawnable()
    {
        return AgentCount < AgentGlobalCapacity;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Instantiate(agentObj, new Vector3(Random.Range(-50, 50), Random.Range(-50, 50)), quaternion.identity);
        }
    }
}
