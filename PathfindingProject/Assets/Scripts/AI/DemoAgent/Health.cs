using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    
    public void Die()
    {
        transform.position = GameManager.GetSpawn();
    }
}