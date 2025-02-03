using System;
using UnityEngine;

public class MovementOutput : ScriptableObject
{

    public float DecodedAngle;
    public float Input_V;

    public MovementOutput(float first, float second)
    {
        float DecodedAngle = first;
        float Input_V = second;
    }

}
