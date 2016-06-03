using UnityEngine;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

public class ThreadedSpatialSubdivision : MonoBehaviour
{

    private GameObject _center; // The center of the subdivided space, the lower-most object (by x, y, and z)
    private int _cellSize = 0; // The size of each subdivided space

    // Initialize lists by setting size to number of objects * 2^d, where d is the dimension (d = 3)
    public void Initialize(List<GameObject> system, GameObject center, GameObject largestObject)
    {
        int numObjects = system.Count;
        CellIDs.Capacity = numObjects*8;
        ObjectIDs.Capacity = numObjects*8;
        _center = center;
        _cellSize = (int)largestObject.GetComponent<Body>().Radius*2; // Size of cell is twice the radius of the largest object

        // Add cell IDs and object IDs
        Obj body = new Obj();
        ulong id = 0;
        List<ulong> hashes = new List<ulong>(); // Hashes for each object
        List<Thread> hashThreads = new List<Thread>();
        for (int i = 0; i < numObjects; i++)
        {
            // Set body
            body.ObjectID = id;
            body.Object = system[(int)id];

            // At i%8 == 0, new object
            if (i%8 == 0)
            {
                if (i != 0) // Edge case
                    id++;

                hashes = GetCellHashes(body.Object); // Hashes for all cells given a body
                body.Control = 1; // Indicating centroid cell for Object IDs list
            }
            else
            {
                body.Control = 0;
            }

            // Set lists
            CellIDs[i] = hashes[i%8];
            ObjectIDs[i] = body;
        }
    }

    // Generate hashes of cells based on object position
    private List<ulong> GetCellHashes(GameObject body)
    {
        List<ulong> hashes = new List<ulong>();
        Vector3 centerPos = _center.transform.position;
        Vector3 bodyPos = body.transform.position;

        // Bit shifts
        int XSHIFT = 64;
        int YSHIFT = 42;
        int ZSHIFT = 0;

        // Home cell
        hashes[0] = ((ulong) ((bodyPos.x-centerPos.x)/_cellSize) << XSHIFT) |
                   ((ulong) ((bodyPos.y-centerPos.y)/_cellSize) << YSHIFT) |
                   ((ulong) ((bodyPos.z-centerPos.z)/_cellSize) << ZSHIFT);

        /* Phantom cells */
        double bodyRadius = body.GetComponent<Body>().Radius;
        int i = 1;

        // Directional flags
        bool right = false, left = false, up = false, down = false, front = false, back = false;

        // Sphere crosses to the right or left
        if ((bodyPos.x/_cellSize) < ((bodyPos.x + bodyRadius)/_cellSize))
        {
            right = true;
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
        }
        else if ((bodyPos.x/_cellSize) > ((bodyPos.x + bodyRadius)/_cellSize))
        {
            left = true;
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
        }

        // Sphere crosses up or down
        if ((bodyPos.y / _cellSize) < ((bodyPos.y + bodyRadius) / _cellSize))
        {
            up = true;
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
        }
        else if ((bodyPos.y / _cellSize) > ((bodyPos.y + bodyRadius) / _cellSize))
        {
            down = true;
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
        }

        // Sphere crosses front or back
        if ((bodyPos.z / _cellSize) < ((bodyPos.z + bodyRadius) / _cellSize))
        {
            front = true;
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }
        else if ((bodyPos.z / _cellSize) > ((bodyPos.z + bodyRadius) / _cellSize))
        {
            back = true;
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }

        // Right-side Diagonals
        if (right && up)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
            if (front)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
            else if (back)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
        }
        else if (right && down)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
            if (front)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
            else if (back)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
        }
        if (right && front)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }
        else if (right && back)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x + _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }

        // Left-side Diagonals
        if (left && up)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
            if (front)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
            else if (back)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
        }
        else if (left && down)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z) / _cellSize) << ZSHIFT);
            i++;
            if (front)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
            else if (back)
            {
                hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                           ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                           ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
                i++;
            }
        }
        if (left && front)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }
        else if (left && back)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x - _cellSize) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }

        // Front and back Diagonals
        if (front && up)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }
        else if (front && down)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y + _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }
        if (back && up)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z + _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }
        else if (back && down)
        {
            hashes[i] = ((ulong)((bodyPos.x - centerPos.x) / _cellSize) << XSHIFT) |
                       ((ulong)((bodyPos.y - centerPos.y - _cellSize) / _cellSize) << YSHIFT) |
                       ((ulong)((bodyPos.z - centerPos.z - _cellSize) / _cellSize) << ZSHIFT);
            i++;
        }

        // Set all remaining spaces to invalid, AKA 0xffffff
        while (i < 8)
        {
            hashes[i] = 0xffffff;
            i++;
        }

        return hashes;
    }

    // Class containing object ID and control bit
    public class Obj
    {
        public ulong ObjectID { get; set; }
        public GameObject Object { get; set; }
        public ulong Control { get; set; }
    }

    /* Getters and Setters */

    // The two arrays to contain the cells and objects, separated for sorting efficiency
    public List<ulong> CellIDs { get; set; } // Contains cell IDs of the objects
    public List<Obj> ObjectIDs { get; set; } // Contains object IDs and control bits

    // Get _cellSize
    public int CellSize()
    {
        return _cellSize;
    }
}
