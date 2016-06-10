using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/* Checks for collisions between all objects within a list
 * and reacts accordingly. */

public class SphericalCollisionCheck : MonoBehaviour {

    // Sends all possible collisions to collision reaction
    public static void CheckForSphericalCollisions(List<GameObject> potentialCollisions)
    {
        for (int i = 0; i < potentialCollisions.Count; i++)
        {
            GameObject obj = potentialCollisions[i];
            Body objBody = obj.GetComponent<Body>();
            for (int j = 0; j < potentialCollisions.Count; j++)
            {
                GameObject otherObj = potentialCollisions[j];
                Body otherObjBody = otherObj.GetComponent<Body>();
                if (obj != otherObj)
                {
                    // First escape test: is the movement vector long enough for the objects to collide?
                    double distance = Vector3.Distance(obj.transform.position, otherObj.transform.position) -
                                      (objBody.Radius + otherObjBody.Radius);
                    Vector3 vectorSum = objBody.Velocity - otherObjBody.Velocity; // Sum of the two vectors
                    double magnitude = vectorSum.magnitude;
                    if (magnitude < distance)
                        continue;

                    // Second escape test: is the object actually moving towards the other object?
                    Vector3 normalizedMovement = vectorSum.normalized;
                    Vector3 connectingVector = otherObj.transform.position - obj.transform.position; // Vector between the center of the two bodies
                    double dot = Vector3.Dot(normalizedMovement, connectingVector);
                    if (dot <= 0) // If less than 0, objects not moving towards each other
                        continue;

                    /* Third escape test: if the closest the objects will get to eachother is more
                     * than the sum of their radii, they will not collide. */
                    double lengthNormalized = normalizedMovement.magnitude;
                    double closestPoint = Mathf.Pow((float) lengthNormalized, 2) - Mathf.Pow((float) dot,2);
                    double sumRadiiSquared = Mathf.Pow((float) (objBody.Radius + otherObjBody.Radius), 2);
                    if (closestPoint >= sumRadiiSquared)
                        continue;

                    // Check if third side of vector triangle is not negative
                    double side = sumRadiiSquared - closestPoint;
                    if (side < 0)
                        continue;

                    // Make sure the distance they must move is not greater than the magnitude of the movement vector
                    double moveDistance = dot - Mathf.Sqrt((float) side); // Distance the sphere must travel along the movement vector
                    if (magnitude < moveDistance)
                        continue;

                    // Set to collide
                    double unit = normalizedMovement.magnitude/objBody.Velocity.magnitude; // Represents when they collide
                    Vector3 aFinal = objBody.Velocity*(float)unit; // Use normalized?
                    Vector3 bFinal = otherObjBody.Velocity*(float) unit; // Use normalized?
                    objBody.Velocity = aFinal;
                    otherObjBody.Velocity = bFinal;
                    // TODO: collide
                }
            }
        }
    }
}
