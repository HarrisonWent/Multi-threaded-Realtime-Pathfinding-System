using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideRenderers : MonoBehaviour
{
    bool Hidden = true;
    private MeshRenderer[] Meshes;
    public GameObject Floor;
    private void Start()
    {
        Meshes = Floor.GetComponentsInChildren<MeshRenderer>();
    }


    public void ToggleHide()
    {
        Hidden = !Hidden;
        foreach(MeshRenderer meshRenderer in Meshes)
        {
            meshRenderer.enabled = Hidden;
        }
    }
}
