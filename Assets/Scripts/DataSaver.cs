using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class DataSaver : MonoBehaviour
{
    public string outpath; // this is where you will save all info at the end of the round
    public StatTracker theseStats;
    private int BufferSize = 65536;
    public Client thisClient;
    public Dictionary<float, Vector3> Positions;
    public Dictionary<float, float> FacingDirections;
    public List<Vector3> Obstacles;
    public List<Vector3> Boundaries;
    public List<Vector3> Corners;
    public List<Vector3> Targets;

    public float RoundStartTime;
    public float RoundEndTime;
    public float LevelNumber;
    public int UsingClient;
    public int NoisyRound;
    public int Difficulty;
    public int CurrentLevelGame;
    public string MoveMode;
    public float ErrorRate = 0f;


    // Start is called before the first frame update
    void Start()
    {

    }

    public void SaveRound(int RoundNumber)
    {
        string basefn = string.Format("{0}/round_{1}/", outpath, RoundNumber.ToString("00"));
        Directory.CreateDirectory(basefn);
        print(string.Format("Made and saving to {0}", basefn));
        thisClient.RoundDirname = basefn;
        string playerInfoFn = string.Format("{0}/player_info.txt", basefn);
        string locationFn = string.Format("{0}/player_transform.txt", basefn);
        string gameboardFn = string.Format("{0}/gameboard.txt", basefn);
        SaveGameboard(gameboardFn);
        SaveLocations(locationFn);
        SaveInfo(playerInfoFn, RoundNumber);
        if (MoveMode == "ScannerMove")
        {
            string fn = string.Format("{0}/lastLevel.txt", outpath);

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fn, true, System.Text.Encoding.UTF8, BufferSize))
            {
                sw.WriteLine(string.Format("{0}", CurrentLevelGame));
            }
        }
    }


    private void SaveInfo(string filename, int RoundNumber)
    {
        using (System.IO.StreamWriter sw = new System.IO.StreamWriter(filename, true, System.Text.Encoding.UTF8, BufferSize))
        {
            sw.WriteLine(string.Format("RoundNumber,LevelNumber,RoundStartTime,RoundEndTime,Noisy,UsingClient,MoveMode,Difficulty,ErrorRate"));
            sw.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}", RoundNumber,
                LevelNumber, RoundStartTime, RoundEndTime, NoisyRound, UsingClient, MoveMode, Difficulty, ErrorRate));
        }
    }
    private void SaveGameboard(string filename)
    {

        using (System.IO.StreamWriter sw = new System.IO.StreamWriter(filename, true, System.Text.Encoding.UTF8, BufferSize))
        {
            // Write out the game board
            sw.WriteLine(string.Format("ObjectType,x,y,z"));
            foreach (Vector3 item in Corners)
            {
                sw.WriteLine(string.Format("Corner,{0},{1},{2}", item.x, item.y, item.z));
            }
            foreach (Vector3 item in Boundaries)
            {
                sw.WriteLine(string.Format("BorderObj,{0},{1},{2}", item.x, item.y, item.z));
            }
            foreach (Vector3 item in Obstacles)
            {
                sw.WriteLine(string.Format("Obstacle,{0},{1},{2}", item.x, item.y, item.z));
            }
            foreach (Vector3 item in Targets)
            {
                sw.WriteLine(string.Format("Target,{0},{1},{2}", item.x, item.y, item.z));
            }
        }
        // reset

    }

    private void SaveLocations(string filename)
    {
        using (System.IO.StreamWriter sw = new System.IO.StreamWriter(filename, true, System.Text.Encoding.UTF8, BufferSize))
        {
            sw.WriteLine(string.Format("Event,Time,x,y,z"));
            foreach (KeyValuePair<float, float> entry in FacingDirections)
            {
                sw.WriteLine(string.Format("Transform.Rotation,{0},0,{1},0", entry.Key, entry.Value));
            }
            foreach (KeyValuePair<float, Vector3> entry in Positions)
            {
                sw.WriteLine(string.Format("Transform.Position,{0},{1},{2},{3}", entry.Key, entry.Value.x, entry.Value.y, entry.Value.z));
            }
        }
        FacingDirections = null;
        Positions = null;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
