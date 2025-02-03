using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    // Reference to the Animator component of the player
    public Animator anim;
    public Collider col;
    public Rigidbody rb;
    public string commPath; 
    public LineRenderer line;
    private Vector3[] linePositions;
    public PathInstantiator pathInstance;
    public GameObject targetPrefab;
    public Vector3 startPos;
    private int currentPoint;
    static Vector3 currentPosHolder;
    public Vector3 FLAG_LOCATION;
    public List<Vector3> PointPath;
    public int fileCounter;
    public int PrevFileCounter;
    public float TR_in_sec = 2f;
    private float MinimumPathLength;
    public Vector3 currentPosition;
    public Dictionary<float, float> FacingDirections;
    public Dictionary<float, Vector3> Positions;
    private float ThisRoundGameControl;
    public float RunningMovementSum;
    public float MinimumDistance;
    private Vector3 StartPosition;
    private Vector3 PreviousPosition;
    public float MovementError;
    public float ErrorThreshold;

    #region Variables interfacing with the animator
    [Header("Animation Speed Settings")]
    public float WALK_SPEED = 100f; 
    public float ROTATE_DEGREES = 180f; // rotate this amount
    public float SMOOTH_TIME = 0.5f; 
    public float VELOCITY = 1f; 
    #endregion

    public float startTime;
    public bool RoundEnd;
    public bool DidTimeout;

    public delegate MovementOutput MoveDelegate(float A, float B);
    public MoveDelegate PlayerMovement;

    private MovementOutput PreviousMovement;
    private IEnumerator movementRoutine = null;


    private void OnEnable()
    {

        currentPoint = 0; // start at the first point
        MovementError = 0f;
        // get the pieces of the avatar
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        startTime = Time.time; // record when we're starting
        RoundEnd = false;
        Positions = new Dictionary<float, Vector3>(); // keep track of location at each movement
        FacingDirections = new Dictionary<float, float>(); // keep track of facing directions at each movement
        if (FLAG_LOCATION != null)
        {
            linePositions = new Vector3[2]; // make new vector3 of 2 indices
            linePositions[0] = transform.position; // add starting point to it
            Vector3 lastPoint = PointPath[PointPath.Count - 1]; // get the final point
            
            linePositions[1] = new Vector3(lastPoint.x, 0f, lastPoint.z);
            line.SetPositions(linePositions); // render the line to it
        }
        PreviousMovement = ScriptableObject.CreateInstance<MovementOutput>();
        PreviousMovement.DecodedAngle = 0f;
        PreviousMovement.Input_V = WALK_SPEED;
        StartPosition = transform.position;
        PreviousPosition = StartPosition;
    }

    void CheckPoint()
    {
        // check where we're at, see if at goal
        startPos = transform.position;
        currentPosHolder = PointPath[currentPoint];
    }

    // Update is called once per frame
    void Update()
    {
        float PreviousFacingDirection = transform.rotation.y;
        float DeltaPosition;
        var A = PreviousMovement.DecodedAngle;
        var B = PreviousMovement.Input_V;

        MovementOutput CurrentMovement = PlayerMovement?.Invoke(A, B);

        float CurrentFacingDirection = transform.rotation.y;
        Vector3 CurrentPosition = transform.position;
        DeltaPosition = Vector3.Distance(PreviousPosition, CurrentPosition);

        // update the line renderer
        linePositions[0] = PreviousPosition;
        line.SetPositions(linePositions);

        // we only want to update when there's input because the animator
        // has some built-in sway that we don't want to pick up on
        float DeltaDecoded = PreviousMovement.DecodedAngle - CurrentMovement.DecodedAngle;
        float DeltaV = PreviousMovement.Input_V - CurrentMovement.Input_V;

        if (DeltaDecoded != 0) // if we have a new decoded angle
        {
            FacingDirections.Add(Time.time, CurrentFacingDirection); // record it
        } 
        if (DeltaPosition > 0.008) // if we've moved more than what would be expected by sway
        {
            Positions.Add(Time.time, CurrentPosition);
            RunningMovementSum += DeltaPosition;
            PreviousPosition = CurrentPosition;
        }
        PreviousMovement = CurrentMovement;
        // check if we're at the end
        if (Vector3.Distance(CurrentPosition, FLAG_LOCATION) < 2f) // very close to flag
        {
            RoundEnd = true;
            MovementError = CalculateMovementError(RunningMovementSum, StartPosition, CurrentPosition);
        }

    }

    private float CalculateMovementError(float ActualDistanceTraveled, Vector3 StartPosition, Vector3 EndPosition)
    {

        MinimumDistance = Vector3.Distance(StartPosition, EndPosition);
        float abs_error = ActualDistanceTraveled - MinimumDistance;
        float error = abs_error / MinimumDistance; // normalize error at end of run 
        //print(string.Format("Error: {0} | actual:{1} | ideal: {2} | threshold: {3}", error, ActualDistanceTraveled, MinimumDistance, ErrorThreshold));
        return error;
    }

    // moves automatically between points along the path at a constant rate
    public MovementOutput AutoMove(float PreviousDecodedAngle, float PreviousInputV)
    {
        CheckPoint();
        anim.SetFloat("animSpeed", WALK_SPEED);
        float Distance2Goal = Vector3.Distance(transform.position, currentPosHolder);

        if (Distance2Goal > 2) // check where we are. if we're not at the target, move toward it
        {
            Quaternion idealRotation = GetIdealRotation(); // Look toward the next Target
            transform.rotation = idealRotation;
            Debug.DrawRay(transform.position, currentPosHolder, Color.red);
            anim.SetFloat("vertical", WALK_SPEED);
        }
        else // if we're at a point on the path and there are points remaining, move toward the next point
        {
            if (currentPoint < PointPath.Count - 1)
            {
                currentPoint++;
                CheckPoint();
            }
            else // otherwise we're done, stop moving
            {
                anim.SetFloat("vertical", 0);
                anim.SetFloat("horizontal", 0);
            }
        }
        MovementOutput MVMT = ScriptableObject.CreateInstance<MovementOutput>();
        MVMT.DecodedAngle = 0f;
        MVMT.Input_V = 0f;
        return MVMT;
    }

    // move according to up/down/left/right keys
    public MovementOutput KeypressMove(float PreviousDecodedAngle, float PreviousInputV)
    {
        // getting input through unity.
        var InputVHere = Input.GetAxis("Vertical"); // forward/back
        var DecodedAngleHere = Input.GetAxis("Horizontal"); // left/right

        // now we've set decodedAngle and input_v; update movement
        if (DecodedAngleHere != 0)
        {
            // do some smoothing
            DecodedAngleHere = Mathf.SmoothDamp(DecodedAngleHere, 0, ref VELOCITY, SMOOTH_TIME);
        }

        transform.Rotate(0, DecodedAngleHere * ROTATE_DEGREES * Time.deltaTime, 0);
        anim.SetFloat("vertical", InputVHere);
        anim.SetFloat("animSpeed", WALK_SPEED);

        MovementOutput MVMT = ScriptableObject.CreateInstance<MovementOutput>();
        MVMT.DecodedAngle = InputVHere;
        MVMT.Input_V = DecodedAngleHere;
        return MVMT;
    }

    // move according to joystick input
    public MovementOutput JoystickMove(float PreviousDecodedAngle, float PreviousInputV)
    {
        CheckPoint();
        var InputVHere = Input.GetAxis("JoyY"); // forward/back
        var DecodedAngleHere = Input.GetAxis("JoyX"); // left/right
        transform.Rotate(0, DecodedAngleHere * ROTATE_DEGREES * Time.deltaTime, 0);
        anim.SetFloat("vertical", InputVHere);
        anim.SetFloat("animSpeed", WALK_SPEED);
        MovementOutput MVMT = ScriptableObject.CreateInstance<MovementOutput>();
        MVMT.DecodedAngle = InputVHere;
        MVMT.Input_V = DecodedAngleHere;
        return MVMT;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("target"))
        {
            other.gameObject.SetActive(false);
        }
    }

    // Generates the ideal rotation toward the next goal
    private Quaternion GetIdealRotation()
    {
        var lookPos = currentPosHolder - transform.position; // find transform between the target and our current position
        lookPos.y = 0; // there is an offset because targets appear 0.5 off the ground
        var LookRotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
        return LookRotation; // return this quaternion
    }

    // Generates the next rotation from scanner input, given the decoded brain value and a confidence (input through python)
    private Quaternion GetBCIRotation(Vector3 position, float decodedAngle, float confidence)
    {
        var lookPos = position - transform.position; // this is the ideal - transform between the target and the current position
        lookPos.y = 0; 
        var rotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
        // now compute the offest - the decoded value is scaled by the confidence and put into game space (from -1 to 1 range) by multiplying by 90 
        float error = decodedAngle * confidence * 90f; 
        // take the ideal and rotate toward the offset 
        rotation *= Quaternion.Euler(0f, error, 0f); 
        //print(string.Format("Decoded angle: {0}, Confidence: {1}, initial position: {2}", decodedAngle, confidence, position));
        //print(string.Format("Error: {0}, rotation: {1}, lookPos: {2}", error, rotation, lookPos));
        return rotation;
    }

    public IEnumerator MoveRotation(Quaternion endRotation, float duration)
    {
        // rotate toward the final angle over a period of time
        var startRotation = transform.rotation;
        var t = 0f;
        while (t <= 1f)
        {
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            t += Time.deltaTime / duration;
            yield return null;
        }

        transform.rotation = endRotation;
        movementRoutine = null;

    }

    public MovementOutput ScannerMove(float PreviousDecodedAngle, float PreviousInputV)
    {
        CheckPoint();// chekc if at goal location
        anim.SetFloat("animSpeed", WALK_SPEED); // setting walking speed
        var Distance2Goal = Vector3.Distance(transform.position, currentPosHolder); // how far are we from the current target?

        string path = string.Format("{0}/scanner_output_{1}.txt", commPath, fileCounter);
        // get the file for this update.
        float DecodedAngleHere = PreviousDecodedAngle;
        float InputVHere = PreviousInputV;
        try
        {
            using (StreamReader sr = new StreamReader(path))
            {
                string line = sr.ReadLine();

                // if this exists, it will get reset but saved in the context of this function
                DecodedAngleHere = float.Parse(line.Split(',')[0]); // file is going to have two values: decoded angle (-1 to 1)
                                                                    // (-1 left 1 right of current rotation where 0 would be ideal straight forward to target)

                float gameControl = float.Parse(line.Split(',')[1]); // confidence in the brain decoding (starts at 0.99) --- this is a scaling factor for decodedAngle
                                                                     // and goes down with successful learning in the brain
                                                                     //print(string.Format("Read in file {0}, decoded={1}, gameCt={2}", fileCounter, DecodedAngleHere, gameControl));
                                                                     // this changes round by round, not step by step
                if (fileCounter == PrevFileCounter)
                {
                    ThisRoundGameControl = gameControl;
                }
            }
            fileCounter++;
        }
        catch (Exception e)
        {
            // keep things set as they were from the previous update
        }

        // Now continue
        float confidence = 1f - ThisRoundGameControl; // calculate confidence

        // Check if at goal
        if (Distance2Goal > 1) // check where we are. if we're not at the goal, move toward it
        {
            if (movementRoutine == null)
            {
                Quaternion BCI_Rotation = GetBCIRotation(currentPosHolder, DecodedAngleHere, confidence);
                movementRoutine = MoveRotation(BCI_Rotation, 0.3f);
                StartCoroutine(movementRoutine);
            }
        }
        else // we're at a target
        {
            if (currentPoint < PointPath.Count - 1) // if there are points left in the path increment to next coin
            {
                currentPoint++;
                CheckPoint();
            }
            else // otherwise we're done --> stop moving
            {
                anim.SetFloat("vertical", 0);
                anim.SetFloat("horizontal", 0);
            }
        }
        anim.SetFloat("vertical", WALK_SPEED);
        anim.SetFloat("animSpeed", WALK_SPEED); // setting walking speed
        MovementOutput MVMT = ScriptableObject.CreateInstance<MovementOutput>();
        MVMT.DecodedAngle = DecodedAngleHere;
        MVMT.Input_V = InputVHere;
        return MVMT;
    }
}
