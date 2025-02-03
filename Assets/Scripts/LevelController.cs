using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    public static LevelController controller;
    public PlayerController playerScript;

    private Landscaper landscaper;
    private PathInstantiator pathInstance;


    public string dataSavePath;
    public string commPath;
    public string joystickID;
    private bool scannerMode;
    public int difficulty;
    public int fileCounter = 0;
    public int waitTime = 5;
    public int currentLevel;
    public int currentLevelGame; // used for staircase
    public int currentRound;
    public int maxSuccesses;
    public int maxRounds = 100;
    public float ErrorThreshold = 0.1f;
    public int score; // cuurrent score
    public float timeAllowed = 60f; // put baakcto 60
    public float lim = 100f;
    public bool playMode;
    public bool pause;
    public bool noisyLevel;
    public bool ReceivedQuit;
    public float startTime;
    public float roundStartTime;
    public float endTime;
    public string MoveMode = null;
    public bool usingClient; // if using client, get input from files;  if not, use built-in
    public bool started;
    public GameObject playerPrefab; // get the player
    private GameObject thisPlayer;
    public Vector3 startPosition;
    private bool QPressed;
    public bool RestartRound;
    public GameObject targetsPrefab;
    public GameObject thisTargetPath;
    public string OutgoingMessage;
    public GameObject flagPrefab;
    private GameObject thisFlag;
    private Vector3 flagLocation;
    private float ErrorRate;
    public GameObject landscapePrefab;
    private GameObject thisLandscape;

    public Camera defaultCamera;
    public GameObject canvasUI;
    public Image waitScreen;
    public TextMeshProUGUI calibrationText1;
    public TextMeshProUGUI calibrationText2;
    public TextMeshProUGUI calibrationText3;
    public TextMeshProUGUI waitText;

    public Image scorePanel;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI roundWinnerText;
    public TextMeshProUGUI messageText;

    //public ArenaLayout thisArena;

    private delegate void PlayerMovement();
    private PlayerMovement CurrentMovement;

    public DataSaver dataSaver;
    public int calib_TR = -1;
    public int TOTAL_CALIB;
    bool DidTimeout = false;
    private int DeltaControl;

    private Dictionary<int, string> calibrationMessages;
    private Dictionary<int, string> successMessages;
    private List<float> DistanceErrorRates; // keeps track of excess distance travelled over past trials
    private int TrialWindow = 2; // look just at the past two trials to staircase

    void Awake()
    {
        // check if the controller already exists; if not, this is the controller.
        if (controller == null)
        {
            DontDestroyOnLoad(gameObject);
            controller = this;
        }
        else if (controller != this) // otherwise destroy this and move on
        {
            Destroy(gameObject);
        }

    }

    void SwitchWaitScreen()
    {
        if (waitScreen.enabled)
        {
            waitScreen.enabled = false;
            waitText.enabled = false;
        }
        else
        {
            waitScreen.enabled = true;
            defaultCamera.enabled = true;
            waitText.enabled = true;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        timeAllowed += 10 * currentLevel;
        //print(string.Format("current calib TR : {0}", calib_TR));
        calibrationMessages = new Dictionary<int, string>();
        calibrationMessages.Add(0, "Waiting for input ... ");
        calibrationMessages.Add(1, "Your goal is to make the avatar move toward the flag using your brain.");
        calibrationMessages.Add(2, "The path pointing from the avatar to the flag shows you the fastest path to the goal.");
        calibrationMessages.Add(3, "To do this, you might want to explore various strategies, including changing what you are thinking about or focusing on different parts of the scene.");
        calibrationMessages.Add(4, "To do this, you might want to explore various strategies, including changing what you are thinking about or focusing on different parts of the scene.");
        calibrationMessages.Add(5, "Remember - the avatar's movement isn't  immediate. It reflects your brain activity over the past 5 seconds.");
        calibrationMessages.Add(6, string.Format("Game {0} starts in {1}", currentLevel, 4));
        calibrationMessages.Add(7, string.Format("Game {0} starts in {1}", currentLevel, 3));
        calibrationMessages.Add(8, string.Format("Game {0} starts in {1}", currentLevel, 2));
        calibrationMessages.Add(9, string.Format("Game {0} starts in {1}", currentLevel, 1));
        calibrationMessages.Add(10, string.Format("Game {0} starts in {1}", currentLevel, 0));

        DistanceErrorRates = new List<float>();
        messageText.text = "";
        // turn off the wait screen
        SwitchWaitScreen();
        // instantiate the player and set all variables in its script
        defaultCamera.enabled = false;
        pause = true;
        score = 0;
        startTime = Time.time;
        RestartRound = false;
    }


    float timer = 0f;

    private bool CalibrationScreenHack()
    {
        if (calib_TR < TOTAL_CALIB)
        {
            messageText.text = calibrationMessages[calib_TR];
            messageText.enabled = true;
            OutgoingMessage = string.Format("ReceivedCalib_{0}", calib_TR);
            return false;
        }
        else
        {
            return true;
        }
    }
    void OnEnable()
    {
        roundWinnerText.text = "";
        messageText.text = "";
        SwitchWaitScreen();

    }

    void MakePlayer()
    {
        System.DateTime dt = System.DateTime.Now;
        score = 0;

        // set up the player
        float xloc = UnityEngine.Random.Range(-lim, lim);
        float zloc = UnityEngine.Random.Range(-lim, lim);

        startPosition = new Vector3(xloc, 0, zloc);

        thisPlayer = Instantiate(playerPrefab, startPosition, Quaternion.identity);
        playerScript = thisPlayer.GetComponent<PlayerController>();

        thisTargetPath = Instantiate(targetsPrefab, startPosition, Quaternion.identity);
        pathInstance = thisTargetPath.GetComponent<PathInstantiator>();

        thisLandscape = Instantiate(landscapePrefab, startPosition, Quaternion.identity);
        landscaper = thisLandscape.GetComponent<Landscaper>();
        if (difficulty > 10)
        {
            landscaper.dist = 1;
        }
        else { landscaper.dist = 2; }

        landscaper.maxObjects = difficulty * 50 + 25;
        pathInstance.corners = landscaper.corners;
        pathInstance.enabled = false;
        pathInstance.nWayPoints = maxSuccesses;
        pathInstance.corners = landscaper.corners;
        pathInstance.startPos = startPosition + new Vector3(1f, 0, 1f);
        pathInstance.noisy = noisyLevel;

        pathInstance.enabled = true;
        landscaper.RewardLocations = pathInstance.points;

        playerScript.enabled = false;
        playerScript.PointPath = pathInstance.points;

        flagLocation = pathInstance.points[pathInstance.points.Count - 1];
        MakeTarget(flagLocation);
        playerScript.FLAG_LOCATION = flagLocation;
        playerScript.PrevFileCounter = fileCounter;
        playerScript.ErrorThreshold = ErrorThreshold;
        landscaper.SetUpObjects();
        landscaper.RenderLandscape();
        playerScript.enabled = true;
        score = 0;
        pause = false;
    }

    public void MakeTarget(Vector3 Location)
    {
        Vector3 locationToRender = new Vector3(Location.x, -1, Location.z);
        thisFlag = Instantiate(flagPrefab, locationToRender, Quaternion.identity);
    }

    void StartPlay()
    {
        OutgoingMessage = String.Format("Begin_{0}_{1}", currentRound, Time.time);
        roundStartTime = Time.time;
        roundWinnerText.text = "";
        messageText.text = "";
        pause = false;
        RestartRound = false;
        playMode = true;
        scorePanel.gameObject.SetActive(true);
        score = 0;
        String s = "Level: " + currentLevel.ToString() + "\nRound: " + currentRound.ToString();
        scoreText.text = s;
        messageText.text = "";
        defaultCamera.enabled = false;
        if (MoveMode == "ScannerMove")
        {
            StartScanner();
        }
        else if (MoveMode == "KeyboardMove")
        {
            StartKeypress();
        }
        else if (MoveMode == "JoystickMove")
        {
            StartJoystick();
        }
        else if (MoveMode == "AutoMove")
        {
            StartAutomove();
        }
        //OutgoingMessage = null;
    }

    bool readyToStart = false;
    bool startPlay = false;

    void Update()
    {
        if (MoveMode == "ScannerMove" && calib_TR != 100 && usingClient)
        {
            if (calib_TR == -1)
            {
                calib_TR = 0;
            }
            readyToStart = CalibrationScreenHack();
        }
        else
        {
            readyToStart = true;
        }

        if (readyToStart && !startPlay)
        {
            startPlay = true;
            StartPlay();
        }


        if (playerScript != null)
        {
            if (MoveMode != "ScannerMove")
            {
                SetScoreText();

            }
            else
            {
                SetScoreTextScanner();
            }
        }
        if (ReceivedQuit)
        {
            QPressed = true;
            playerScript.RoundEnd = true;
            if (MoveMode != "ScannerMove")
            {
                StartCoroutine(RoundFinished(false));
            }
            else
            {
                StartCoroutine(RoundFinishedScanner(false, "", 0));
            }

            ReceivedQuit = false;
        }
        if (roundStartTime - Time.time > timeAllowed)
        {
            playerScript.RoundEnd = true;
        }
    }
    void SetScoreTextScanner()
    {
        string FirstString = "";
        // if the round is ended, figure out how to scale game control.
        if (playerScript.RoundEnd)
        {

            bool toContinue;
            playMode = false;
            pause = true;
            float elapsed = Time.time - roundStartTime;
            string SecondString = "";
            float CurrErrorRate = playerScript.MovementError;
            float IdealDistance = playerScript.MinimumDistance;
            float ObservedDistance = playerScript.RunningMovementSum;
            print(string.Format("Ideal: {0} | observed: {1} | Error: {2}", IdealDistance, ObservedDistance, CurrErrorRate));

            DeltaControl = 0;       // keeps track of if GameControl is going up (1), staying same (0), or going down (-1) -- this gets returned to python       

            // have we finished this level?
            if ((elapsed < timeAllowed) & (currentRound >= 30))
            {
                FirstString = String.Format("Game {0} complete!\nRound {1} time elapsed: {2}", currentLevel, currentRound, elapsed.ToString());
                toContinue = false;
            }

            // look back over the window to get the average
            else if (elapsed < timeAllowed)
            {

                FirstString = string.Format("Round {0} complete!\nTime elapsed: {1}", currentRound, elapsed.ToString("F2"));
                // If we're still within trial windows, just aggregate data
                if (currentRound < TrialWindow)
                {
                    float perc = CurrErrorRate * 100f;
                    SecondString = string.Format("This round you took {0}% more steps than the shortest possible path.\nGreat work!", Mathf.RoundToInt(perc));
                    //SecondString = string.Format("\nThis shortest path in this round is {0} steps.\nThis round took you {1} steps." +
                    //        " Great work!", IdealDistance.ToString("F0"), ObservedDistance.ToString("F0"));

                    if ((perc > 25f) & (currentLevel > 1))
                    {
                        SecondString = string.Format("This round you took {0}% more steps than the shortest possible path.\nLeveling down!", Mathf.RoundToInt(perc));
                        DeltaControl = 1;
                    }

                }

                // otherwise, scale feedback accordingly
                else
                {
                    float RunningError = 0;
                    // start at the final element in the list; go backwards for the number of trials we want to consider
                    // this may run into trouble with indexing, so check this our
                    for (int i = DistanceErrorRates.Count - 1; i > DistanceErrorRates.Count - TrialWindow - 1; i--)
                    {
                        print(string.Format("Aggregating over {0} {1}", i, DistanceErrorRates[i]));
                        RunningError += DistanceErrorRates[i];
                    }
                    RunningError /= TrialWindow;

                    // This way count excess "steps" as a percentage
                    float perc = CurrErrorRate * 100f;
                    print(string.Format("average error {0}, current error {1}", RunningError, CurrErrorRate));
                    SecondString = string.Format("This round you took {0}% more steps than the shortest path.", Mathf.RoundToInt(perc));

                    // prevent the running error from being too large, so that even a huge current error counts as a success - cap at 20%
                    if (RunningError > 0.25)
                    {
                        print(string.Format("Running error was too big; capping it!"));
                        RunningError = 0.25f;
                        DeltaControl = 1;
                        SecondString += " You did worse than your record.\nLeveling down!";
                    }

                    else if (CurrErrorRate < RunningError) // if theyre within 1% of each other
                    {
                        DeltaControl = -1;
                        SecondString += " You beat your record!\nLeveling up.";

                    }

                    else if (CurrErrorRate - RunningError < 0.002)
                    {
                        DeltaControl = 0;
                        SecondString += " You matched your record!";
                    }
                    else // (CurrErrorRate - RunningError < 0)
                    {
                        DeltaControl = 1;
                        SecondString += " You did worse than your record.\nLeveling down!";
                    }
                }
                toContinue = true;
            }
            // otherwise, we're overtime, and 
            else
            {
                FirstString = String.Format("Timed out in round {0}!\nLet's try again.", currentRound);
                SecondString = "\nLeveling down!";
                toContinue = true;
                DeltaControl = 1;
            }
            // Store the error rate
            DistanceErrorRates.Add(CurrErrorRate);
            //// Set the text
            messageText.enabled = false;
            roundWinnerText.enabled = true;
            roundWinnerText.text = FirstString;
            messageText.text = "";
            //messageText.enabled = false;
            defaultCamera.enabled = true;
            pause = true;
            StartCoroutine(RoundFinishedScanner(toContinue, SecondString, CurrErrorRate)); // Start the co-routine
            playerScript.RoundEnd = false;
            //Time.timeScale = 0;
            Time.timeScale = 1f;
            fileCounter = playerScript.fileCounter;
        }
        // If the round is overtime, force it to end.
        else if (Time.time - roundStartTime > timeAllowed) // force the round to end if timed out
        {
            playerScript.RoundEnd = true;
            DidTimeout = true;
        }
        // Otherwise, just keep the counter in the corner of the screen.
        else
        {
            roundWinnerText.text = "";
            //roundWinnerText.enabled = false;
            messageText.text = "";
            //messageText.enabled = false;
            Time.timeScale = 1f;
            pause = false;
            playMode = true;
            scorePanel.gameObject.SetActive(true);
            score = 0;
            String s = "Level: " + currentLevelGame.ToString() + "\nRound: " + currentRound.ToString();
            scoreText.text = s;
        }

    }
    void SetScoreText()
    {
        // have one version of this for all modes

        if (playerScript.RoundEnd)
        {
            bool toContinue;
            playMode = false;
            float elapsed = Time.time - roundStartTime;
            string r = "";
            if ((elapsed < timeAllowed) & (currentRound >= 60))
            {
                r = String.Format("Level {0} complete!\nRound {1} time elapsed: {2}", currentLevel, currentRound, elapsed.ToString());
                toContinue = false;
            }
            else if (elapsed < timeAllowed)
            {
                r = String.Format("Round {0} complete!\nTime elapsed: {1}", currentRound, elapsed.ToString());
                toContinue = true;

            }
            else
            {
                r = String.Format("Timed out in round {0}!\nLet's try again.", currentRound);
                toContinue = true;
            }
            roundWinnerText.text = r;
            roundWinnerText.enabled = true;
            defaultCamera.enabled = true;
            pause = true;
            StartCoroutine(RoundFinished(toContinue));
            playerScript.RoundEnd = false;
            //Time.timeScale = 0;
            Time.timeScale = 1f;
            fileCounter = playerScript.fileCounter;

        }
        else if (Time.time - roundStartTime > timeAllowed) // force the round to end if timed out
        {
            playerScript.RoundEnd = true;
            DidTimeout = true;
        }
        else
        {
            roundWinnerText.text = "";
            //roundWinnerText.enabled = false;
            Time.timeScale = 1f;
            pause = false;
            playMode = true;
            scorePanel.gameObject.SetActive(true);
            score = 0;
            String s = "Level: " + currentLevel.ToString() + "\nRound: " + currentRound.ToString();
            scoreText.text = s;
        }
    }
    public void StartScanner()
    {
        MakePlayer();
        playerScript.PlayerMovement = playerScript.ScannerMove;
        playerScript.commPath = commPath;
        playerScript.fileCounter = fileCounter;
        scannerMode = true;
    }
    public void StartAutomove()
    {
        // do a thing that sttarts the given behavior
        MakePlayer();
        playerScript.PlayerMovement = playerScript.AutoMove;
        playerScript.commPath = commPath;
        scannerMode = false;
    }
    public void StartKeypress()
    {
        // do a thing
        MakePlayer();
        playerScript.PlayerMovement = playerScript.KeypressMove;
        playerScript.commPath = commPath;
        scannerMode = false;
    }
    public void StartJoystick()
    {
        MakePlayer();
        playerScript.PlayerMovement = playerScript.JoystickMove;
        playerScript.commPath = commPath;
        scannerMode = false;
    }

    private IEnumerator RoundFinishedScanner(bool Continue, string SecondString, float RoundErrorRate)
    {
        float completeTime = Time.time;

        if (Continue)
        {
            OutgoingMessage = String.Format("End_{0}_{1}_{2}", currentRound, completeTime, DeltaControl);
        }
        else
        {
            OutgoingMessage = String.Format("FinalEnd_{0}_{1}_{2}", currentRound, completeTime, DeltaControl);


        }

        Dictionary<float, float> FacingDirections = playerScript.FacingDirections;
        Dictionary<float, Vector3> Positions = playerScript.Positions;
        dataSaver.Corners = landscaper.Corners;
        dataSaver.Boundaries = landscaper.boundaryPoints;
        dataSaver.Obstacles = landscaper.objectPoints;
        dataSaver.Targets = landscaper.RewardLocations;
        dataSaver.ErrorRate = RoundErrorRate;

        scorePanel.gameObject.SetActive(false);
        DestroyObjects();
        dataSaver.RoundStartTime = startTime;
        dataSaver.RoundEndTime = endTime;
        dataSaver.LevelNumber = currentLevel;
        int noise = 0;
        if (noisyLevel)
        {
            noise = 1;
        }
        dataSaver.NoisyRound = noise;
        int client = 0;
        if (usingClient)
        {
            client = 1;
        }
        dataSaver.UsingClient = client;
        dataSaver.MoveMode = MoveMode;
        dataSaver.Positions = Positions;
        dataSaver.FacingDirections = FacingDirections;
        print(string.Format("Saving current level: {0}", currentLevelGame));
        dataSaver.CurrentLevelGame = currentLevelGame;
        dataSaver.SaveRound(currentRound);
        //roundWinnerText.enabled = false;
        //roundWinnerText.text = "";

        messageText.text = SecondString;

        if (Continue)
        {
            //roundWinnerText.enabled = false;
            yield return new WaitForSeconds(1f);
            roundWinnerText.text = "";

            for (int c = 3; c > 0; c--)
            {
                messageText.text = SecondString;
                messageText.enabled = true;
                yield return new WaitForSeconds(1f);
            }
            messageText.enabled = false;
            for (int c = waitTime - 2; c > 0; c--)
            {
                roundWinnerText.text = string.Format("Next round starts in {0}", c);
                roundWinnerText.enabled = true;
                yield return new WaitForSeconds(1f);
            }
            currentRound++;
            currentLevelGame -= DeltaControl;
            if (currentLevelGame < 0)
            {
                currentLevelGame = 1;
            }
            StartPlay();
            messageText.text = "";
            roundWinnerText.text = "";
            //messageText.enabled = false;
            RestartRound = false;

        }
        else
        {
            QuitGame();
        }

    }


    private IEnumerator RoundFinished(bool Continue)
    {
        print("In coroutine");
        float completeTime = Time.time;
        if (Continue)
        {
            OutgoingMessage = String.Format("End_{0}_{1}", currentRound, completeTime);
        }
        else
        {
            OutgoingMessage = String.Format("FinalEnd_{0}_{1}", currentRound, completeTime);
        }

        Dictionary<float, float> FacingDirections = playerScript.FacingDirections;
        Dictionary<float, Vector3> Positions = playerScript.Positions;
        dataSaver.Corners = landscaper.Corners;
        dataSaver.Boundaries = landscaper.boundaryPoints;
        dataSaver.Obstacles = landscaper.objectPoints;
        dataSaver.Targets = landscaper.RewardLocations;


        scorePanel.gameObject.SetActive(false);
        DestroyObjects();
        dataSaver.RoundStartTime = startTime;
        dataSaver.RoundEndTime = endTime;
        dataSaver.LevelNumber = currentLevel;
        int noise = 0;
        if (noisyLevel)
        {
            noise = 1;
        }
        dataSaver.NoisyRound = noise;
        int client = 0;
        if (usingClient)
        {
            client = 1;
        }
        dataSaver.UsingClient = client;
        dataSaver.MoveMode = MoveMode;
        dataSaver.Positions = Positions;
        dataSaver.FacingDirections = FacingDirections;
        dataSaver.SaveRound(currentRound);

        if (Continue)
        {
            yield return new WaitForSeconds(1f);
            for (int c = waitTime; c > 0; c--)
            {
                roundWinnerText.text = string.Format("Next round starts in {0}", c);
                roundWinnerText.enabled = true;
                yield return new WaitForSeconds(1f);
            }
            currentRound++;
            StartPlay();
            RestartRound = false;

        }
        else
        {
            QuitGame();
        }

    }

    public void QuitGame()
    {
        // save any game data here
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
             Application.Quit();
#endif
    }

    private void DestroyObjects()
    {
        Destroy(thisPlayer);
        Destroy(thisTargetPath);
        Destroy(thisLandscape);
        Destroy(thisFlag);
    }
}
