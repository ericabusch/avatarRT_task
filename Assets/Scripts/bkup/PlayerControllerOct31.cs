using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class PlayerControllerOct31 : MonoBehaviour
{
    // Reference to the Animator component of the player
    public Animator anim;
    public Collider col;
    public Rigidbody rb;
    public string commPath; // Path where scanner output is written
    public LineRenderer line;
    private Vector3[] linePositions;
    public PathInstantiator pathInstance;
    public GameObject targetPrefab;
    public Vector3 startPos;
    public Vector3 flagLocation; // where is the goal location?
    public List<Vector3> PointPath; // list of waypoints toward the goal
    public Vector3 currentPosition; // tracks avatar's current possition
    public Dictionary<float, float> FacingDirections; // tracks what direction the avatar is looking
    public Dictionary<float, Vector3> Positions; // tracks the avatar's location in space after each movement
    public float rotateDegrees = 180f; // rotate this amount
    public float smoothTime = 0.5f; // over ths amount of time
    public float velocity = 1f; // DONT TOUCH THIS
    public float startTime;
    public bool RoundEnd; // has the round completed?
    public delegate void MoveDelegate(); // what is the movement type?
    public MoveDelegate PlayerMovement;
    public int fileCounter; // this essentially keeps track of how many TRs we're at
    public float MovementError;
    public float MinimumPathLength;
    public float TR = 0.3f;

    private float input_v; // input in the vertical direction
    private float decodedAngle = 0;

    static Vector3 currentPosHolder; // the nearest waypoint from the point path
    private int currentPoint; // which point in the point path we're currently reached
    private Quaternion rotation;
    private float deltaH; // tracks change in the horizontal direction
    private float deltaV; // tracks change in the vertical dimension
    private float currentFacingDirection; // tracks where the avatar is currently facing
    private Vector3 AvatarRenderPosition; // Where does the avatar start?
    private float RunningDistanceTracker;
    private Quaternion rotationPrev;
    private float input_v_prev;
    private float decodedAngle_prev;

    #region Variables interfacing with the animator
    [Header("Animation Speed Settings")]
    public float walkSpeed = 1f; // this is what's being adjusted constantly
    #endregion

    private void OnEnable()
    {
        currentPoint = 0;
        RunningDistanceTracker = 0; // this is going to keep track of the distance traveled by avatar
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        startTime = Time.time;
        RoundEnd = false;
        Positions = new Dictionary<float, Vector3>();
        FacingDirections = new Dictionary<float, float>();
        rotationPrev = Quaternion.identity;
        input_v_prev = 0f;
        decodedAngle_prev = 0f;
        AvatarRenderPosition = transform.position;

        if (flagLocation != null)
        {
            linePositions = new Vector3[2]; // make new vector3 of 2 indices
            linePositions[0] = transform.position; // add starting point to it
            Vector3 lastPoint = PointPath[PointPath.Count - 1]; // get the final point
            linePositions[1] = new Vector3(lastPoint.x, 0f, lastPoint.z);
            line.SetPositions(linePositions); // render the line to it
        }
    }

    void CheckPoint()
    {
        startPos = transform.position;
        currentPosHolder = PointPath[currentPoint];
    }

    // Update is called once per frame
    void Update()
    {
        // save the previous states
        Vector3 prevCurrentPosition = currentPosition;
        input_v_prev = deltaV;
        decodedAngle_prev = deltaH;
        rotationPrev = rotation;

        PlayerMovement?.Invoke(); // do the movement

        // check what has changed after invoking movement.
        float deltaHPrev = deltaH;

        if (decodedAngle == 0)
        {
            decodedAngle = transform.rotation.y;
        }

        deltaH = decodedAngle;
        deltaV = input_v;
        currentFacingDirection = transform.rotation.y;
        currentPosition = transform.position;

        // update the line renderer
        linePositions[0] = currentPosition;
        line.SetPositions(linePositions);


        // we only want to update when there's input because the animator
        // has some built-in sway that we don't want to pick up on
        if (deltaH != 0 & deltaHPrev != currentFacingDirection)
        {
            FacingDirections.Add(Time.time, currentFacingDirection);
        }
        if (Vector3.Distance(prevCurrentPosition, currentPosition) > 0.008) // again, the avatar has some built in sway,
                                                                            // so we want to make sure we really moved
        {
            Positions.Add(Time.time, currentPosition);
            RunningDistanceTracker += Vector3.Distance(prevCurrentPosition, currentPosition); // adds on the distance moved in this update
        }


        // check if we're at the end
        if (Vector3.Distance(currentPosition, flagLocation) < 1)
        {
            // we made it to the goal; now lets calculate the error in this movement
            MovementError = CalculateMovementError(RunningDistanceTracker, AvatarRenderPosition, flagLocation);
            print(string.Format("Movement error: {0}", MovementError));
            RoundEnd = true; // this gets read back in by level controller
        }

    }

    // this function takes in the distance the avatar actually traveled between steps (a runnings sum) 
    // then compares it to the shortest possible distance the avatar could travel to the goal, and returns that error term.
    private float CalculateMovementError(float ActualDistanceTraveled, Vector3 AvatarRenderPosition, Vector3 EndPosition)
    {
        // error is the (MeasuredDistance - TrueDistance) / TrueDistance
        float MinimalDistanceToTravel = Vector3.Distance(AvatarRenderPosition, EndPosition) * 2f;
        float MovementError = ActualDistanceTraveled - MinimalDistanceToTravel;
        return MovementError / MinimalDistanceToTravel;
    }

    // automatically move at a constant rate toward the goal location
    public void AutoMove()
    {
        CheckPoint();
        anim.SetFloat("animSpeed", walkSpeed);
        var dist = Vector3.Distance(transform.position, currentPosHolder);

        if (dist > 1) // check where we are. if we're not at the coin, move toward it
        {
            Quaternion idealRotation = GetIdealRotation();
            transform.rotation = idealRotation;
            Debug.DrawRay(transform.position, currentPosHolder, Color.red);
            input_v = walkSpeed;
            anim.SetFloat("vertical", input_v);
        }
        else
        {
            if (currentPoint < PointPath.Count - 1)
            {
                currentPoint++;
                CheckPoint();
            }
            else
            {
                anim.SetFloat("vertical", 0);
                anim.SetFloat("horizontal", 0);
            }
        }

    }

    // Move via the forward/backward keypresses
    public void KeypressMove()
    {
        // Get input through the keypresses on a standard keyboard
        input_v = Input.GetAxis("Vertical"); // forward/back
        decodedAngle = Input.GetAxis("Horizontal"); // left/right - this is called "decoded angle" for consistence with ScannerMove
        transform.Rotate(0, decodedAngle * rotateDegrees * Time.deltaTime, 0); // rotate it in the direction over time
        anim.SetFloat("vertical", input_v);
        anim.SetFloat("animSpeed", walkSpeed);
    }

    // Move via input from a joystick
    public void JoystickMove()
    {
        // Get input through the keypresses on a standard joystick
        CheckPoint();
        input_v = Input.GetAxis("JoyY"); // forward/back
        decodedAngle = Input.GetAxis("JoyX"); // left/right
        transform.Rotate(0, decodedAngle * rotateDegrees * Time.deltaTime, 0); // rotate it in the direction over time
        anim.SetFloat("vertical", input_v);
        anim.SetFloat("animSpeed", walkSpeed);
    }

    // This function would be useful if we were still doing the coin retrieval game -- keep it here
    // for archiving in case we go back to that version.
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("target"))
        {
            other.gameObject.SetActive(false);
            //ScoreCallback(true);
            print("Trigger");
        }
    }

    // Get the rotation that would point us ideally toward the next goal location.
    private Quaternion GetIdealRotation()
    {
        var lookPos = currentPosHolder - transform.position; // find transform between the next coin and our current position
        lookPos.y = 0; // there is an offset because coins appear 0.5 off the ground
        var rotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
        return rotation;
    }

    private Quaternion GetBCIRotation(float decodedAngle, float confidence)
    {
        var lookPos = currentPosHolder - transform.position;
        // at this point, it is moving left when want negative number and moving right when positidealRotation       var lookPos = currentPosHolder - this.transform.position; // find transform betweenidealRotationext coin and our current position
        lookPos.y = 0; // there is an offset because coins appear 0.5 off the ground
        var rotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
        float error = decodedAngle * confidence * 45f;
        rotation *= Quaternion.Euler(0f, error, 0f);
        print(string.Format("Decoded angle: {0}, Confidence: {1}, error {2}", decodedAngle, confidence, error));
        return rotation;

    }

    public IEnumerator MoveRotation(Quaternion endRotation, float duration)
    {
        var startRotation = transform.rotation;
        var t = 0f;
        while (t <= 1f)
        {
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            t += Time.deltaTime / duration;
            yield return null;
        }

        transform.rotation = endRotation;

    }
    IEnumerator movementRoutine = null;

    public void ScannerMove()
    {
        CheckPoint();// chekc if at goal location
        anim.SetFloat("animSpeed", walkSpeed); // setting walking speed
        input_v_prev = input_v; // keeping track of previous forward/back movement (forward 1 none 0 back -1) -- doesn't matter here
        // for scanner input, set to walkSpeed when it reads first file
        decodedAngle_prev = decodedAngle; // keep track of decoded angle from previous update
        var dist = Vector3.Distance(transform.position, currentPosHolder); // how far are we from the current target?
        //rotationPrev = GetIdealRotation();
        string path = string.Format("{0}/scanner_output_{1}.txt", commPath, fileCounter); // get the file for this update.
        try
        {
            using (StreamReader sr = new StreamReader(path))
            {

                string line = sr.ReadLine();
                decodedAngle = float.Parse(line.Split(',')[0]); // file is going to have two values: decoded angle (-1 to 1)
                                                                // (-1 left 1 right of current rotation where 0 would be ideal straight forward to target)

                float gameControl = float.Parse(line.Split(',')[1]); // confidence in the brain decoding (starts at 0.99) --- this is a scaling factor for decodedAngle
                                                                     // and goes down with successful learning in the brain
                float confidence = 1f - gameControl;
                //StopCoroutine(movementRoutine);
                movementRoutine = null;

                if (dist > 1) // check where we are. if we're not at the coin, move toward it
                {
                    //CALCULATE ERROR BETWEEN IDEAL AND DECODED VALUES
                    input_v = walkSpeed;
                    Quaternion rotation = GetIdealRotation();
                    //Quaternion rotation = GetBCIRotation(decodedAngle, confidence);

                    if (movementRoutine == null)
                    {
                        movementRoutine = MoveRotation(rotation, 0.3f);
                        StartCoroutine(movementRoutine);
                    }

                    //transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * walkSpeed);
                    anim.SetFloat("vertical", input_v);
                    //transform.rotation = rotation;
                    //Debug.DrawRay(transform.position, currentPosHolder, Color.red);
                    rotationPrev = rotation;
                    // decodedAngle controls the angle of movement. Now we need to set the magnitude.


                }
                else // we are at the coin 
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
            }
            fileCounter++;
        }
        catch (Exception e)
        {

            // TODO EB: set the 0.3 to a global parameter read in from config file

            if (movementRoutine == null)
            {
                rotation = GetIdealRotation();
                movementRoutine = MoveRotation(rotation, 0.3f);
                StartCoroutine(movementRoutine);
            }
            //transform.rotation = Quaternion.Slerp(transform.rotation, rotationPrev, Time.deltaTime * walkSpeed);
            anim.SetFloat("vertical", input_v);
            anim.SetFloat("horizontal", 0);
        }
    }
}



