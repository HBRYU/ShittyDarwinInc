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
        if(Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.A))
            Time.timeScale += Input.GetAxisRaw("Horizontal") * timeScaleDelta;
        if (Input.GetKeyDown(KeyCode.S))
            Time.timeScale = 1f;
    }
}
