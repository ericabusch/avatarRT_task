using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformStorer : MonoBehaviour
{
    // this gets set by the player controller
    public Dictionary<float, float> FacingDirections = new Dictionary<float, float>();
    public Dictionary<float, Vector3> Positions = new Dictionary<float, Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        FacingDirections = new Dictionary<float, float>();
        Positions = new Dictionary<float, Vector3>();

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ResetStore()
    {
        FacingDirections = new Dictionary<float, float>();
        Positions = new Dictionary<float, Vector3>();
    }
}
