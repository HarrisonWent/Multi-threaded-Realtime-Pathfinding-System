using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

//An action to take along a given path
public class PathAction
{
    public bool NeedsDestroy;
    public Vector3 Position;
}

//An instruction for a specific path to be created
public class PathInstruction
{
    public Action<List<PathAction>> FinishAction;
    public PathAgent pathAgent;//Can't be used on seperate thread
    public Vector3 From, To;
    public Node RootNode;//The quadtree root itself
    public bool agentCanFly;
}

//A node used in the search for a path
public class PathFindingNode
{
    public Node node;
    public float G, H,F;//Costs
    public PathFindingNode CameFrom;
}

public class Pathfinding : MonoBehaviour
{
    public enum TreeState
    {
        Init,
        Updating,
        AvailableForReading,
        Reading
    }

    //Determines is the octree is availble for reading for pathfinding
    public static TreeState treeState = TreeState.Init;

    List<Thread> PathFindingThreads = new List<Thread>();
    private Queue<PathInstruction> QueuedPathInstructions = new Queue<PathInstruction>();
    private Queue<Action> QueuedPathResults = new Queue<Action>();
    private Queue<PathInstruction> QueuedForNextUpdate = new Queue<PathInstruction>();

    private void Start()
    {
        treeState = TreeState.Init;
        MaxPathsDonePerUpdate = SystemInfo.processorCount;
        Debug.Log("Setting max pathfinding threads to the processor count of this system: " + SystemInfo.processorCount);
    }

    int PathsDoneSinceLastUpdate = 0, MaxPathsDonePerUpdate;
    private void Update()
    {
        if (FindObjectOfType<Navigation>().SceneNode == null)
        {
            return;
        }

        //Hand out any pending results of the pathfinding threads
        while(QueuedPathResults.Count>0)
        {
            Action action = QueuedPathResults.Dequeue();
            action();
        }

        CleanupThreads();

        if(treeState == TreeState.Updating) { return; }

        //Debug.Log("Queued paths waiting for thread : " + QueuedPathInstructions.Count);
        //Debug.Log("Queued paths waiting for next world update: " + QueuedForNextUpdate.Count);

        //If the Quadtree is available and has pending paths to calculate then start a thread
        if (QueuedPathInstructions.Count > 0 && PathsDoneSinceLastUpdate < MaxPathsDonePerUpdate)
        {
            FindAvailableThread();
        }

        //If no pathfinding is being done then update the quadtree
        if(PathFindingThreads.Count == 0)//(
        {
            treeState = TreeState.Updating;

            //Debug.LogWarning("Do a quadtree update");
            
            StartCoroutine(FindObjectOfType<Navigation>().DetectWorldChanges());
        }
    }

    /// <summary>
    /// A* pathfinding called from a new thread
    /// </summary>
    /// <param name="pathInstruction"></param>
    void PathFindTask(PathInstruction pathInstruction)
    {
        //var stopwatch = new System.Diagnostics.Stopwatch();
        //stopwatch.Start();

        //Debug.Log("Started path finding for instruction: From: " + pathInstruction.From + ", To: " + pathInstruction.To);

        Node RootNode = pathInstruction.RootNode;

        //Open node list is for neighbours we haven't yet visited
        List<PathFindingNode> Open = new List<PathFindingNode>();
        //Closed is for nodes we have visited
        List<PathFindingNode> Closed = new List<PathFindingNode>();

        Node EndNode = Navigation.WorldPositionToNode(pathInstruction.To, RootNode);
        Node StartNode = Navigation.WorldPositionToNode(pathInstruction.From, RootNode);

        // Add the start node to the open node list
        PathFindingNode StartPathFindingNode = new PathFindingNode();
        StartPathFindingNode.node = StartNode;
        StartPathFindingNode.G = 0;
        StartPathFindingNode.H = Vector3.SqrMagnitude(EndNode.bounds.center - StartNode.bounds.center);
        StartPathFindingNode.F = StartPathFindingNode.G + StartPathFindingNode.H;
        Open.Add(StartPathFindingNode);

        //G = Distance between the current node and the start node
        //H = Estimated distance from current node to the end node
        //F = Total cost of node

        PathFindingNode closest = null;
        float RecordClosest = Mathf.Infinity;

        //Setup array for finding neighbours as looping through arrays are faster than lists
        int NeighbourSize;
        if (pathInstruction.agentCanFly)
        {
            NeighbourSize = 18;
        }
        else
        {
            NeighbourSize = 10;
        }
        Node[] Neighbours = new Node[NeighbourSize];

        //While we still have potential neighbour nodes
        while (Open.Count > 0)
        {
            PathFindingNode current = Open[0];

            //Debug.Log("at: " + current.node.bounds.center);

            //Set the current node to to lowest F value in the open node list
            for (int i = 0; i< Open.Count;i++)
            {
                if (Open[i].F < current.F)
                {
                    current = Open[i];
                }
            }

            //Used for debugging
            //currentNodeCheck.Add(current.node.bounds);

            Open.Remove(current);
            //The node has now been checked
            Closed.Add(current);

            //if currentNode is the goal, we are done
            if (current.node == EndNode)
            {                           
                ProcessPath(current,pathInstruction,true);

                //stopwatch.Stop();
                //Debug.Log("We got to the destination, nodes checked: " + Closed.Count);
                //Debug.Log("path finding calculation took: " + stopwatch.ElapsedMilliseconds + "ms. ");

                return;
            }
            else if (current.H < RecordClosest)
            {
                closest = current;
                RecordClosest = current.H;
            }

            //Get our neighbour nodes from the current node

            //Right, up, forward
            Neighbours[0] = (GetNeighbour(current.node,new Vector3(1,0,0),pathInstruction.agentCanFly));
            Neighbours[1] = (GetNeighbour(current.node, new Vector3(0, 1, 0), pathInstruction.agentCanFly));
            Neighbours[2] = (GetNeighbour(current.node, new Vector3(0, 0, 1), pathInstruction.agentCanFly));

            //left, down ,Back
            Neighbours[3] = (GetNeighbour(current.node, new Vector3(-1, 0, 0), pathInstruction.agentCanFly));
            Neighbours[4] = (GetNeighbour(current.node, new Vector3(0, -1, 0), pathInstruction.agentCanFly));
            Neighbours[5] = (GetNeighbour(current.node, new Vector3(0, 0, -1), pathInstruction.agentCanFly));

            //Diagonal flats
            Neighbours[6] = (GetNeighbour(current.node, new Vector3(1, 0, 1), pathInstruction.agentCanFly));
            Neighbours[7] = (GetNeighbour(current.node, new Vector3(-1, 0, -1), pathInstruction.agentCanFly));
            Neighbours[8] = (GetNeighbour(current.node, new Vector3(-1, 0, 1), pathInstruction.agentCanFly));
            Neighbours[9] = (GetNeighbour(current.node, new Vector3(1, 0, -1), pathInstruction.agentCanFly));

            //Diagonals, up down
            if (pathInstruction.agentCanFly)
            {
                Neighbours[10] = (GetNeighbour(current.node, new Vector3(1, 1, 1), pathInstruction.agentCanFly));
                Neighbours[11] = (GetNeighbour(current.node, new Vector3(-1, 1, -1), pathInstruction.agentCanFly));
                Neighbours[12] = (GetNeighbour(current.node, new Vector3(-1, 1, 1), pathInstruction.agentCanFly));
                Neighbours[13] = (GetNeighbour(current.node, new Vector3(1, 1, -1), pathInstruction.agentCanFly));

                Neighbours[14] = (GetNeighbour(current.node, new Vector3(1, -1, 1), pathInstruction.agentCanFly));
                Neighbours[15] = (GetNeighbour(current.node, new Vector3(-1, -1, -1), pathInstruction.agentCanFly));
                Neighbours[16] = (GetNeighbour(current.node, new Vector3(-1, -1, 1), pathInstruction.agentCanFly));
                Neighbours[17] = (GetNeighbour(current.node, new Vector3(1, -1, -1), pathInstruction.agentCanFly));
            }

            for (int i = 0; i<Neighbours.Length; i++)
            {
                if (Neighbours[i] == null) { continue; }

                //if neighbour is in the closedList or open list, no need to viist,add again
                if (CheckPathfindingList(Closed, Neighbours[i]) || CheckPathfindingList(Open, Neighbours[i]))
                {
                    continue;
                }

                // Create the f, g, and h values
                PathFindingNode findingNode = new PathFindingNode();
                findingNode.node = Neighbours[i];
                findingNode.CameFrom = current;

                //distance between neighbour and current, plus the additional cost to walk on here
                findingNode.G = current.G+1;
                float CostMultiplier = 1f + (Neighbours[i].BonusWalkingCost * 0.5f);

                //If we can't fly increase the cost for falling, also helps keeps path on walkable surfaces
                if (!pathInstruction.agentCanFly)
                {
                    if (Neighbours[i].Surface == null)
                    {
                        //findingNode.G++;
                        CostMultiplier += 0.2f;//was 0.2f
                    }
                    //Additionally Avoid walking along edges of drop offs so that we dont fall down unless we are looking to fall down
                    else if (IsOnEdge(Neighbours[i]))//&& EndNode.bounds.center.y>=neighbourNode.bounds.center.y
                    {
                        //findingNode.G += 5;
                        CostMultiplier += 0.2f;//was 2f
                    }
                }
                //if we can fly avoid surfaces
                else
                {
                    if(AdjacentToSurface(Neighbours[i]))
                    {
                        //findingNode.G += 5;
                        CostMultiplier += 0.5f;
                    }
                }

                //Add the break cost
                if(Neighbours[i].Surface != null)
                {
                    CostMultiplier += (Neighbours[i].Surface.BreakCost*0.5f);
                }

                //distance from neighbour to target
                findingNode.H = Vector3.Distance(pathInstruction.To, Neighbours[i].bounds.center);
                //combined G and H values
                findingNode.F = (findingNode.G + findingNode.H)* CostMultiplier;
                
                Open.Add(findingNode);
                //Debug.Log("Add neighbour: " + findingNode.node.bounds.center);
            }
        }

        ProcessPath(closest, pathInstruction,false);

        //stopwatch.Stop();
        //Debug.Log("We did not get there, nodes checked: " + Closed.Count);
        //Debug.Log("path finding calculation took: " + stopwatch.ElapsedMilliseconds + "ms. ");
    }


    //private List<Bounds> currentNodeCheck = new List<Bounds>();
    //private List<Bounds> VisitedNodes = new List<Bounds>();
    //private void OnDrawGizmos()
    //{
    //    if (currentNodeCheck.Count == 0) { return; }

    //    Gizmos.color = Color.red;
    //    Gizmos.DrawCube(currentNodeCheck[0].center, currentNodeCheck[0].size);
    //    VisitedNodes.Add(currentNodeCheck[0]);
    //    currentNodeCheck.RemoveAt(0);

    //    Gizmos.color = Color.green;

    //    for (int i = 0; i < VisitedNodes.Count; i++)
    //    {
    //        Gizmos.DrawCube(VisitedNodes[i].center, VisitedNodes[i].size);
    //    }
    //}


    /// <summary>
    /// Converts the destination node into a path, skips nodes in straight lines, still run in the thread
    /// </summary>
    /// <param name="LastNode"></param>
    /// <param name="pathInstruction"></param>
    private void ProcessPath(PathFindingNode CurrentNode, PathInstruction pathInstruction,bool GotThere)
    {
        List<PathAction> Path = new List<PathAction>();
        PathFindingNode prev = CurrentNode;
        PathAction pathAction;
        while (CurrentNode.CameFrom != null)
        {
            pathAction = new PathAction();
            pathAction.Position = CurrentNode.node.bounds.center;

            //Store whether this position needs a break action
            if(CurrentNode.node.Surface != null)
            {
                if(CurrentNode.node.Surface.Breakable)
                {
                    pathAction.NeedsDestroy = true;
                }
                else
                {
                    pathAction.NeedsDestroy = false;
                }                
            }

            Path.Add(pathAction);
            //if (Path.Count > 2)
            //{
            //    //if the node has the same x or z as the last and the one after skip as these are useless
            //    //if (((CurrentNode.node.bounds.center.x != prev.node.bounds.center.x && CurrentNode.node.bounds.center.x != CurrentNode.CameFrom.node.bounds.center.x) ||
            //    //    (CurrentNode.node.bounds.center.z != prev.node.bounds.center.z && CurrentNode.node.bounds.center.z != CurrentNode.CameFrom.node.bounds.center.z)) &&
            //    //    (CurrentNode.node.bounds.center.y != prev.node.bounds.center.y && CurrentNode.node.bounds.center.y != CurrentNode.CameFrom.node.bounds.center.y))
            //    //{
            //        Path.Add(pathAction);
            //    //}
            //}
            //else
            //{
            //    Path.Add(pathAction);
            //}

            prev = CurrentNode;
            CurrentNode = CurrentNode.CameFrom;
        }

        Path.Reverse();

        if (GotThere)
        {
            pathAction = new PathAction();
            pathAction.Position = pathInstruction.To;
            Path.Add(pathAction);
        }

        //Queue up the result onto the main thread for reading
        Action result = () =>
        {
            pathInstruction.FinishAction(Path);
        };

        QueuedPathResults.Enqueue(result);
        return;
    }   

    /// <summary>
    /// Checks if a node is contained in a list of PathFindingNodes
    /// </summary>
    /// <param name="pathFindingNodes"></param>
    /// <param name="node"></param>
    /// <returns></returns>
    private static bool CheckPathfindingList(List<PathFindingNode> pathFindingNodes, Node node)
    {
        for(int i = 0; i<pathFindingNodes.Count; i++)
        {
            if(pathFindingNodes[i].node == node) { return true; }
        }
        return false;
    }    

    /// <summary>
    /// Sets the quadtree to available for reading from other threads
    /// </summary>
    public void WorldUpdated()
    {
        MoveQueuedIntoCurrent();        
        PathsDoneSinceLastUpdate = 0;
        treeState = TreeState.AvailableForReading;
    }

    /// <summary>
    /// Go through current threads and clear out finished threads
    /// </summary>
    void CleanupThreads()
    {
        List<Thread> SortedThreads = new List<Thread>();
        for (int i = 0; i < PathFindingThreads.Count; i++)
        {
            if (PathFindingThreads[i].IsAlive)
            {
                SortedThreads.Add(PathFindingThreads[i]);
            }
        }
        PathFindingThreads.Clear();
        PathFindingThreads.AddRange(SortedThreads);
    }

    /// <summary>
    /// Creates a new thread if under max threads
    /// </summary>
    /// <param name="pathInstruction"></param>
    void FindAvailableThread()
    {
        int MaxThreads = SystemInfo.processorCount;

        //If slot available for another thread then create a new one for the queued task
        if (PathFindingThreads.Count < MaxThreads)
        {
            PathInstruction pathInstruction = QueuedPathInstructions.Dequeue();
            pathInstruction.From = pathInstruction.pathAgent.transform.position;
            pathInstruction.To = pathInstruction.pathAgent.GetCurrentTarget();

            pathInstruction.RootNode = FindObjectOfType<Navigation>().SceneNode;

            PathsDoneSinceLastUpdate++;
            //Create a new thread with this lambda function
            //which executes the path find function with the params we want
            PathFindingThreads.Add( StartThread(() => { PathFindTask(pathInstruction); }));
        }
    }

    /// <summary>
    /// Create a new thread to carry out the provided action (PathFindingTask())
    /// </summary>
    /// <param name="Function"></param>
    /// <returns></returns>
    Thread StartThread(Action Function)
    {
        //Debug.LogWarning("Staring a new thread");
        treeState = TreeState.Reading;
        Thread thread = new Thread(new ThreadStart(Function));
        thread.Start();
        thread.Name = "AI pathfinding thread";
        return thread;
    }

    /// <summary>
    /// Detects if the node forward,left,back,right is in the open
    /// </summary>
    /// <param name="node"></param>
    /// <returns>If is on the edge of a walkable space</returns>
    private static bool IsOnEdge(Node node)
    {
        //right,forward,left,back
        Node testnode = GetNeighbour(node, new Vector3(1, 0, 0),true);
        if(testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(0, 0, 1), true);
        if (testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(-1, 0, 0), true);
        if (testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(0, 0, -1), true);
        if (testEdgeNode(testnode)) { return true; }

        //diagonal forward right, back left, forward left, back right
        if (testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(1, 0, 1), true);
        if (testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(-1, 0, -1), true);
        if (testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(1, 0, -1), true);
        if (testEdgeNode(testnode)) { return true; }
        testnode = GetNeighbour(node, new Vector3(-1, 0, 1), true);
        if (testEdgeNode(testnode)) { return true; }

        return false;
    }

    private static bool testEdgeNode(Node node)
    {
        //if node, open or (not walkable and not breakable)
        if(node != null && (node.Surface == null || (node.Surface.HiddenID != 1 )))//&& !node.Surface.Breakable
        {
            return true;
        }
        return false;
    } 

    /// <summary>
    /// Checks if adjacent to any surface (used by flying agents to avoid bumping into things)
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public bool AdjacentToSurface(Node node)
    {
        //right,forward,left,back, up, down
        Node testnode = GetNeighbour(node, new Vector3(1, 0, 0), true);
        if (testnode != null && testnode.Surface != null && !testnode.Surface.Breakable) { return true; }
        testnode = GetNeighbour(node, new Vector3(0, 0, 1), true);
        if (testnode != null && testnode.Surface != null && !testnode.Surface.Breakable) { return true; }
        testnode = GetNeighbour(node, new Vector3(-1, 0, 0), true);
        if (testnode != null && testnode.Surface != null && !testnode.Surface.Breakable) { return true; }
        testnode = GetNeighbour(node, new Vector3(0, 0, -1), true);
        if (testnode != null && testnode.Surface != null && !testnode.Surface.Breakable) { return true; }
        testnode = GetNeighbour(node, new Vector3(0, 1, 0), true);
        if (testnode != null && testnode.Surface != null && !testnode.Surface.Breakable) { return true; }
        testnode = GetNeighbour(node, new Vector3(0, -1, 0), true);
        if (testnode != null && testnode.Surface != null && !testnode.Surface.Breakable) { return true; }

        return false;
    }

    /// <summary>
    /// Gets neighbour in direction from given node, can fly effects if it can return open space or not
    /// </summary>
    /// <param name="node"></param>
    /// <param name="Direction"></param>
    /// <param name="CanFly"></param>
    /// <returns></returns>
    public static Node GetNeighbour(Node node, Vector3 Direction, bool CanFly)
    {
        Node current = node.parent;

        //use the extents of the nodes bounds and move out from there as node size may be greater than agent size

        Vector3 DesiredPoint = Direction = new Vector3(
            (Direction.x * node.bounds.extents.x) +( Direction.x * (Navigation.BakedAgentSize * 0.5f)),
            (Direction.y * node.bounds.extents.y) + (Direction.y * (Navigation.BakedAgentSize * 0.5f)),
            (Direction.z * node.bounds.extents.z) + (Direction.z * (Navigation.BakedAgentSize * 0.5f)));

        DesiredPoint += node.bounds.center;

        //Debug.Log("Currently at : " + node.bounds.center + ", looking for node at: " + DesiredPoint);

        //Move up until our desired position is inside our scopeS
        while (!current.bounds.Contains(DesiredPoint))
        {
            if(current.parent == null)
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

        //at current right neighbour at the bottom of the tree
        //Debug.Log("Neighbour found at: " + current.bounds.center);
        //return current;

        //Open area
        if(current.Surface == null)
        {
            //If open and we cant fly then cant go here
            if (CanFly)            
            {
                return current;
            }
            else
            {
                //if we are coming from a walkable area, we can peek over the edges to check falls
                if(node.Surface != null) 
                {
                    return current;

                }
                //we must now be peeking and therefore can only navigate downwards
                if(Direction.y < 0)//0 && Direction.x == 0 && Direction.z == 0
                {
                    return current;                    
                }

                return null;
            }
        }
        //has surface properties
        else
        {
            //return null if we are trying to diagonally from open area
            if (!CanFly && (Direction.x != 0 && Direction.z != 0) && node.Surface == null)
            {
                return null;
            }
            //If is a walkable area
            if (current.Surface.HiddenID == 1)
            {
                return current;
            }

            if(current.Surface.Breakable)
            {
                if (!CanFly && Direction.y > 0) { return null; }
                return current;
            }
            else
            {
                //Blocked and can't be broken, cant go there
                return null;
            }
        }
    }

    /// <summary>
    /// Orders a path calculation between the two transforms
    /// </summary>
    /// <param name="From">Start transform</param>
    /// <param name="To">target transform</param>
    /// <param name="action">Function to call when complete</param>
    /// <param name="CanFly">If the agent can move through open space</param>
    /// <param name="NextQuadtree">Only execute on the next world update</param>
    /// <returns></returns>
    public void QueueAPath(PathAgent pathAgent, Action<List<PathAction>> ResultAction, bool CanFly, bool NextQuadtree)
    {
        PathInstruction pathInstruction = new PathInstruction();
        pathInstruction.pathAgent = pathAgent;
        pathInstruction.FinishAction = ResultAction;
        pathInstruction.agentCanFly = CanFly;

        if(NextQuadtree)
        {
            QueuedForNextUpdate.Enqueue(pathInstruction);
        }
        else
        {
            QueuedPathInstructions.Enqueue(pathInstruction);
        }        
    }

    public void MoveQueuedIntoCurrent()
    {
        while(QueuedForNextUpdate.Count>0)
        {
            QueuedPathInstructions.Enqueue(QueuedForNextUpdate.Dequeue());
        }
    }
}
