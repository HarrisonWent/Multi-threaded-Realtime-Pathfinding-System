using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node
{
    //0 is reserved for open area and will be null, 1 for walkable
    public CustomNavigationSurface Surface;

    public Bounds bounds;
    public NodeChildren nodeChildren;
    public Node parent;

    //Set by the surface bellow if this is set to walkable
    public int BonusWalkingCost = 0;
}


public class NodeChildren
{
    //Bottom level
    public Node D1;
    public Node D2;
    public Node D3;
    public Node D4;

    //Top Level
    public Node U1;
    public Node U2;
    public Node U3;
    public Node U4;
}