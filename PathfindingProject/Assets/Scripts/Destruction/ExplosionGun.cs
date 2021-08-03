using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionGun : MonoBehaviour
{
    public GameObject projectile;
    public int Range = 10;
    private PathAgent pathAgent;
    private DemoAgent demoAgent;
    private ExplosionGun explosionGun;
    private Queue<Projectile> ProjectilePool = new Queue<Projectile>();

    private void Start()
    {
        pathAgent = GetComponent<PathAgent>();
        demoAgent = GetComponent<DemoAgent>();
        explosionGun = GetComponent<ExplosionGun>();        
    }

    public void ReturnToPool(Projectile projectile)
    {
        ProjectilePool.Enqueue(projectile);
        projectile.gameObject.SetActive(false);
    }

    public void Shoot(Vector3 Direction,Vector3 point)
    {
        counter = 0f;

        //Spawn projectile and aim at target
        Projectile newprojectile;

        if(ProjectilePool.Count == 0)
        {
            newprojectile = Instantiate(projectile).GetComponent<Projectile>();
            newprojectile.owner = this;
        }
        else
        {
            newprojectile = ProjectilePool.Dequeue();
            newprojectile.gameObject.SetActive(true);
        }

        newprojectile.transform.position = transform.position + Direction;
        newprojectile.transform.LookAt(point);
    }

    public float FireRate = 0.4f;
    private float counter = 0f;
    private void Update()
    {
        if (counter < FireRate)
        {
            counter += Time.deltaTime;
            return;
        }

        PathAction pathAction = pathAgent.GetShootPoint();
        
        if (pathAction != null)
        {
            //Check if in range
            if (Vector3.Distance(pathAction.Position, transform.position) > Range) { return; }

            Vector3 DirectionToTarget = (pathAction.Position - transform.position).normalized;

            //Check if any objects in the way
            if (Physics.Raycast(transform.position + DirectionToTarget, DirectionToTarget, out RaycastHit raycastHit, Range))
            {
                if (raycastHit.collider.TryGetComponent(out ObjectNavigationProperties objectNavigationProperties))
                {
                    //check if that is breakable
                    foreach (CustomNavigationSurface customNavigationSurface in Navigation.SurfaceTypes)
                    {
                        if (objectNavigationProperties.SurfaceID == customNavigationSurface.HiddenID)
                        {
                            //if its breakable shoot
                            if (!customNavigationSurface.Breakable) { return; }
                            break;
                        }
                    }
                }
                else
                {
                    return;
                }
            }

            //Fire the gun
            explosionGun.Shoot(DirectionToTarget, pathAction.Position);
            return;
        }

        if (demoAgent.demoMode == DemoAgent.DemoMode.Combat && demoAgent.Target)
        {
            Vector3 DirectionToTarget = (demoAgent.Target.position - transform.position).normalized;

            if (Physics.Raycast(transform.position + DirectionToTarget, DirectionToTarget, out RaycastHit raycastHit, Range))
            {
                if (raycastHit.collider.TryGetComponent(out Health targetHealth))
                {
                    explosionGun.Shoot(DirectionToTarget, demoAgent.Target.position);
                }
            }            
        }
    }
}
