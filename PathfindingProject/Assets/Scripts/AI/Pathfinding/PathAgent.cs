using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathAgent : MonoBehaviour
{
    public bool CanFly = false;
    public int Speed = 6;
    private LineRenderer GameViewPath;
    private Vector3 CurrentTarget;
    private Transform CurrentTargetTransform;
    private Pathfinding Pathfinding;
    private bool setup = false;
    private bool isStopped = false;

    private bool Waiting = false;

    private void Setup()
    {
        Pathfinding = FindObjectOfType<Pathfinding>();
        GameViewPath = GetComponent<LineRenderer>();
        Route = new List<PathAction>();
        setup = true;
    }

    public void SetDestination(Vector3 Destination)
    {
        if (!setup) { Setup(); }
        isStopped = false;
        CurrentTarget = Destination;
        CurrentTargetTransform = null;
        Queue(false);
    }

    private void Queue(bool nextupdate)
    {
        if (!Waiting)
        {
            Pathfinding.QueueAPath(this, JobDone, CanFly, nextupdate);
            Waiting = true;
        }
    }

    public void SetDestinationTransform(Transform Destination)
    {
        if (!setup) { Setup(); }
        isStopped = false;
        CurrentTargetTransform = Destination;
        Queue(false);
    }

    public void StopAgent()
    {
        Route.Clear();
        CurrentTargetTransform = null;
        isStopped = true;
    }

    public Vector3 GetDesiredVelocity()
    {
        if (Route == null || Route.Count == 0) { return Vector3.zero; }

        //Remove close postions
        for (int i = 0; i<Route.Count;i++)
        {
            if (Route[i].NeedsDestroy) 
            {
                Collider[] colliders= Physics.OverlapSphere(Route[i].Position, Navigation.BakedAgentSize);
                
                bool blocked = false;
                foreach(Collider c in colliders)
                {
                    if(c.TryGetComponent(out Fracture fracture))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked)
                {
                    Route[i].NeedsDestroy = false;
                }
                break; 
            }
            if(Vector3.Distance(transform.position, Route[i].Position) < Navigation.BakedAgentSize * 1.5f)
            {                
                Route.RemoveAt(i);
                i--;
            }
            else
            {
                break;
            }
        }

        UpdateLineRenderer();

        if(Route.Count == 0) { return Vector3.zero; }

        return (Route[0].Position - transform.position).normalized * Speed;
    }

    /// <summary>
    /// Called from pathfinding when the set destination request has been completed
    /// </summary>
    /// <param name="Path"></param>
    public void JobDone(List<PathAction> Path)
    {
        Waiting = false;

        if (isStopped) { return; }

        Route.Clear();

        if (Path.Count > 0)
        {
            //if we are closer to another point on the route than the starting then skip to that point
            
            Route.AddRange(Path);
            float Record = Vector3.Distance(transform.position, Route[0].Position);
            float dist;
            for (int i = 0; i < Route.Count; i++)
            {
                dist = Vector3.Distance(Route[i].Position, transform.position);
                if (dist < Record)
                {
                    Route.RemoveRange(0, i);
                    i = 0;
                    Record = dist;
                }
            }
        }

        UpdateLineRenderer();

        Queue(true);
    }

    public List<PathAction> Route;

    /// <summary>
    /// Gets the nest position that needs to be destroyed
    /// </summary>
    /// <returns></returns>
    public PathAction GetShootPoint()
    {
        if (Route == null || Route.Count == 0) { return null; }

        for (int i = 0; i<Route.Count;i++)
        {
            if(Route[i].NeedsDestroy)
            {
                return Route[i];
            }
        }

        return null;
    }

    void UpdateLineRenderer()
    {
        GameViewPath.positionCount = Route.Count;
        for (int i = 0; i < Route.Count; i++)
        {
            GameViewPath.SetPosition(i, Route[i].Position);
        }
    }

    public Vector3 GetCurrentTarget()
    {
        if (CurrentTargetTransform) { return CurrentTargetTransform.position; }
        return CurrentTarget;
    }
}

