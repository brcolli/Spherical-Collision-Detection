using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class Octree : MonoBehaviour
{

    private List<GameObject> _containedObjects; // The list of contained objects at a node
    private static Queue<GameObject> _pendingObjects = new Queue<GameObject>(); // Objects to be inserted later
    private int _maxCount = 5; // The maximum number of objects allowed to be contained in a node
    private int _minSize = 1; // The minimum allowed size TODO be aware of this (units)
    private Bounds _region; // Defined region
    private byte _activeNodes = 0; // 8 bits defining which children are being used

    private int _lifeSpan = 8; // How many frames to wait until an empty branch is deleted
    private int _currLife = -1; // Current life

    private Octree _parent;
    private List<Octree> _children = new List<Octree>(8);

    private static bool _ready = false; // Whether or not tree has pending objects
    private static bool _built = false; // Whether or not tree already exists

    /* Constructors */

    private Octree(Bounds region, List<GameObject> containedObjects)
    {
        _region = region;
        _containedObjects = containedObjects;
        _currLife = -1;
    }

    public Octree()
    {
        _containedObjects = new List<GameObject>();
        _region = new Bounds(Vector3.zero, Vector3.zero);
        _currLife = -1;
    }

    public Octree(Bounds region)
    {
        _containedObjects = new List<GameObject>();
        _region = region;
        _currLife = -1;
    }

    /* Methods */

    // Lazy tree update (update only if queue is full)
    private void UpdateTree()
    {
        if (!_built) // If not built, build tree based on queue...
        {
            while (_pendingObjects.Count != 0)
                _containedObjects.Add(_pendingObjects.Dequeue());
            BuildTree();
        }
        else // ...else insert objects into queue
        {
            while (_pendingObjects.Count != 0)
                Insert(_pendingObjects.Dequeue());
        }
        _ready = true;
    }

    // Build tree based on pending objects
    private void BuildTree()
    {
        if (_pendingObjects.Count <= 1)
            return;

        Vector3 enclosed = _region.max - _region.min; // enclosed space

        if (enclosed == Vector3.zero)
        {
            FindSmallestBounds();
            enclosed = _region.max - _region.min;
        }

        if (enclosed.x <= _minSize && enclosed.y <= _minSize && enclosed.z <= _minSize)
            return;

        // Create child regions
        List<Bounds> childRegions = new List<Bounds>(8);
        childRegions[0] = new Bounds((_region.center + _region.extents)/2, _region.extents); // Top front right
        childRegions[1] = 
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z + _region.extents.z)/2, _region.extents); // Top front left
        childRegions[2] =
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z)/2, _region.extents); // Top back left
        childRegions[3] =
            new Bounds(new Vector3(_region.center.x + _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z)/2, _region.extents); // Top back right
        childRegions[4] =
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z)/2, _region.extents); // Bottom front right
        childRegions[5] =
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z)/2, _region.extents); // Bottom front left
        childRegions[6] =
            new Bounds(new Vector3(_region.center.x + _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z)/2, _region.extents); // Bottom back left
        childRegions[7] =
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z)/2, _region.extents); // Bottom back right

        // List to hold objects within each octant
        List<List<GameObject>> objectList = new List<List<GameObject>>(8);
        for (int i = 0; i < 8; i++)
            objectList[i] = new List<GameObject>();
        //List<GameObject> remove = new List<GameObject>(); // List containing all objects that have been moved down the tree

        // Look for all objects within a bounding box and add to corresponding list, as well as remove from contained objects at this level
        foreach (GameObject obj in _containedObjects)
        {
            double radius = obj.GetComponent<Body>().Radius;
            if (Math.Abs(radius) > 0.001)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (childRegions[i].Contains(obj.transform.position) &&
                        childRegions[i].SqrDistance(obj.transform.position) >= Mathf.Pow((float) radius, 2))
                    {
                        objectList[i].Add(obj);
                        //remove.Add(obj);
                        _containedObjects.Remove(obj);
                        break;
                    }
                }
            }
        }

        /*foreach (GameObject obj in remove)
            _containedObjects.Remove(obj);*/

        // Make children and recurse
        for (int i = 0; i < 8; i++)
        {
            if (objectList[i].Count != 0)
            {
                _children[i] = CreateNode(childRegions[i], objectList[i]);
                _activeNodes |= (byte) (1 << i);
                _children[i].BuildTree();
            }
        }

        _built = true;
        _ready = true;
    }

    // Create child node with region and list of game objects
    private Octree CreateNode(Bounds region, List<GameObject> objectList)
    {
        if (objectList.Count == 0)
            return null;

        Octree ot = new Octree(region, objectList) {_parent = this};

        return ot;
    }

    // Create child node with region and single game object, after inserting into list; less memory use
    private Octree CreateNode(Bounds region, GameObject obj)
    {
        List<GameObject> objectList = new List<GameObject>(1) {obj};
        Octree ot = new Octree(region, objectList) {_parent = this};

        return ot;
    }

    // Insert object into tree
    private void Insert(GameObject body)
    {
        
    }

    // Finds smallest cube with power of 2 size
    private void FindSmallestBounds()
    {

    }

    /* Getters and setters */

    public List<GameObject> ContainedObjects
    {
        get { return _containedObjects; }
    }

    public Queue<GameObject> PendingObjects
    {
        get { return _pendingObjects; }
    }

    public Bounds Body
    {
        get { return _region; }
    }

    public byte ActiveNodes
    {
        get { return _activeNodes; }
    }

    public int CurrLife
    {
        get { return _currLife; }
    }
}
