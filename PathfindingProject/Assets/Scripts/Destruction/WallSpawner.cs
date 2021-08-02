using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallSpawner : MonoBehaviour
{
    public GameObject wall;
    public GameObject Prefab;
    public float Delay = 5;
    private float timer = 0;
    
    private void Start()
    {
        transform.position = wall.transform.position;
        transform.rotation = wall.transform.rotation;
    }

    void Update()
    {
        if(wall == null)
        {
            timer += Time.deltaTime;
            if (timer >= Delay)
            {
                wall = Instantiate(Prefab, transform.position, transform.rotation);
                timer = 0f;
            }
        }
    }
}
