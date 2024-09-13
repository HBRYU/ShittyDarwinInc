using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class CameraControl : MonoBehaviour
{
    private Camera _camera;
    private float _initialSize;
    public float moveSpeed;
    public float cameraSizeDelta;
    
    // Start is called before the first frame update
    void Start()
    {
        _camera = GetComponent<Camera>();
        _initialSize = _camera.orthographicSize;
        Reset();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.position += new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0f) * moveSpeed;
    }

    private void Update()
    {
        if (Input.mouseScrollDelta.y > 0f)
        {
            _camera.orthographicSize -= cameraSizeDelta;
        }
        else if (Input.mouseScrollDelta.y < 0f)
        {
            _camera.orthographicSize += cameraSizeDelta;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Reset();
        }
    }

    void Reset()
    {
        transform.position = new Vector3(0f, 0f, -10f);
        _camera.orthographicSize = _initialSize;
    }
}
