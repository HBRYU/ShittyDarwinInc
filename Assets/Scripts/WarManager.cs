using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

public class WarManager : MonoBehaviour
{
    public static Dictionary<int, int> TeamSurvivors = new Dictionary<int, int>();
    public Color[] teamColors;
    public GameObject agentObj;
    public int teamCount, startingAgentsPerTeam = 16, endGameOnTeamAgentCount = 4, mutantsPerTeam = 4;
    public Transform[] spawnAreas;

    private Visualizer _visualizer;
    private int generation = 1;
    private TextMeshProUGUI generationText;
    
    private void Awake()
    {
        TeamSurvivors[LayerMask.NameToLayer("Agent0")] = 0;
        TeamSurvivors[LayerMask.NameToLayer("Agent1")] = 0;
    }

    void Start()
    {
        _visualizer = GameObject.FindGameObjectWithTag("Canvas").GetComponent<Visualizer>();
        generationText = _visualizer.generation;
        generationText.text = "Generation " + generation;
        StartGame();
    }
    
    void FixedUpdate()
    {
        TeamSurvivors = new Dictionary<int, int>();
        TeamSurvivors[LayerMask.NameToLayer("Agent0")] = 0;
        TeamSurvivors[LayerMask.NameToLayer("Agent1")] = 0;
        
        foreach (var obj in GameObject.FindGameObjectsWithTag("ShooterAgent"))
        {
            TeamSurvivors[obj.layer]++;
        }

        foreach (var survivors in TeamSurvivors)
        {
            if (survivors.Value <= endGameOnTeamAgentCount)
            {
                EndGame();
            }
        }
    }

    void StartGame()
    {
        for (int team = 0; team < teamCount; team++)
        {
            for (int i = 0; i < startingAgentsPerTeam; i++)
            {
                var spawnPoint = spawnAreas[team].position;
                spawnPoint.x += Random.Range(-spawnAreas[team].localScale.x, spawnAreas[team].localScale.x) * 0.5F;
                spawnPoint.y += Random.Range(-spawnAreas[team].localScale.y, spawnAreas[team].localScale.y) * 0.5f;
                var agent = Instantiate(agentObj, spawnPoint, quaternion.identity);
                agent.GetComponent<ShooterBehaviour>().team = team;
                agent.GetComponent<SpriteRenderer>().color = teamColors[team];
            }
        }
    }

    void EndGame()
    {
        print("End Game Called");
        //print(TeamSurvivors[0]);
        //print(TeamSurvivors[1]);
        
        generationText.text = "Generation " + ++generation;
        
        var survivors = GameObject.FindGameObjectsWithTag("ShooterAgent");
        var nns = new NeuralNetwork[survivors.Length];
        var scores = new float[survivors.Length];
        var scoreSum = 0f;

        for (int i = 0; i < survivors.Length; i++)
        {
            var behaviour = survivors[i].GetComponent<ShooterBehaviour>();
            nns[i] = behaviour.nn;
            scores[i] = Mathf.Pow(behaviour.score * 10, 2) + behaviour.health;  // Spaghetti
            scoreSum += scores[i];
        }


        var offspringNns = new NeuralNetwork[teamCount * startingAgentsPerTeam];
        var indexes = new List<int>(teamCount * startingAgentsPerTeam);
        
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
            if (i >= teamCount * (startingAgentsPerTeam - mutantsPerTeam))
                offspringNns[i].Initialize();
            indexes.Add(i);
        }

        foreach (var s in survivors)
        {
            s.GetComponent<ShooterBehaviour>().Die();
        }

        for (int team = 0; team < teamCount; team++)
        {
            for (int i = 0; i < startingAgentsPerTeam; i++)
            {
                var index = indexes[Random.Range(0, indexes.Count)];
                indexes.Remove(index);
                var nn = offspringNns[index];
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
