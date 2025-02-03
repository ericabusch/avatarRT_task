using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class InfoLoader : MonoBehaviour
{
    public string player;
    public int run;
    public int session;
    public string baseDirectory;
    public string moveType;
    public string experimentPath;
    public string inputDirectory;
    public int difficulty;
    public string bicFN = "params.txt";
    public string elbFN = "params2.txt";
    public string elbPath = "/Users/elb/Desktop/BCI/RT_Avatar/experiment";
    public string bicPath = "/home/watts/Desktop/ntblab/erica/RT_Avatar/experiment";
    public string paramFile;
    public bool pythonCommunicator = false;
    public int fileCounter;
    public InfoSetter infoSetter;
    public int level;
    // Start is called before the first frame update
    void Start()
    {
        fileCounter = 1;
        if (Directory.GetCurrentDirectory().Contains("watts"))
        {
            experimentPath = bicPath;
            paramFile = string.Format("{0}/{1}", experimentPath, bicFN);

        }
        else
        {
            experimentPath = elbPath;
            paramFile = string.Format("{0}/{1}", experimentPath, elbFN);
        }

        // read in information from a text file (written out by python)
        string[] paramLines = File.ReadAllLines(paramFile);
        player = paramLines[0];
        run = int.Parse(paramLines[1]);
        session = int.Parse(paramLines[2]);
        baseDirectory = paramLines[3];
        moveType = paramLines[4];
        difficulty = int.Parse(paramLines[5]);
        int pycomm = int.Parse(paramLines[6]);
        bool noisy = bool.Parse(paramLines[7]);
        if (pycomm == 1)
        {
            pythonCommunicator = true;
            print(string.Format("Params from: {0} | Dirname: {1} | Move: {2} | Difficulty: {3} | Python: True", paramFile, baseDirectory, moveType, difficulty));
        }
        else
        {
            pythonCommunicator = false;
            print(string.Format("Params from: {0} | Dirname: {1} | Move: {2} | Python: False", paramFile, baseDirectory, moveType));
        }

        inputDirectory = string.Format("{0}/{1}", baseDirectory, "scanner_comms");
        print(string.Format("Input directory from info loader: {0}", inputDirectory));
        if (moveType == "ScannerMove")
        {
            try
            {
                string thisFN = string.Format("{0}/scanner_comms/display_level.txt", baseDirectory);
                print(thisFN);
                string[] lines = File.ReadAllLines(thisFN);
                level = int.Parse(lines[0]);
                print(string.Format("Loaded level {0} from {1}", level, thisFN));
            }
            catch (Exception)
            {
                print("No level found; setting to 1");
                level = 1;
            }
        }
        infoSetter.SetInfo();
    }
}
