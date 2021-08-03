using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    /// <summary>
    /// Respawns the agent
    /// </summary>
    public void Die()
    {
        transform.position = GameManager.GetSpawn();
    }
}