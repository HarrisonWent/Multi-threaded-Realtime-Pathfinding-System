using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float Speed = 6;
    public ExplosionGun owner;

    private void OnEnable()
    {
        Invoke("ReturnToPool", 5);
    }
    void Update()
    {
        transform.Translate(Vector3.forward * Speed * Time.deltaTime, Space.Self);
    }

    //When it hits a destructible object destroy this and frature the object
    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Hit: " + other.name + ", parent: " + owner.name);
        if(other.TryGetComponent(out Fracture fracture))
        {
            fracture.DoDestruct(transform.position, Speed);            
        }
        else if(other.TryGetComponent(out Health health))
        {
            health.Die();
        }

        ReturnToPool();
    }

    void ReturnToPool()
    {
        owner.ReturnToPool(this);
    }
}
