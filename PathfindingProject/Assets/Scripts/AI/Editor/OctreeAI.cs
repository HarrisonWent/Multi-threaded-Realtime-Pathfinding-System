using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OctreeAI : EditorWindow
{
    [MenuItem("Window/AI/Octree AI Tools")]

    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(OctreeAI));
    }

    void OnGUI()
    {
        if (navi == null)
        {
            navi = FindObjectOfType<Navigation>();
        }

        WindowScrollPosition = GUILayout.BeginScrollView(WindowScrollPosition, GUIStyle.none);
                
        GUILayout.Space(15);        

        if (navi == null)
        {
            DrawNoNavigationOptions();
            GUILayout.EndScrollView();
            return;
        }

        DrawWithNavigationOptions();

        GUILayout.Space(50);

        DrawObjectEditingSettings();

        GUILayout.Space(50);

        DrawSurfaceSettings();

        GUILayout.EndScrollView();
    }

    void DrawWithNavigationOptions()
    {
        GUILayout.Label("Scene navigation Settings", EditorStyles.boldLabel);

        //GUILayout.Label("Note: Debug views only update in play mode", EditorStyles.helpBox);
        Navigation.DisplayOpen = EditorGUILayout.Toggle("Debug display open area", Navigation.DisplayOpen);

        if (Navigation.Walkable != null)
        {
            Navigation.Walkable.DebugDisplay = EditorGUILayout.Toggle("Debug display walkable area", Navigation.Walkable.DebugDisplay);
        }

        if (navi.checkHasNavigation())
        {
            GUILayout.Label("READY\nState: Navigation present in scene", EditorStyles.helpBox);
        }
        else
        {
            GUILayout.Label("READY\nState:  No navigation present in scene, Reason: " + navi.NavDebugMessage(), EditorStyles.helpBox);
        }
        GUILayout.Space(15);

        SceneSettings sceneSettings = FindObjectOfType<SceneSettings>();
        GUILayout.Label("Note: these values have performance implications", EditorStyles.helpBox);

        float curSize = sceneSettings.AgentSize, curHeight = sceneSettings.AgentHeight;
        Vector3 curDimen = sceneSettings.bakeDimensions;

        sceneSettings.AgentSize = EditorGUILayout.FloatField("Agent radius (1 default)", sceneSettings.AgentSize);
        sceneSettings.AgentHeight = EditorGUILayout.FloatField("Agent height (2 default)", sceneSettings.AgentHeight);
        sceneSettings.bakeDimensions = EditorGUILayout.Vector3Field("World Dimensions", sceneSettings.bakeDimensions);

        if (curSize != sceneSettings.AgentSize || curHeight != sceneSettings.AgentHeight || curDimen != sceneSettings.bakeDimensions)
        {
            EditorUtility.SetDirty(sceneSettings);
        }

        if (GUILayout.Button("Remove navigation"))
        {
            DestroyImmediate(navi.gameObject);
        }
    }

    void DrawNoNavigationOptions()
    {
        GUILayout.Label("State:  No navigation baked in scene, no navigation object found", EditorStyles.helpBox);

        if (GUILayout.Button("Add navigation"))
        {
            GameObject g = new GameObject("NavigationData");
            g.AddComponent<SceneSettings>();
            g.AddComponent<Navigation>();
            g.AddComponent<Pathfinding>();
        }
    }

    int _selected = 0;
    void DrawObjectEditingSettings()
    {

        GUILayout.Label("Object surface editor:", EditorStyles.boldLabel);
        
        List<string> SurfaceOptions = new List<string>();
        foreach (CustomNavigationSurface CNS in Navigation.SurfaceTypes)
        {
            SurfaceOptions.Add(CNS.SurfaceName);
        }

        string[] _options = SurfaceOptions.ToArray();

        GUILayout.Label("Surface to apply to selected objects:", EditorStyles.helpBox);
        _selected = EditorGUILayout.Popup("", _selected, _options);

        if (GUILayout.Button("Assign objects"))
        {
            foreach(GameObject g in Selection.gameObjects)
            {
                if(g.TryGetComponent(out Collider collider))
                {
                    if(g.TryGetComponent(out ObjectNavigationProperties objectNavigationProperties))
                    {                        
                        objectNavigationProperties.SurfaceID = Navigation.SurfaceTypes[_selected].HiddenID;
                        objectNavigationProperties.SurfaceCollider = collider;
                    }
                    else
                    {
                        objectNavigationProperties = g.AddComponent<ObjectNavigationProperties>();
                        objectNavigationProperties.SurfaceID = Navigation.SurfaceTypes[_selected].HiddenID;
                        objectNavigationProperties.SurfaceCollider = collider;
                    }
                    EditorUtility.SetDirty(objectNavigationProperties);
                    Debug.Log(g.name + " applied surface type: " + Navigation.SurfaceTypes[_selected].SurfaceName);
                }
                else
                {
                    Debug.Log(g.name + " does not have an attached collider, skipping...");
                }
            }
        }
    }

    void DrawSurfaceSettings()
    {
        GUILayout.Label("Custom surface settings:", EditorStyles.boldLabel);
        
        List<CustomNavigationSurface> ToDelete = new List<CustomNavigationSurface>();

        SurfaceScrollPosition = GUILayout.BeginScrollView(SurfaceScrollPosition, GUIStyle.none);
        for (int i = 0; i < Navigation.SurfaceTypes.Count; i++)
        {
            NavSurfaceUIInfo NSUI = Navigation.SelectedSurfaces[i];
            NSUI.state = EditorGUILayout.BeginFoldoutHeaderGroup(Navigation.SelectedSurfaces[i].state, Navigation.SurfaceTypes[i].SurfaceName);

            CustomNavigationSurface CNS = Navigation.SurfaceTypes[i];

            if (NSUI.state)
            {
                CNS.SurfaceName = EditorGUILayout.TextField("Surface Name: ", CNS.SurfaceName);

                //break and debug display toggle
                CNS.Breakable = EditorGUILayout.Toggle("Breakable", CNS.Breakable);
                if (CNS.Breakable)
                {
                    CNS.BreakCost = EditorGUILayout.IntSlider("Break cost", CNS.BreakCost, 0, 20);
                }

                CNS.WalkingCost = EditorGUILayout.IntSlider("Walk cost to travel on top", CNS.WalkingCost, 0, 20);

                CNS.DebugDisplay = EditorGUILayout.Toggle("Debug display", CNS.DebugDisplay);

                CNS.DebugColour = EditorGUILayout.ColorField("Debug colour", CNS.DebugColour);

                if (GUILayout.Button("Delete surface"))
                {
                    Debug.Log("Del");
                    ToDelete.Add(Navigation.SurfaceTypes[i]);
                }

                GUILayout.Space(15);
            }

            Navigation.SelectedSurfaces[i] = NSUI;
            Navigation.SurfaceTypes[i] = CNS;            

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        GUILayout.EndScrollView();

        foreach (CustomNavigationSurface CNS in ToDelete)
        {
            int ID = CNS.HiddenID;
            for (int i = 0; i < Navigation.SurfaceTypes.Count; i++)
            {
                if (Navigation.SurfaceTypes[i].HiddenID == ID)
                {
                    Navigation.SurfaceTypes.RemoveAt(i);
                    Navigation.SelectedSurfaces.RemoveAt(i);
                    break;
                }
            }

            ObjectNavigationProperties[] objectNavigationProperties = FindObjectsOfType<ObjectNavigationProperties>();
            bool FoundWithSameID = false;
            for(int i = 0; i< objectNavigationProperties.Length;i++)
            {
                if(objectNavigationProperties[i].SurfaceID == ID)
                {
                    FoundWithSameID = true;
                    objectNavigationProperties[i].SurfaceID = 0;
                }
            }

            if(FoundWithSameID)
            {
                Debug.LogWarning("Found objects in scene with deleted surface");
            }
        }


        if (GUILayout.Button("Add default surfaces"))
        {
            AddDefaultSurfaces();
        }

        if (GUILayout.Button("Add new surface type"))
        {
            AddNewSurface();
        }

        if (GUILayout.Button("Save surfaces"))
        {
            NavSettingsIO.SaveSurfaceSettings();
        }
    }    

    /// <summary>
    /// Adds the default surfaces (Floor, wall and breakable)
    /// </summary>
    private void AddDefaultSurfaces()
    {
        //WALL
        CustomNavigationSurface CNS = new CustomNavigationSurface();
        CNS.HiddenID = GetNewUniqueID();
        CNS.SurfaceName = "Wall";
        CNS.DebugColour = Color.blue;
        CNS.Breakable = false;
        CNS.BreakCost = 2;
        CNS.WalkingCost = 1;

        NavSurfaceUIInfo NSUI = new NavSurfaceUIInfo();
        NSUI.state = false;
        NSUI.HiddenID = CNS.HiddenID;

        Navigation.SurfaceTypes.Add(CNS);
        Navigation.SelectedSurfaces.Add(NSUI);

        //FLOOR
        CNS = new CustomNavigationSurface();
        CNS.HiddenID = GetNewUniqueID();
        CNS.SurfaceName = "Floor";
        CNS.DebugColour = Color.yellow;
        CNS.Breakable = false;
        CNS.BreakCost = 2;

        NSUI = new NavSurfaceUIInfo();
        NSUI.state = false;
        NSUI.HiddenID = CNS.HiddenID;

        Navigation.SurfaceTypes.Add(CNS);
        Navigation.SelectedSurfaces.Add(NSUI);

        //BREAKABLE
        CNS = new CustomNavigationSurface();
        CNS.HiddenID = GetNewUniqueID();
        CNS.SurfaceName = "Breakable";
        CNS.DebugColour = Color.yellow;
        CNS.Breakable = true;
        CNS.BreakCost = 2;

        NSUI = new NavSurfaceUIInfo();
        NSUI.state = false;
        NSUI.HiddenID = CNS.HiddenID;

        Navigation.SurfaceTypes.Add(CNS);
        Navigation.SelectedSurfaces.Add(NSUI);
    }

    /// <summary>
    /// Creates a new surface
    /// </summary>
    private void AddNewSurface()
    {
        CustomNavigationSurface CNS = new CustomNavigationSurface();
        CNS.HiddenID = GetNewUniqueID();
        CNS.SurfaceName = "New navigation surface";

        NavSurfaceUIInfo NSUI = new NavSurfaceUIInfo();
        NSUI.state = false;
        NSUI.HiddenID = CNS.HiddenID;

        Navigation.SurfaceTypes.Add(CNS);
        Navigation.SelectedSurfaces.Add(NSUI);
    }


    /// <summary>
    /// Gets a unique surface ID which isn't currenntly used on any surface
    /// </summary>
    /// <returns></returns>
    private byte GetNewUniqueID()
    {
        List<int> UsedIDS = new List<int>();

        //Open and walkable
        UsedIDS.Add(0);
        UsedIDS.Add(1);

        foreach(CustomNavigationSurface CNS in Navigation.SurfaceTypes) { UsedIDS.Add(CNS.HiddenID); }

        bool FoundID = false;
        byte Counter = 0;
        while(!FoundID)
        {
            if (UsedIDS.Contains(Counter))
            {
                Counter++;
            }
            else
            {
                break;
            }
        }
        return Counter;
    }

    Vector2 WindowScrollPosition,SurfaceScrollPosition;

    private Navigation navi;
}
