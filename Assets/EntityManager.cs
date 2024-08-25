using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityManager : MonoBehaviour
{
    public static int AgentGlobalCapacity = 256;
    public static int AgentCount = 0;

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
}
