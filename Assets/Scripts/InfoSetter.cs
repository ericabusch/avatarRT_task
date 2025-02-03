using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class InfoSetter : MonoBehaviour
{
    public InfoLoader infoLoader;
    public Client client;
    public LevelController levelController;
    public DataSaver dataSaver;
    public TransformStorer transformStorer;
    public ArenaLayout arenaLayout;
    public StatTracker statTracker;
    public bool noisyLevel;
    private int currentRound;
    private float timerToConnect = 0f;
    private float timeout = 30f;
    private int TOTAL_CALIB = 10; // the number of TRs that will be used for calibration
    private bool GotFinalEnd; // ends run not round
    // Use this for initialization
    void Start()
    {
        currentRound = 0;
        levelController.enabled = false;
        GotFinalEnd = false;
    }

    void EndGame()
    {
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
             Application.Quit();
#endif
    }

    public void SetInfo()
    {
        client.enabled = false;
        client.TryToConnect = false;
        // Start by getting info from the loader & passing on to whoever needs it
        if (infoLoader.pythonCommunicator)
        {
            client.TryToConnect = true;
            client.enabled = true;

        }
        dataSaver.outpath = infoLoader.baseDirectory;
        Directory.CreateDirectory(dataSaver.outpath);
        levelController.pause = true;
        levelController.MoveMode = infoLoader.moveType;
        levelController.currentRound = currentRound;
        if (levelController.MoveMode == "ScannerMove")
        {
            levelController.currentLevelGame = infoLoader.level; // this is for staircasing
            levelController.currentLevel = infoLoader.run;
        }
        else
        {
            levelController.currentLevel = infoLoader.run;

        }
        levelController.maxSuccesses = infoLoader.difficulty + 4;
        levelController.usingClient = infoLoader.pythonCommunicator;
        levelController.commPath = infoLoader.inputDirectory;
        levelController.noisyLevel = noisyLevel;
        levelController.difficulty = infoLoader.difficulty;
        levelController.fileCounter = infoLoader.fileCounter;
        levelController.TOTAL_CALIB = TOTAL_CALIB;

        if (!levelController.usingClient)
        {
            levelController.enabled = true; // starts if we're not waiting to sync with python
        }


    }

    // Update is called once per frame
    void Update()
    {
        int i = 0;
        if (Input.GetKeyDown(KeyCode.Q) & !GotFinalEnd)
        {
            levelController.ReceivedQuit = true;
            if (!client.connected)
            {
                EndGame();
            }
        }

        // if we're trying to connect, wait a little while to connect before quitting.
        if (infoLoader.pythonCommunicator)
        {
            while (!client.connected)
            {
                Time.timeScale = 0;
                if (i == 0)
                {
                    levelController.enabled = false;
                }
                i++;

            }
            // enable the level controller and signal to start round
            Time.timeScale = 1;
            if (!levelController.enabled)
            {
                levelController.enabled = true;
                levelController.RestartRound = true;
            }
            // if the clent was connected but then disconnected, turn things off
            if (!client.connected & client.disconnected)
            {
                client.enabled = false;
                Time.timeScale = 0;
                levelController.enabled = false;
            }
        }
        // Pass messages between level controller and client, signaling where the level is progressing.
        if (client.connected)
        {
            if (levelController.OutgoingMessage != null)
            {
                if (levelController.OutgoingMessage.Length > 1)
                {
                    client.outgoingMessage = levelController.OutgoingMessage;
                    if (levelController.OutgoingMessage.Contains("FinalEnd"))
                    {
                        GotFinalEnd = true;
                    }
                }

            }

            // Handles the calibration timing - making sure it's paused for 10 TRs
            if (client.calibration_TR_count >= 0 && client.calibration_TR_count < TOTAL_CALIB)
            {
                levelController.calib_TR = client.calibration_TR_count;
            }
            else if (client.calibration_TR_count >= TOTAL_CALIB)
            {
                levelController.calib_TR = 100;
            }
            //if (client.incomingMessage.Contains("Connected"))
           // {
           //     levelController.calib_TR = 100;
           // }

        }
        // if the port closes, end gracefully!
        if (client.disconnected)
        {
            levelController.ReceivedQuit = true;
            GotFinalEnd = true;
        }
        levelController.OutgoingMessage = null;
        currentRound = levelController.currentRound;

    }
}

