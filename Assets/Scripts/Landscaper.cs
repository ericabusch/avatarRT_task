using UnityEngine;
using System.Collections.Generic;

public class Landscaper : MonoBehaviour
{
    public GameObject North;
    public GameObject East;
    public GameObject West;
    public GameObject South;
    public GameObject treeType1;
    public List<Vector3> boundaryPoints;
    public GameObject holder;
    public GameObject mushroom1;
    public GameObject rock1;
    public GameObject mushroom2;
    public GameObject rock2;
    public GameObject bush1;
    public GameObject bush2;
    public GameObject grass1;
    public GameObject grass2;
    public GameObject treeType2;
    public GameObject treeType3;
    public int maxObjects;
    public int minObjects = 2;
    public int dist;
    public List<Vector3> objectPoints;
    public List<Vector3> RewardLocations;
    public Dictionary<string, float> corners;
    public List<Vector3> Corners;
    private List<GameObject> LandscapeFeatures;
    public int numObjects;
    public int numRendered = 0;

    float xMin;
    float xMax;
    float zMax;
    float zMin;

    void OnEnable()
    {

        holder = new GameObject();
        corners = new Dictionary<string, float>();
        boundaryPoints = GetBoundaryPoints();
        for (int i = 0; i < boundaryPoints.Count; i++)
        {
            Instantiate(treeType1, boundaryPoints[i], Quaternion.identity, holder.transform);

        }
        holder.transform.parent = gameObject.transform;
        LandscapeFeatures = new List<GameObject>();
        SetUpArena();

    }

    void SetUpArena()
    {
        Corners = new List<Vector3>();
        Corners.Add(North.transform.position);
        Corners.Add(East.transform.position);
        Corners.Add(South.transform.position);
        Corners.Add(West.transform.position);

    }

    private List<GameObject> GameObjectTypes()
    {
        List<GameObject> objectTypes = new List<GameObject>();
        objectTypes.Add(mushroom1);
        objectTypes.Add(mushroom2);
        objectTypes.Add(rock1);
        objectTypes.Add(rock2);
        objectTypes.Add(grass1);
        objectTypes.Add(grass2);
        objectTypes.Add(bush1);
        objectTypes.Add(bush2);
        objectTypes.Add(treeType3);
        return objectTypes;

    }


    private List<Vector3> GetBoundaryPoints()
    {
        xMin = North.transform.position.x;
        xMax = East.transform.position.x;
        zMax = North.transform.position.z;
        zMin = South.transform.position.z; 
        corners.Add("xMin", xMin);
        corners.Add("xMax", xMax);
        corners.Add("zMin", zMin);
        corners.Add("zMax", zMax);

        // create the top line and the bottom line
        for (float x = xMin + 2; x < xMax; x += 2)
        {
            Vector3 i = new Vector3(x, 0, zMax);
            Vector3 j = new Vector3(x, 0, zMin);
            boundaryPoints.Add(i);
            boundaryPoints.Add(j);
        }
        for (float z = zMin + 2; z < zMax; z += 2)
        {
            Vector3 i = new Vector3(xMin, 0, z);
            Vector3 j = new Vector3(xMax, 0, z);
            boundaryPoints.Add(i);
            boundaryPoints.Add(j);
        }
        return boundaryPoints;
    }

    public void SetUpObjects()
    {
        numObjects = UnityEngine.Random.Range(maxObjects - minObjects, maxObjects);
        objectPoints = new List<Vector3>();

        for (int i = 0; i < numObjects; i++)
        {
            float xloc = UnityEngine.Random.Range(xMin, xMax);
            float zloc = UnityEngine.Random.Range(zMin, zMax);
            Vector3 vec = new Vector3(xloc, 0.5f, zloc);
            List<Vector3> NoZone = new List<Vector3>();
            objectPoints.Add(Vector3.Scale(vec, new Vector3(1f, 0f, 1f)));
        }
    }
    public void RenderLandscape()
    {
        // loop through points on the path and object points;
        // measure distance and render objects at valid locations
        List<GameObject> objectTypes = GameObjectTypes();
        List<Vector3> toRender = new List<Vector3>();

        for (int x = 0; x < objectPoints.Count; x++)
        {
            Vector3 loc = objectPoints[x];
            bool CHOOSE = true;

            // decide if it's too close to other things or to the path to render
            for (int c = 0; c < RewardLocations.Count; c++)
            {
                if (Vector3.Distance(loc, RewardLocations[c]) < dist)
                {
                    CHOOSE = false;
                }
            }

            if (CHOOSE)
            {
                int idx = UnityEngine.Random.Range(0, objectTypes.Count);
                GameObject g = objectTypes[idx];
                GameObject X = Instantiate(g, loc, Quaternion.identity, holder.transform);
                LandscapeFeatures.Add(X);
                numRendered++;
            }

        }
        print(string.Format("Rendered {0}/{1} objects", numRendered, numObjects));
    }
}