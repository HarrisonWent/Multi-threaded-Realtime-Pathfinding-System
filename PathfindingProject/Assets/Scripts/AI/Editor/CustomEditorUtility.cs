using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

class CustomEditorUtility : EditorWindow
{
    [MenuItem("Window/EDITOR UTILITY TOOLS")]

    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(CustomEditorUtility));
    }

    void OnGUI()
    {
        GUILayout.Label("Set static", EditorStyles.boldLabel);
        if (GUILayout.Button("Set selected to static and apply to prefab"))
        {
            FixStatics();
        }    

        GUILayout.Label("Object Index Mover", EditorStyles.boldLabel);
        if (GUILayout.Button("Move selected objects to top"))
        {
            MoveSelected();
        }

        GUILayout.Label("Object Replacer", EditorStyles.boldLabel);
        ObjectReplacerName = EditorGUILayout.TextField("Object name", ObjectReplacerName);
        PrefabToReplaceWith = EditorGUILayout.ObjectField("Prefab to replace with:", PrefabToReplaceWith, typeof(GameObject), true);
        if (GUILayout.Button("Replace objects containing name"))
        {
            ReplaceNamed();
        }
    }

    private string ObjectReplacerName = "";
    private Object PrefabToReplaceWith;

    private void FixStatics()
    {
        foreach (GameObject o in Selection.gameObjects)
        {
            if (o.isStatic) { continue; }
            o.isStatic = true;

            PrefabUtility.ApplyPrefabInstance(o, InteractionMode.UserAction);
        }
    }

    private void MoveSelected()
    {
        foreach (GameObject o in Selection.gameObjects)
        {
            o.transform.SetAsFirstSibling();
        }
    }

    private void ReplaceNamed()
    {
        GameObject[] sceneobjects = FindAll();
        List<GameObject> GetRid = new List<GameObject>();

        foreach (GameObject g in sceneobjects)
        {
            if (g.name.Contains(ObjectReplacerName))
            {
                GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(PrefabToReplaceWith);
                replacement.transform.position = g.transform.position;
                replacement.transform.rotation = g.transform.rotation;
                GetRid.Add(g);
                replacement.transform.SetAsFirstSibling();
            }
        }

        while (GetRid.Count > 0)
        {
            DestroyImmediate(GetRid[0]);
            GetRid.RemoveAt(0);
        }
    }

    private GameObject[] FindAll()
    {
        Object[] tempList = Resources.FindObjectsOfTypeAll(typeof(GameObject));
        List<GameObject> realList = new List<GameObject>();
        GameObject temp;

        foreach (Object obj in tempList)
        {
            if (obj is GameObject)
            {
                temp = (GameObject)obj;
                if (temp.hideFlags == HideFlags.None)
                    realList.Add((GameObject)obj);
            }
        }
        return realList.ToArray();
    }
}
