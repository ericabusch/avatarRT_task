using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathInstantiator : MonoBehaviour
{

    public bool noisy;
    public Vector3 startPos;
    public int nWayPoints;
    //private float travelDist = 50;
    public float range = 0f;
    public Dictionary<string, float> corners;

    // declare a variable for the prefab to be instanced
    public GameObject pickupPrefab;

    // this is where we're going to store the transforms for the objects
    public List<Vector3> points;
    private GameObject holder;


    void OnEnable()
    {
        // loop repeats for each instance
        //noisy = true;
        Vector3 startPosUp = new Vector3(startPos.x, 0.5f, startPos.z);
        points = generatePoints(startPosUp, nWayPoints, range, noisy);
        holder = new GameObject();

        for (int i = 0; i < points.Count; i++)
        {
            Instantiate(pickupPrefab, points[i], Quaternion.identity, holder.transform);

        }

        holder.transform.parent = this.gameObject.transform;

    }
    void Update()
    {
    }

    private List<Vector3> generatePoints(Vector3 startPos, int nPoints, float dist, bool noisy)
    {

        points = new List<Vector3>();
        points.Add(startPos);

        if (noisy)
        {
            for (var i = 0; i < nPoints - 1; i++)
            {
                Vector3 pos = points[i];
                if (Vector3.Distance(pos, points[i]) < dist * 3f)
                {
                    float zDelta = UnityEngine.Random.Range(0, dist * 2f) + dist;
                    float xDelta = UnityEngine.Random.Range(-dist * 1.5f, dist * 1.5f) + dist;
                    float nextX = points[i].x + xDelta;
                    float nextZ = points[i].z + zDelta;


                    //check if too close to the boundary; make it curve
                    if ((Mathf.Abs(nextX - corners["xMax"]) <= 2f * dist))
                    {
                        xDelta = 0f;
                    }
                    if ((Mathf.Abs(nextX - corners["xMin"]) <= 2f * dist))
                    {
                        xDelta = 0f;
                    }

                    if ((Mathf.Abs(nextZ - corners["zMax"]) <= 2f * dist))
                    {
                        //xDelta = 1f;
                        zDelta = 0f;

                    }
                    if ((Mathf.Abs(nextZ - corners["zMin"]) <= 2f * dist))
                    {
                        //xDelta = 1f;
                        zDelta = 0f;
                    }

                    nextX = points[i].x + xDelta;
                    nextZ = points[i].z + zDelta;

                    // make sure all points are within the boundaries


                    if ((Mathf.Abs(nextX - corners["xMax"]) <= 4f * dist) || (Mathf.Abs(nextX - corners["xMin"]) <= 4f * dist) ||
                    (Mathf.Abs(nextZ - corners["zMax"]) <= 4f * dist) || (Mathf.Abs(nextZ - corners["zMin"]) <= 4f * dist))
                    {
                        return points;
                    }
                    pos = new Vector3(nextX, 0.5f, nextZ);
                }

                points.Add(pos);
            }


        }
        else
        {
            for (var i = 0; i < nPoints - 1; i++)
            {
                Vector3 pos = points[i];
                float zDelta = 4 + dist;
                float xDelta = 4 + dist;
                float nextX = points[i].x + xDelta;
                float nextZ = points[i].z + zDelta;
                // make sure all points are within the boundaries

                int solution = 0;

                foreach (string key in corners.Keys)
                {
                    float comparator;
                    if ((key == "xMin") || (key == "xMax"))
                    {
                        comparator = nextX;
                    }
                    else
                    {
                        comparator = nextZ;
                    }
                    if (Mathf.Abs(comparator - corners[key]) <= 4f * dist)
                    {
                        solution++;
                        print("Found exist condition in distance");
                        return points;
                    }

                }

                nextX = points[i].x + xDelta;
                nextZ = points[i].z + zDelta;
                pos = new Vector3(nextX, 0f, nextZ);
                points.Add(pos);
            }
        }

        return points;
    }
}
