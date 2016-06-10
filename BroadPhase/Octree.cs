using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;

/* A class using an octree for spatial subdivision of my universe. Used in collision detection
 * as the broad phase to determine possible collisions, and send possible collisions to the narrow
 * phase to be accurately determined. Implements lazy creation, generating a branch only when needed
 * and deleting a branch only when needed (after a certain lifetime of being empty). Possible collisions
 * are determined by traversing up the tree from a given cell and returning all objects in all its parents. */

public class Octree : MonoBehaviour
{

    private List<GameObject> _containedObjects; // The list of contained objects at a node
    private static Queue<GameObject> _pendingObjects = new Queue<GameObject>(); // Objects to be inserted later
    private List<GameObject> _potentialCollisions = new List<GameObject>(); // List of objects that can potentially collide within this tree
    //private int _maxCount = 5; // The maximum number of objects allowed to be contained in a node
    private int _minSize = 1; // The minimum allowed size TODO be aware of this (units)
    private Bounds _region; // Defined region
    private byte _activeNodes = 0; // 8 bits defining which children are being used

    private int _lifeSpan = 8; // How many frames to wait until an empty branch is deleted
    private int _currLife = -1; // Current life

    private Octree _parent;
    private List<Octree> _children = new List<Octree>(8);

    //private static bool _ready = false; // Whether or not tree has pending objects
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
        //_ready = true;
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
        //_ready = true;
    }

    // Update tree based on game time, call this
    public void UpdateTree(Time time)
    {
        if (_built)
        {
            // Subtract lifespan
            if (_containedObjects.Count == 0)
            {
                if (_currLife == -1)
                    _currLife = _lifeSpan;
                else if (_currLife > 0)
                    _currLife--;
            }
            else
            {
                if (_currLife != -1)
                {
                    if (_lifeSpan <= 64)
                        _lifeSpan *= 2;
                    _currLife = -1;
                }
            }
            List<GameObject> movedObjects = new List<GameObject>(_containedObjects.Count);

            foreach (GameObject obj in _containedObjects)
            {
                // See if object has moved TODO keep an eye on this
                if (BodyPhysics.TestMovement(obj))
                    movedObjects.Add(obj);
            }

            // Remove dead objects
            int numObjects = _containedObjects.Count;
            for (int i = 0; i < numObjects; i++)
            {
                if (!_containedObjects[i].activeSelf) // TODO potentially change to activeInHierarchy
                {
                    if (movedObjects.Contains(_containedObjects[i]))
                        movedObjects.Remove(_containedObjects[i]);
                    _containedObjects.RemoveAt(i--);
                    numObjects--;
                }
            }

            // Update child nodes
            for (int i = _activeNodes, j = 0; i > 0; i >>= 1, j++)
            {
                if ((i & 1) == 1)
                    _children[j].UpdateTree(time);
            }

            // For all moved objects, move its position in the tree
            foreach (GameObject obj in movedObjects)
            {
                Octree currNode = this;
                /* Find out how far up the tree the object needs to be moved.
                 * Move the object into an enclosing parent until full containment. */
                while (!currNode._region.Contains(obj.transform.position) &&
                       !(currNode._region.SqrDistance(obj.transform.position) >= Mathf.Pow((float) obj.GetComponent<Body>().Radius, 2)))
                {
                    if (currNode._parent != null)
                        currNode = currNode._parent;
                    else
                        break;
                }

                // Remove from current node and place into new node
                _containedObjects.Remove(obj);
                currNode.Insert(obj);
            }

            // Prune dead branches
            for (int i = _activeNodes, j = 0; i > 0; i >>= 1, j++)
            {
                if ((i & 1) == 1 && _children[j]._currLife == 0)
                {
                    _children[j] = null;
                    _activeNodes ^= (byte) (1 << j);
                }
            }

            // Look for collisions
            if (_parent == null) // root node
            {
                // TODO this
                _potentialCollisions.AddRange(CheckForPotentialCollisions(this)); // Get all potential collisions added to _potentialCollisions
                SphericalCollisionCheck.CheckForSphericalCollisions(_potentialCollisions);
            }
        }
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

    // Insert object into tree at shallowest level possible
    private void Insert(GameObject body)
    {
        // If empty leaf node, insert and leave
        if (_containedObjects.Count <= 1 && _activeNodes == 0)
        {
            _containedObjects.Add(body);
            return;
        }

        // Check to see if enclosed region is greater than the minimum dimensions
        Vector3 enclosed = _region.max - _region.min;
        if (enclosed.x <= _minSize && enclosed.y <= _minSize && enclosed.z <= _minSize)
        {
            _containedObjects.Add(body);
            return;
        }

        // Create subdivided regions for each octant in current region
        List<Bounds> childRegions = new List<Bounds>(8);
        childRegions[0] = (_children[0] != null) ? _children[0]._region : new Bounds((_region.center + _region.extents) / 2, _region.extents); // Top front right
        childRegions[1] = (_children[1] != null) ? _children[1]._region :
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z + _region.extents.z) / 2, _region.extents); // Top front left
        childRegions[2] = (_children[2] != null) ? _children[2]._region :
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z) / 2, _region.extents); // Top back left
        childRegions[3] = (_children[3] != null) ? _children[3]._region :
            new Bounds(new Vector3(_region.center.x + _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z) / 2, _region.extents); // Top back right
        childRegions[4] = (_children[4] != null) ? _children[4]._region :
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z) / 2, _region.extents); // Bottom front right
        childRegions[5] = (_children[5] != null) ? _children[5]._region :
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z) / 2, _region.extents); // Bottom front left
        childRegions[6] = (_children[6] != null) ? _children[6]._region :
            new Bounds(new Vector3(_region.center.x + _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z) / 2, _region.extents); // Bottom back left
        childRegions[7] = (_children[7] != null) ? _children[7]._region :
            new Bounds(new Vector3(_region.center.x - _region.extents.x, _region.center.y + _region.extents.y, _region.center.z - _region.extents.z) / 2, _region.extents); // Bottom back right

        // Is it completely contained?
        if (_region.Contains(body.transform.position) &&
            (_region.SqrDistance(body.transform.position) >= Mathf.Pow((float) body.GetComponent<Body>().Radius, 2)))
        {
            bool found = false;

            // Attempt to placec in child node, else place in current node
            for (int i = 0; i < 8; i++)
            {
                if (childRegions[i].Contains(body.transform.position) &&
                    (childRegions[i].SqrDistance(body.transform.position) >=
                     Mathf.Pow((float) body.GetComponent<Body>().Radius, 2)))
                {
                    if (_children[i] != null)
                        _children[i].Insert(body); // Add item to tree and let child work it out
                    else
                    {
                        _children[i] = CreateNode(childRegions[i], body); // Create new tree
                        _activeNodes |= (byte) (1 << i);
                    }
                    found = true;
                }
            }
            if (!found)
                _containedObjects.Add(body);
        }
        // Either the object lies outside of enclosed box, or it is intersecting it. Must rebuild tree.
        else
            BuildTree();
    }

    // Recurse up to root node and add all potential collisions
    private List<GameObject> CheckForPotentialCollisions(Octree currNode)
    {
        // Reached root
        if (currNode._parent == null)
            return currNode._containedObjects;

        List<GameObject> potentialCollisions = new List<GameObject>();

        // Add all objects contained in this layer
        if (currNode._containedObjects.Count != 0)
            potentialCollisions.AddRange(currNode._containedObjects);

        // Add parent potential collisions and return
        potentialCollisions.AddRange(CheckForPotentialCollisions(currNode._parent));
        return potentialCollisions;
    }

    // Find dimensions of smallest possible bounding box necessary to enclose all objects in list
    private void FindSmallestBox()
    {
        Vector3 globalMin = Vector3.zero, globalMax = Vector3.zero;

        // Find extremes of each contained object
        foreach (GameObject obj in _containedObjects)
        {
            Vector3 localMin = Vector3.zero, localMax = Vector3.zero;
            Bounds objBounds = obj.GetComponent<Bounds>();

            if (Math.Abs(objBounds.size.magnitude) < 0.001)
                throw new Exception("Must have a bounding region.");

            if (objBounds.max != objBounds.min)
            {
                localMin = objBounds.min;
                localMax = objBounds.max;
            }

            if (localMin.x < globalMin.x)
                globalMin.x = localMin.x;
            if (localMin.y < globalMin.y)
                globalMin.y = localMin.y;
            if (localMin.z < globalMin.z)
                globalMin.z = localMin.z;

            if (localMax.x < globalMax.x)
                globalMax.x = localMax.x;
            if (localMax.y < globalMax.y)
                globalMax.y = localMax.y;
            if (localMax.z < globalMax.z)
                globalMax.z = localMax.z;
        }

        _region.min = globalMin;
        _region.max = globalMax;
    }

    // Finds smallest cube with power of 2 size
    private void FindSmallestBounds()
    {
        FindSmallestBox();

        // Find min offset
        Vector3 offset = _region.min - Vector3.zero;
        _region.min += offset;
        _region.max += offset;

        // Find nearest power of two for max values
        int high = (int) Mathf.Floor(Mathf.Max(Mathf.Max(_region.max.x, _region.max.y), _region.max.z));

        // Is it power of 2?
        for (int b = 0; b < 32; b++)
        {
            if (high == 1 << b)
            {
                _region.max = new Vector3(high, high, high);

                _region.min -= offset;
                _region.max -= offset;
                return;
            }
        }

        // Gets MSB, essentially ceiling but with bits to get power of 2
        int msb = GetMSB(high);
        _region.max = new Vector3(msb, msb, msb);

        _region.min -= offset;
        _region.max -= offset;
    }

    // Return Most Significant Bit (MSB)
    private int GetMSB(int num)
    {
        int bitPos = 0, msb = num;
        while (msb != 0)
        {
            bitPos++;
            msb >>= 1;
        }
        return (num & (1 << bitPos-1));
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
