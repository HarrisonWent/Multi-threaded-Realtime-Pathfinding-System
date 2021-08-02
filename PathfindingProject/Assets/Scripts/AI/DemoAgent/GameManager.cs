using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static Queue<Vector3> SpawnPoints;
    private void Start()
    {
        SpawnPoints = new Queue<Vector3>();
        Team.AllPlayers = new List<Team>();
        foreach (Team t in FindObjectsOfType<Team>())
        {
            Team.AllPlayers.Add(t);
            SpawnPoints.Enqueue(t.transform.position);
        }
    }

    public static Vector3 GetSpawn()
    {
        Vector3 vector3 = SpawnPoints.Dequeue();
        SpawnPoints.Enqueue(vector3);
        return vector3;
    }
}
