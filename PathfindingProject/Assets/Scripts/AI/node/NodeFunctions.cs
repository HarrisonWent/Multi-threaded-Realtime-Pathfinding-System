using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodeFunctions : MonoBehaviour
{
    /// <summary>
    /// Checks if node has children
    /// </summary>
    /// <param name="node"></param>
    /// <returns>true if the node has children</returns>
    public static bool HasChildren(Node node)
    {
        if (node.nodeChildren != null) { return true; }
        return false;
    }

    /// <summary>
    /// Creates a child node, applies its dimensions,parent and state
    /// </summary>
    /// <param name="parent">Parent node</param>
    /// <param name="Center">Center of child node</param>
    /// <param name="Size">Size of child node</param>
    /// <returns>Child node</returns>
    private static Node CreateChild(Node parent, Vector3 Center,Vector3 Size)
    {
        Node newNode = Navigation.GetPooledNode();
        newNode.bounds.center = Center;
        newNode.bounds.size = Size;
        newNode.parent = parent;
        newNode.BonusWalkingCost = 0;

        //Set children to null just in case the new node is at the bottom of the tree
        newNode.nodeChildren = null;

        return newNode;
    }

    /// <summary>
    /// Splits this node into quadrants (child nodes)
    /// </summary>
    /// <param name="node">Node to subdivide</param>
    private static void SubDivide(Node node)
    {
        
        node.nodeChildren = Navigation.GetPooledNodeChildrenManager();

        ref Bounds bounds = ref node.bounds;
        ref NodeChildren nodeChildren = ref node.nodeChildren;

        Vector3 QuarterSize = bounds.size * 0.25f;
        Vector3 HalfSize = bounds.size * 0.5f;

        Vector3 xPlus = new Vector3(bounds.center.x + QuarterSize.x, 0, 0);
        Vector3 zPlus = new Vector3(0, 0, bounds.center.z + QuarterSize.z);
        Vector3 yPlus = new Vector3(0, bounds.center.y + QuarterSize.y, 0);
        Vector3 zMinus = new Vector3(0, 0, bounds.center.z - QuarterSize.z);
        Vector3 xMinus = new Vector3(bounds.center.x - QuarterSize.x, 0, 0);
        Vector3 yMinus = new Vector3(0, bounds.center.y - QuarterSize.y, 0);

        //Doing the bottom children first is important for detecting walkable spaces when going over the top nodes

        //Bottom layer

        nodeChildren.D1 = CreateChild(node, yMinus + xMinus + zMinus, HalfSize);
        nodeChildren.D2 = CreateChild(node, yMinus + xPlus + zMinus, HalfSize);
        nodeChildren.D3 = CreateChild(node, yMinus + xMinus + zPlus, HalfSize);
        nodeChildren.D4 = CreateChild(node, yMinus + xPlus + zPlus, HalfSize);

        //Top layer

        nodeChildren.U1 = CreateChild(node, yPlus + xMinus + zMinus, HalfSize);
        nodeChildren.U2 = CreateChild(node, yPlus + xPlus + zMinus, HalfSize);
        nodeChildren.U3 = CreateChild(node, yPlus + xMinus + zPlus, HalfSize);
        nodeChildren.U4 = CreateChild(node, yPlus + xPlus + zPlus, HalfSize);
    }

    /// <summary>
    /// Creates child nodes if has collider within it and updates them, deletes exisiting ones if no collider within
    /// </summary>
    /// <param name="node">node to update</param>
    public static void UpdateNodeState(Node node, Bounds AreaToUpdate)
    {
        //if contains bound
        if (!AreaToUpdate.Intersects(node.bounds))
        {
           return;
        }

        //if we are the minimum size calculate our state and return
        if (
            node.bounds.size.x <= Navigation.BakedAgentSize ||
            node.bounds.size.y <= Navigation.BakedAgentSize ||
            node.bounds.size.z <= Navigation.BakedAgentSize
            )
        {
            node.Surface = CalculateState(node);

            //has children destroy them
            if (node.nodeChildren != null)
            {
                RemoveChildren(node);
            }

            return;
        }

        //Check pysics, we are not min size so can do children checks
        if (GetPhyscicsObjects(node.bounds))
        {

            //if has no children
            if (node.nodeChildren == null)
            {
                SubDivide(node);
            }

            //check their state
            UpdateNodeState(node.nodeChildren.D1, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.D2, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.D3, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.D4, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.U1, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.U2, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.U3, AreaToUpdate);
            UpdateNodeState(node.nodeChildren.U4, AreaToUpdate);

            return;
        }

        //Nothing here
        else
        {
            //has children destroy them
            if (node.nodeChildren != null) 
            {
                RemoveChildren(node);
            }

            //update my state
            node.Surface = CalculateState(node);
        }
    }

    /// <summary>
    /// Checks if a collider is present within the bounds with an ObjectNavigationPropertiesObject
    /// </summary>
    /// <param name="bounds"></param>
    /// <returns></returns>
    private static bool GetPhyscicsObjects(Bounds bounds)
    {
        //we want to add the agent height to the bottom of the extents so that if there is an object just bellow (outside)
        //this node we can have walkable areas within this node
        //Collider[] hitColliders = Physics.OverlapSphere(node.bounds.center, node.bounds.extents.magnitude + Navigation.BakedAgentHeight);
        Collider[] colliders =Physics.OverlapBox(bounds.center, bounds.extents + (Vector3.one * Navigation.BakedAgentHeight));

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].TryGetComponent(out ObjectNavigationProperties objectNavigationProperties))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Boxcasts the node volume for colliders and returns the surfaceID it hits
    /// </summary>
    /// <returns>New state for node</returns>
    private static CustomNavigationSurface CalculateState(Node node)
    {
        Collider[] hitColliders = Physics.OverlapBox(node.bounds.center, node.bounds.extents);        

        //maybe chache reference to last object properties
        if (hitColliders.Length > 0)
        {
            CustomNavigationSurface foundsurface = null;
            for(int i = 0; i<hitColliders.Length;i++)
            {
                if (hitColliders[i].TryGetComponent(out ObjectNavigationProperties ONP))
                {
                    foreach (CustomNavigationSurface customNavigationSurface in Navigation.SurfaceTypes)
                    {
                        if (customNavigationSurface.HiddenID == ONP.SurfaceID)
                        {
                            //Not breakables take precedence over breakables
                            if (!customNavigationSurface.Breakable) { return customNavigationSurface; }
                            foundsurface = customNavigationSurface;                           
                        }
                    }
                }
            }

            if (foundsurface!=null) 
            {
                return foundsurface; 
            }
            
            //Object is not valid to be considered, make open or walkable
            else
            {
                Node nBellow1 = NodeBellow(node);
                if (nBellow1 != null)
                {
                    //Apply walk cost from the surface bellow
                    node.BonusWalkingCost = nBellow1.Surface.WalkingCost;
                    return Navigation.Walkable;
                }
                return null;
            }
        }

        //detect if walkable
        else 
        {
            Node nBellow2 = NodeBellow(node);
            if (nBellow2 != null)
            {
                //Apply walk cost from the surface bellow
                node.BonusWalkingCost = nBellow2.Surface.WalkingCost;
                return Navigation.Walkable;
            }         
        }

        //Connection is open
        return null;
    }

    /// <summary>
    /// Draws the node as a box on Gizmos, draws children too
    /// </summary>
    /// <param name="node">Node to draw</param>
    public static void DebugDraw(Node node)
    {
        NodeChildren nodeChildren = node.nodeChildren;

        if (nodeChildren != null)
        {
            DebugDraw(nodeChildren.D1);
            DebugDraw(nodeChildren.D2);
            DebugDraw(nodeChildren.D3);
            DebugDraw(nodeChildren.D4);
            DebugDraw(nodeChildren.U1);
            DebugDraw(nodeChildren.U2);
            DebugDraw(nodeChildren.U3);
            DebugDraw(nodeChildren.U4);
        }
        else
        {
            if (node.Surface == null)
            {
                if (Navigation.DisplayOpen)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
                }
            }
            else if(node.Surface.HiddenID == 1)
            {
                if (Navigation.Walkable.DebugDisplay)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
                }
            }
            else
            {
                if (node.Surface.DebugDisplay)
                {
                    Gizmos.color = node.Surface.DebugColour;
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
                }
            }
        }
    }   

    /// <summary>
    /// Find the node containing the given position
    /// </summary>
    /// <param name="node">Root node</param>
    /// <param name="Position">Position we want to find</param>
    /// <returns>Closest node containing</returns>
    public static Node FindPosition(Node node,Vector3 Position)
    {
        if (!node.bounds.Contains(Position))
        {            
            return null;
        }

        NodeChildren nodeChildren = node.nodeChildren;

        if (nodeChildren != null)
        {
            //Debug.Log("has childs");

            Node nodetest;
            
            nodetest = FindPosition(nodeChildren.D1, Position);
            if (nodetest != null) {  return nodetest; }
            nodetest = FindPosition(nodeChildren.D2, Position);
            if (nodetest != null) {  return nodetest; }
            nodetest = FindPosition(nodeChildren.D3, Position);
            if (nodetest != null) { return nodetest; }
            nodetest = FindPosition(nodeChildren.D4, Position);
            if (nodetest != null) {  return nodetest; }
            nodetest = FindPosition(nodeChildren.U1, Position);
            if (nodetest != null) {  return nodetest; }
            nodetest = FindPosition(nodeChildren.U2, Position);
            if (nodetest != null) {  return nodetest; }
            nodetest = FindPosition(nodeChildren.U3, Position);
            if (nodetest != null) {  return nodetest; }
            nodetest = FindPosition(nodeChildren.U4, Position);
            if (nodetest != null) {  return nodetest; }

            //Debug.Log(" is containing position, but somehow none of the childs are");
        }

        //Debug.Log("Found your position at: " + node.bounds.center);

        return node;
    }

    /// <summary>
    /// Remove a specific nodes children, recusivley child nodes
    /// </summary>
    /// <param name="node">Node whos childs we want to remove</param>
    private static void RemoveChildren(Node node)
    {
        if (node.nodeChildren != null)
        {
            //after testing it turns out that this method is slower than just garbage collecting them and spawning new nodes by about 1ms

            //RemoveToObjectPool(node.nodeChildren.D1);
            //RemoveToObjectPool(node.nodeChildren.D2);
            //RemoveToObjectPool(node.nodeChildren.D3);
            //RemoveToObjectPool(node.nodeChildren.D4);
            //RemoveToObjectPool(node.nodeChildren.U1);
            //RemoveToObjectPool(node.nodeChildren.U2);
            //RemoveToObjectPool(node.nodeChildren.U3);
            //RemoveToObjectPool(node.nodeChildren.U4);

            //Navigation.UnassigndChildManagers.Add(node.nodeChildren);
            node.nodeChildren = null;
        }
    }

    /// <summary>
    /// Remove a speicifc node, recursivley remove its children
    /// </summary>
    /// <param name="node">Node to remove</param>
    private static void RemoveToObjectPool(Node node)
    {
        if(node.nodeChildren != null)
        {
            RemoveChildren(node);
        }

        Navigation.UnassignedNodes.Add(node);
    }

    /// <summary>
    /// Gets the node bellow
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private static Node NodeBellow(Node node)
    {
        Node current = node;

        //use the extents of the nodes bounds and move out from there as node size may be greater than agent size
        Vector3 Direction = new Vector3(0, -1, 0);
        Vector3 DesiredPoint = new Vector3(
            (Direction.x * node.bounds.extents.x) + (Direction.x * (Navigation.BakedAgentSize * 0.5f)),
            (Direction.y * node.bounds.extents.y) + (Direction.y * (Navigation.BakedAgentSize * 0.5f)),
            (Direction.z * node.bounds.extents.z) + (Direction.z * (Navigation.BakedAgentSize * 0.5f)));

        DesiredPoint += node.bounds.center;

        //Debug.Log("Currently at : " + node.bounds.center + ", looking for node at: " + DesiredPoint);

        //Move up until our desired position is inside our scopeS
        while (!current.bounds.Contains(DesiredPoint))
        {
            if (current.parent == null)
            {
                //Debug.LogError("Target positistion outside of the quadtree");
                return null;
            }

            current = current.parent;
        }

        //Navigate  back down to the child containing the desired point

        //if current has children move down
        while (current.nodeChildren != null)
        {
            if (current.nodeChildren.D1.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.D1;
            }
            else if (current.nodeChildren.D2.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.D2;
            }
            else if (current.nodeChildren.D3.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.D3;
            }
            else if (current.nodeChildren.D4.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.D4;
            }
            else if (current.nodeChildren.U1.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.U1;
            }
            else if (current.nodeChildren.U2.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.U2;
            }
            else if (current.nodeChildren.U3.bounds.Contains(DesiredPoint))
            {
                current = current.nodeChildren.U3;
            }
            else
            {
                current = current.nodeChildren.U4;
            }
        }

        if(current == null) { return null; }
        else if(current.Surface == null) { return null; }
        else if(current.Surface.HiddenID == 1) { return null; }

        return current;
    }    
}
