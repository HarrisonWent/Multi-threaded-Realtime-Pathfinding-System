using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleDownDestroy : MonoBehaviour
{
    public void StartShrink(float timeToShrink)
    {
        IEnumerator CarOut = CarryOut(timeToShrink);
        StartCoroutine(CarOut);
    }

    /// <summary>
    /// Shrinks the gameobjectscale and then destroys
    /// </summary>
    /// <param name="timeToShrink"></param>
    /// <returns></returns>
    private IEnumerator CarryOut(float timeToShrink)
    {
        float Timer = 0.0f;
        while (Timer < (timeToShrink - 0.1f))
        {
            Timer += Time.deltaTime;
            float size = 1 - (Timer / timeToShrink);
            transform.localScale = new Vector3(size, size, size);
            yield return 0;
        }
        Destroy(gameObject);
    }
}