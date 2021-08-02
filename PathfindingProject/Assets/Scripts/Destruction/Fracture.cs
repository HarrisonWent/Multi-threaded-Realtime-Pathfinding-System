using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class Fracture : MonoBehaviour
{
    [Header("Objects")]
    public Transform CellParent;
    public Transform OriginalMesh;
    public Transform ObjectsParentToCleanup;
    public Transform DestroyedMeshVariant;
    public GameObject[] ObjectsToKeep;

    [Header("Explosion")]
    public int ExplosionRadius = 50;
    public ParticleSystem FX;
    public Object TrailFX;
    public AudioSource Sound;

    bool Doing = false;

    public bool DetectCells = false;
    public Rigidbody[] FoundRigids;
    public ScaleDownDestroy[] FoundScaleDowns;

#if UNITY_EDITOR
    private void Update()
    {
        //Automatically detects and sets up objects that are children of the cell parent with rigidbodies,
        //colliders and the tag and layer

        if (DetectCells)
        {
            foreach (Transform t in Cells())
            {
                if (!t.GetComponent<MeshFilter>()) { continue; }
                t.gameObject.SetActive(true);

                //Sort Collider
                MeshCollider MC = t.gameObject.GetComponent<MeshCollider>();
                if (!MC)
                {
                    MC = t.gameObject.AddComponent<MeshCollider>();
                }

                MC.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
                MC.convex = true;

                //Sort Rigidbody
                Rigidbody RB = t.gameObject.GetComponent<Rigidbody>();
                if (!RB)
                {
                    RB = t.gameObject.AddComponent<Rigidbody>();
                }

                RB.isKinematic = true;
                RB.Sleep();

                if(!t.GetComponentInChildren<ParticleSystem>())
                {
                    Object trail = PrefabUtility.InstantiatePrefab(TrailFX, t);
                    GameObject g = trail as GameObject;
                    g.transform.localPosition = Vector3.zero;
                }

                if(!t.TryGetComponent(out ScaleDownDestroy scaleDownDestroy))
                {
                    t.gameObject.AddComponent<ScaleDownDestroy>();
                }
            }

            FoundRigids = Rigids();

            FoundScaleDowns = ScaleDownDestroys();

            CellParent.gameObject.SetActive(false);

            DetectCells = false;

            Debug.Log("Cell setup for: " + gameObject.name + ". Completed Successfully");
        }
    }
#endif

    public void DoDestruct(Vector3 Origin, float Power)
    {
        if (Doing) { return; }
        Doing = true;

        StartCoroutine(DestructWork(Origin, Power));
    }

    /// <summary>
    /// Plays the destruction sequence
    /// </summary>
    /// <param name="Origin"></param>
    /// <param name="Power"></param>
    /// <returns></returns>
    private IEnumerator DestructWork(Vector3 Origin, float Power)
    {
        Power = Mathf.Clamp(Power, 0, 15);

        foreach (GameObject g in ObjectsToKeep)
        {
            g.transform.SetParent(null);
        }

        Destroy(OriginalMesh.gameObject);        

        foreach (Rigidbody r in FoundRigids)
        {
            r.transform.SetParent(null);
        }

        if (FX)
        {
            FX.transform.SetParent(null);
            FX.gameObject.SetActive(true);
            FX.Play();
        }

        if (Sound)
        {
            Sound.transform.SetParent(null);
            Sound.gameObject.SetActive(true);
            Sound.Play();
        }

        if (DestroyedMeshVariant)
        {
            DestroyedMeshVariant.SetParent(null);
            DestroyedMeshVariant.gameObject.SetActive(true);
        }

        yield return 0;

        if (CellParent)
        {
            foreach (Rigidbody r in FoundRigids)
            {
                r.isKinematic = false;
                //r.WakeUp();
                r.AddExplosionForce(Power, Origin, ExplosionRadius, 1f, ForceMode.Impulse);
            }
            foreach(ScaleDownDestroy scaleDownDestroy in FoundScaleDowns)
            {
                scaleDownDestroy.StartShrink(Random.Range(3f, 7f));
            }
        }

        if (ObjectsParentToCleanup)
        {
            Destroy(ObjectsParentToCleanup.gameObject);
        }
    }

//Used in editor to cache the objects manually
    private Rigidbody[] Rigids()
    {
        if (!CellParent) { return null; }
        CellParent.gameObject.SetActive(true);
        Rigidbody[] Childs = CellParent.GetComponentsInChildren<Rigidbody>();
        return Childs;
    }

    private Transform[] Cells()
    {
        CellParent.gameObject.SetActive(true);
        Transform[] Childs = CellParent.GetComponentsInChildren<Transform>();
        return Childs;
    }

    private ScaleDownDestroy[] ScaleDownDestroys()
    {
        return CellParent.GetComponentsInChildren<ScaleDownDestroy>();
    }
}
