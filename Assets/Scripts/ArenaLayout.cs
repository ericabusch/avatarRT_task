using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.UI;

public class ArenaLayout : MonoBehaviour
{
    // this gets set by the landscaper
    public List<Vector3> Corners;
    public List<Vector3> Borders;
    public List<Vector3> Targets;
    public List<Vector3> Obstacles;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ResetLayout()
    {
        Corners = null;
        Borders = null;
        Targets = null;
        Obstacles = null;
    }

}
