using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoAgent : MonoBehaviour
{
    private PathAgent pathAgent;
    public Transform Target;
    private CharacterController characterController;

    public enum DemoMode
    {
        Chase,
        Static,
        Combat
    }public DemoMode demoMode = DemoMode.Static;

    private void Start()
    {
        pathAgent = GetComponent<PathAgent>();
        characterController = GetComponent<CharacterController>();
        if (demoMode != DemoMode.Combat)
        {
            pathAgent.SetDestinationTransform(Target);
        }        
    }

    float TimeSinceGotTarget = 5,rate = 5;
    private void Update()
    {
        if(demoMode == DemoMode.Static) { return; }

        Vector3 DesiredVelocity = pathAgent.GetDesiredVelocity();

        if (TimeSinceGotTarget < 5)
        {
            TimeSinceGotTarget += Time.deltaTime;
        }

        if (demoMode == DemoMode.Combat && TimeSinceGotTarget>= rate)
        {
            Target = GetComponent<Team>().GetEnemy();
            pathAgent.SetDestinationTransform(Target);
            TimeSinceGotTarget = 0;
        }

        if (pathAgent.CanFly)
        {
            //Move doesn't apply gravity
            characterController.Move(DesiredVelocity * Time.deltaTime);            
        }
        else
        {
            //Simple move applies gravity
            characterController.SimpleMove(DesiredVelocity);
        }
    }
}
