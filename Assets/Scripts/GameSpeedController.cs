using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameSpeedController : MonoBehaviour
{
    public float timeScaleDelta = 0.5f;
    // Start is called before the first frame update
    void Start()
    {
        Time.timeScale = 1f;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
            Time.timeScale += timeScaleDelta;
        if(Input.GetKeyDown(KeyCode.Q))
            Time.timeScale -= timeScaleDelta;
        if (Input.GetKeyDown(KeyCode.T))
            Time.timeScale = 1f;
    }
}
