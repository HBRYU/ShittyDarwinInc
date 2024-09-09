using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

public class WarManager : MonoBehaviour
{
    public static int[] TeamSurvivors = new int[2];
    public Color[] teamColors;
    public GameObject agentObj;
    public int teamCount, startingAgentsPerTeam = 16, endGameOnTeamAgentCount = 4;
    public Transform[] spawnAreas;
    
    // Start is called before the first frame update
    private void Awake()
    {
        TeamSurvivors[0] = 0;
        TeamSurvivors[1] = 0;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        TeamSurvivors = new int[2];
        foreach (var obj in GameObject.FindGameObjectsWithTag("ShooterAgent"))
        {
            TeamSurvivors[obj.GetComponent<ShooterBehaviour>().team]++;
        }

        foreach (var survivors in TeamSurvivors)
        {
            if (survivors <= endGameOnTeamAgentCount)
            {
                EndGame();
            }
        }
    }

    void EndGame()
    {
        print("End Game Called");
        //print(TeamSurvivors[0]);
        //print(TeamSurvivors[1]);
        
        var survivors = GameObject.FindGameObjectsWithTag("ShooterAgent");
        var nns = new NeuralNetwork[survivors.Length];
        var scores = new float[survivors.Length];
        var scoreSum = 0f;

        for (int i = 0; i < survivors.Length; i++)
        {
            var behaviour = survivors[i].GetComponent<ShooterBehaviour>();
            nns[i] = behaviour.nn;
            scores[i] = behaviour.score;  // Spaghetti
            scoreSum += scores[i];
        }


        var offspringNns = new NeuralNetwork[teamCount * startingAgentsPerTeam];
        
        for (int i = 0; i < teamCount * startingAgentsPerTeam; i++)
        {
            NeuralNetwork parent = nns[0];
            float s = 0f, x = Random.Range(0, scoreSum);
            for (int j = 0; j < scores.Length; j++)
            {
                s += scores[j];
                if (x < s)
                {
                    parent = nns[j];
                    break;
                }
            }

            offspringNns[i] = parent.CreateMutation();
        }

        for (int i = 0; i < survivors.Length; i++)
        {
            survivors[i].GetComponent<ShooterBehaviour>().Die();
        }

        for (int team = 0; team < teamCount; team++)
        {
            for (int i = 0; i < startingAgentsPerTeam; i++)
            {
                var nn = offspringNns[team * startingAgentsPerTeam + i];
                var spawnPoint = spawnAreas[team].position;
                spawnPoint.x += Random.Range(-spawnAreas[team].localScale.x, spawnAreas[team].localScale.x) * 0.5F;
                spawnPoint.y += Random.Range(-spawnAreas[team].localScale.y, spawnAreas[team].localScale.y) * 0.5f;
                var agent = Instantiate(agentObj, spawnPoint, quaternion.identity);
                agent.GetComponent<ShooterBehaviour>().team = team;
                agent.GetComponent<SpriteRenderer>().color = teamColors[team];
                agent.GetComponent<ShooterBehaviour>().initializeNn = false;
                agent.GetComponent<ShooterBehaviour>().nn = nn;
            }
        }
    }
}
