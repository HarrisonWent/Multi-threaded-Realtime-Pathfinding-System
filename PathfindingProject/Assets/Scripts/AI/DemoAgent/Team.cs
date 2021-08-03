using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Team : MonoBehaviour
{
    public enum Side
    {
        Unassigned,
        Blue,
        Orange
    }public Side myTeam  = Side.Unassigned;

    public static List<Team> AllPlayers;

    /// <summary>
    /// Gets an enemy transform
    /// </summary>
    /// <returns>A random transform from a player on a team different from its own</returns>
    public Transform GetEnemy()
    {
        List<Team> enemies = new List<Team>();
        foreach(Team t in AllPlayers)
        {
            if(t.myTeam != myTeam)
            {
                enemies.Add(t);
            }
        }
        return enemies[Random.Range(0, enemies.Count)].transform;
    }
}
