using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor;

/// <summary>
/// Used to keep track of dynamic objects in the scene, comparing the cached bounds to colliders current bounds will identify if it needs to be updated
/// </summary>
public class CachedCollider
{
    private Collider Cache_collider;
    public Bounds Cache_bounds;
    public void SetCollider(Collider collider) { Cache_collider = collider; }
    public Collider GetCollider() { return Cache_collider; }
    public void SetBounds(Vector3 Size, Vector3 Position)
    {
        if (Cache_bounds == null) { Cache_bounds = new Bounds(); }
        Cache_bounds.center = Position;
        Cache_bounds.size = Size;
    }
}

/// <summary>
/// Defines surface properties for a dynamic object
/// </summary>
public class CustomNavigationSurface
{
    public string SurfaceName;
    public byte HiddenID;
    public int WalkingCost = 0;
    public bool Breakable;
    public int BreakCost;

    public bool DebugDisplay;
    public Color DebugColour;
}

/// <summary>
/// Used for selecting surface dropdowns in the navigation baker editor window
/// </summary>
[System.Serializable]
public struct NavSurfaceUIInfo
{
    public int HiddenID; //corresponds to the surface ID
    public bool state;
}

[ExecuteAlways]
public class Navigation : MonoBehaviour
{
    //Quad tree
    public Node SceneNode;//x,y,z

    //Pooled objects for use in quadtree
    public static List<Node> UnassignedNodes = new List<Node>();
    public static List<NodeChildren> UnassigndChildManagers = new List<NodeChildren>();

    //Tracked colliders for updating the quadtree
    Queue<CachedCollider> CachedColliders = new Queue<CachedCollider>();

    //The most recent quadtree variables
    public static float BakedAgentSize = 1f, BakedAgentHeight = 2f;
    public static Vector3 bakedDimensions = Vector3.zero;    

    //The current scenes settings (dimensions etc)
    SceneSettings sceneSettings;

    //Used for the dropdown state of surfaces in the editor window
    public static List<NavSurfaceUIInfo> SelectedSurfaces = new List<NavSurfaceUIInfo>();
    //Project wide surface types for objects
    public static List<CustomNavigationSurface> SurfaceTypes = new List<CustomNavigationSurface>();

    //Project hidden surface for walkable areas (agents which cant fly will use this)
    public static CustomNavigationSurface Walkable;

    //Displays the quadtree nodes on gizmos (the empty nodes)
    public static bool DisplayOpen = false;

    //Creates the built in walkable surface
    public static void SetWalkable()
    {
        if(Walkable != null) { return; }
        Walkable = new CustomNavigationSurface();
        Walkable.HiddenID = 1;
        Walkable.DebugDisplay = false;
        Walkable.SurfaceName = "Walkable area";
        Walkable.Breakable = false;
    }

    //Start up clear and create quadtree
    private void Start()
    {
#if UNITY_EDITOR
        EditorUpdating = false;
#endif
        UnassignedNodes.Clear();
        UnassigndChildManagers.Clear();
        CheckNavigationForBake();
    }

    //Used in editor mode when script is changed to update the quadtree for debug view
    private void OnValidate()
    {
#if UNITY_EDITOR
        EditorUpdating = false;
#endif
        CheckLoadSurfaces();
        CheckNavigationForBake();
    }

    /// <summary>
    /// Loads the project custom surfaces
    /// </summary>
    private void CheckLoadSurfaces()
    {
        NavSettingsIO.LoadSurfaces();
    }

    /// <summary>
    /// Used in editor scene mode to update for debug view
    /// </summary>

#if UNITY_EDITOR
    bool EditorUpdating = false;
    private void Update()
    {
        if (!Application.isPlaying && !EditorUpdating)
        {
            CheckNavigationForBake();
            StartCoroutine("DetectWorldChanges");
        }
    }

#endif

    /// <summary>
    /// Takes the loaded surfaces and puts them in navigation lists
    /// </summary>
    /// <param name="customNavigationSurfaces"></param>
    public static void SetSurfaces(List<CustomNavigationSurface> customNavigationSurfaces)
    {
        List<NavSurfaceUIInfo> navSurfaceUIInfos = new List<NavSurfaceUIInfo>();

        foreach (CustomNavigationSurface customNavigationSurface in customNavigationSurfaces)
        {
            NavSurfaceUIInfo NSUI = new NavSurfaceUIInfo();
            NSUI.state = false;
            NSUI.HiddenID = customNavigationSurface.HiddenID;
            navSurfaceUIInfos.Add(NSUI);
        }

        SelectedSurfaces = navSurfaceUIInfos;
        SurfaceTypes = customNavigationSurfaces;
    }

    public static Node GetPooledNode()
    {
        if(UnassignedNodes.Count>0)
        {
            Node node = UnassignedNodes[0];
            UnassignedNodes.RemoveAt(0);
            return node;
        }
        else
        {
            Node node = new Node();
            node.bounds = new Bounds();
            return new Node();
        }
    }

    public static NodeChildren GetPooledNodeChildrenManager()
    {
        if(UnassigndChildManagers.Count>0)
        {
            NodeChildren node = UnassigndChildManagers[0];
            UnassigndChildManagers.RemoveAt(0);
            return node;
        }
        else
        {
            return new NodeChildren();
        }
    }

    public bool checkHasNavigation()
    {
        if(SceneNode == null) { return false; }
        return true;
    }    

    /// <summary>
    /// Checks if a quad tree is needed to be created (start of level or scene settings have changed)
    /// </summary>
    /// <returns></returns>
    public bool CheckNavigationForBake()
    {

        if (!sceneSettings)
        {
            sceneSettings = GetComponent<SceneSettings>();
        }

        if (SceneNode == null || bakedDimensions != sceneSettings.bakeDimensions || BakedAgentSize != sceneSettings.AgentSize)
        {
            //If new bake sizes valid then bake
            if (sceneSettings.AgentSize > 0.1f && sceneSettings.bakeDimensions.magnitude > 0f)
            {
                BakeNavigation(sceneSettings.bakeDimensions, sceneSettings.AgentSize,sceneSettings.AgentHeight);
            }
            else
            {
                //If incorrect bake sizes and the new ones are invalid then delete
                if (SceneNode==null)
                {
                    ClearNavigation();
                }                
                return false;
            }
        }
        return true;
    }

    public string NavDebugMessage()
    {
        if (!sceneSettings)
        {
            sceneSettings = GetComponent<SceneSettings>();
        }

        string Message = "";
        if (sceneSettings.AgentSize < 0.1f )
        {
            Message += "Agent size is too small (Must be above 0.1).";
        }
        if (sceneSettings.bakeDimensions.magnitude <= 0f)
        {
            if (Message.Length > 0)
            {
                Message += " ";
            }
            Message += "Bake dimensions are too small or negative (Must be above 0).";
        }
        return Message;
    }

    public void ClearNavigation()
    {
        SceneNode = null;
        System.GC.Collect();        
    }

    public void BakeNavigation(Vector3 AreaDimensions,float AgentRadius, float AgentHeight)
    {
        ClearNavigation();

        //Spawn nodes
        AddSceneColliders();

        //Add default walkable surface type
        SetWalkable();
        CheckLoadSurfaces();

        //Put nodes into quad tree
        CreateNodes(AreaDimensions,AgentRadius,AgentHeight);        

        //Debug.LogWarning("Nav Baked:");
    }

    /// <summary>
    /// Creates the quadtree itself
    /// </summary>
    /// <param name="AreaDimensions"></param>
    /// <param name="agentRadius"></param>
    /// <param name="agentHeight"></param>
    void CreateNodes(Vector3 AreaDimensions, float agentRadius,float agentHeight)
    {
        if (AreaDimensions.y <= 0 || AreaDimensions.x <= 0 || AreaDimensions.z <= 0) {  return; }//Debug.LogError("Invalid area dimensions");

        BakedAgentSize = agentRadius;
        bakedDimensions = AreaDimensions;
        BakedAgentHeight = agentHeight;

        SceneNode = new Node();

        Bounds bounds = new Bounds();
        bounds.center = Vector3.zero;
        bounds.size = AreaDimensions;

        SceneNode.bounds = bounds;
        NodeFunctions.UpdateNodeState(SceneNode, bounds);

        Debug.Log("Nodes created");        
    }

    bool FirstTime = true;
    /// <summary>
    /// Adds new colliders to the cached colliders
    /// </summary>
    /// <returns>New colldiers</returns>
    private void AddSceneColliders()
    {
        ObjectNavigationProperties[] SceneSurfaceObjects = FindObjectsOfType<ObjectNavigationProperties>();
        for (int i = 0; i < SceneSurfaceObjects.Length; i++)
        {
            if(SceneSurfaceObjects[i].SurfaceCollider == null) 
            { 
                Debug.LogError(SceneSurfaceObjects[i].name + " does not have an attached collider");
                continue;
            }

            if (!CacheContains(SceneSurfaceObjects[i].SurfaceCollider))
            {
                CachedCollider cachedCollider = new CachedCollider();
                cachedCollider.SetCollider(SceneSurfaceObjects[i].SurfaceCollider);

                //We dont want to do updates for objects here at the level start since the nav has just been baked anyway
                if (!FirstTime)
                {
                    cachedCollider.SetBounds(Vector3.zero, SceneSurfaceObjects[i].SurfaceCollider.bounds.center);
                }
                else
                {
                    cachedCollider.SetBounds(SceneSurfaceObjects[i].SurfaceCollider.bounds.size, SceneSurfaceObjects[i].SurfaceCollider.bounds.center);
                }

                CachedColliders.Enqueue(cachedCollider);
            }
        }
        FirstTime = false;
    }

    bool CacheContains(Collider collider)
    {
        for (int i = 0; i < CachedColliders.Count; i++)
        {
            if (CachedColliders.ElementAt(i).GetCollider() == collider)
            {
                return true;
            }
        }
        return false;
    }

    private float LastRun;
    /// <summary>
    /// Checks if cached bounds match colliders current bounds (updates the tree in that area if not)
    /// </summary>
    /// <returns></returns>
    public IEnumerator DetectWorldChanges()
    {
#if UNITY_EDITOR
        EditorUpdating = true;
#endif
        yield return 0;
        //Debug.Log("World update last run: "+ (Time.realtimeSinceStartup-LastRun) + " seconds ago");
        //var stopwatch = new System.Diagnostics.Stopwatch();
        //stopwatch.Start();

        AddSceneColliders();

        //define max updates
        int UpdatesDone = 0, MaxUpdates = 4;

        Collider collider;

        //for cached colliders
        for (int i = 0; i<CachedColliders.Count; i++)
        {
            //If taken more than 4 ms in one frame then yield for the next
            //if (stopwatch.ElapsedMilliseconds >4) 
            //{
            //    yield return 0;                
            //}

            //end if max updates reached
            if (UpdatesDone == MaxUpdates) { break; }

            //take cached collider from the queue
            CachedCollider cachedCollider = CachedColliders.Dequeue();

           
            collider = cachedCollider.GetCollider();

            //if collider is null
            if (collider == null)
            {
                //update the cached bounds area
                UpdatePair(cachedCollider);

                //+1 update
                UpdatesDone++;

                //now one less in the cached colldier list
                i--;

                //The object has been destroyed, dont need to add back into queue
                //continue to the next
                continue;
            }

            //if collider bounds dont match cached bounds
            //The object exists but has changed in some way
            if (!CompareBounds(collider.bounds, cachedCollider.Cache_bounds))
            {                
                UpdatePair(cachedCollider);
                //+1 update
                UpdatesDone++;
            }

            //add the cached collider to the back of the queue
            CachedColliders.Enqueue(cachedCollider);
        }

        //LastRun = Time.realtimeSinceStartup;
        //Debug.Log("World update took: " + stopwatch.ElapsedMilliseconds);
        //stopwatch.Stop();

        if (Application.isPlaying)
        {
            FindObjectOfType<Pathfinding>().WorldUpdated();
        }

#if UNITY_EDITOR
        EditorUpdating = false;
#endif
    }

    private bool CompareBounds(Bounds A, Bounds B)
    {
        if (FastRoundToAgentSize(A.center) == FastRoundToAgentSize(B.center) && 
            FastRoundToAgentSize(A.size) == FastRoundToAgentSize(B.size))
        {
            return true;
        }
        return false;
    }

    public static Node WorldPositionToNode(Vector3 WorldPosition,Node root)
    {
        if(root == null) { Debug.LogError("Root is null"); }
        Node node = NodeFunctions.FindPosition(root, WorldPosition);
        //Debug.Log("Hmm: " + node.bounds.center);
        
        return node;
    }

    private Vector3 FastRoundToAgentSize(Vector3 WorldPosition)
    {
        Vector3 Rounded = new Vector3(
            RoundTo(WorldPosition.x, BakedAgentSize),
            RoundTo(WorldPosition.y, BakedAgentSize),
            RoundTo(WorldPosition.z, BakedAgentSize));

        return Rounded;
    }

    public void UpdateNodeArea(Bounds bounds)
    {
        //find the nodes which intersect with these bounds and update them
        Bounds WalkableAdjustedBounds = new Bounds();
        WalkableAdjustedBounds.center = bounds.center;
        WalkableAdjustedBounds.size = bounds.size +(Vector3.one * BakedAgentSize) +(Vector3.one * BakedAgentHeight * 2);
        NodeFunctions.UpdateNodeState(SceneNode, WalkableAdjustedBounds);
    }

    /// <summary>
    /// Updates where the collider was and where it is now, if they intersect it will just update the encapsulated area
    /// </summary>
    /// <param name="cachedCollider"></param>
    public void UpdatePair(CachedCollider cachedCollider)
    {
        Collider collider = cachedCollider.GetCollider();

        if(collider == null)
        {
            UpdateNodeArea(cachedCollider.Cache_bounds);
            return;
        }        
        
        //if intersect (moved)
        if (collider.bounds.Intersects(cachedCollider.Cache_bounds))
        {
            //update the encapsulating area

            //Avoid re-checking lots of nodes if it has only moved a small amount (intersects the old position)
            Bounds EncapsulatedBounds = new Bounds();
            EncapsulatedBounds.center = collider.bounds.center;
            EncapsulatedBounds.size = collider.bounds.size;
            
            EncapsulatedBounds.Encapsulate(cachedCollider.Cache_bounds);

            UpdateNodeArea(EncapsulatedBounds);

        }
        //else update both areas (teleported)
        else
        {
            //Update the area the object has moved to
            UpdateNodeArea(collider.bounds);

            //Update the area the object has moved from
            UpdateNodeArea(cachedCollider.Cache_bounds);           
        }

        cachedCollider.SetBounds(collider.bounds.size,collider.bounds.center);
    }

    static float RoundTo(float value, float multipleOf)
    {
        return Mathf.Round(value / multipleOf) * multipleOf;
    }

#if UNITY_EDITOR
    /// <summary>
    /// This is VERY performance intensive, shows the nodes and colour codes their state (only used in editor with gizmos enabled)
    /// </summary>
    private void OnDrawGizmos()
    {
        //Shows what surface is applied to the currently selected game object (text over selection in scene view)
        if (Selection.activeGameObject)
        {
            string message = "";
            if (Selection.activeGameObject.TryGetComponent(out ObjectNavigationProperties objectNavigationProperties))
            {
                foreach (CustomNavigationSurface customNavigationSurface in SurfaceTypes)
                {
                    if (customNavigationSurface.HiddenID == objectNavigationProperties.SurfaceID)
                    {
                        message = "Surface: " + customNavigationSurface.SurfaceName;
                        break;
                    }
                }
            }
            else
            {
                message = "No surface";
            }
            Handles.Label(Selection.activeGameObject.transform.position, message);
        }        

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, bakedDimensions);

        if (SceneNode == null) { return; }

        //Skip this process if no surface type has its debug display enabled
        bool activeDebug = false;

        foreach (CustomNavigationSurface CNS in SurfaceTypes)
        {
            if (CNS.DebugDisplay)
            {
                activeDebug = true;
                break;
            }
        }

        if (!activeDebug && !DisplayOpen && !Walkable.DebugDisplay)
        { return; }

        NodeFunctions.DebugDraw(SceneNode);        
    }
#endif
}
