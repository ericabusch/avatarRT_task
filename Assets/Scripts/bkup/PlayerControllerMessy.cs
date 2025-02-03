using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class PlayerControllerMessy : MonoBehaviour
{
    // Reference to the Animator component of the player
    public Animator anim;
    public Collider col;
    public Rigidbody rb;
    public string commPath; // can be for either scanner comm or joystick comm
    public LineRenderer line;
    private Vector3[] linePositions;
    public PathInstantiator pathInstance;
    public GameObject targetPrefab;
    public Vector3 startPos;
    private int currentPoint;
    static Vector3 currentPosHolder;
    public Vector3 flagLocation;
    public List<Vector3> PointPath;
    public int fileCounter;
    public float input_v;
    public float decodedAngle = 0;
    private float decodedAngle_prev;
    private float input_v_prev;
    private Quaternion rotationPrev;
    private Quaternion rotation;
    private float deltaH;
    private float deltaV;
    public Vector3 currentPosition;
    private float currentFacingDirection;
    public Dictionary<float, float> FacingDirections;
    public Dictionary<float, Vector3> Positions;

    #region Variables interfacing with the animator
    [Header("Animation Speed Settings")]
    public float walkSpeed = 1f; // this is what's being adjusted constantly


    // WHAT DO ANY OF THESE SPECIFICALLY DO TO THE MOTION
    public float rotateDegrees = 180f; // rotate this amount
    public float smoothTime = 0.5f; // over ths amount of time
    public float velocity = 1f; // DONT TOUCH THIS
    public float startTime;

    public float rate;
    public float rot;

    #endregion
    public bool RoundEnd;
    //public delegate void TriggerCallback(bool GOOD);
    //public TriggerCallback ScoreCallback;

    public delegate void MoveDelegate();
    public MoveDelegate PlayerMovement;

    private void OnEnable()
    {
        currentPoint = 0;
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        //fileCounter = 1;
        startTime = Time.time;
        RoundEnd = false;
        Positions = new Dictionary<float, Vector3>();
        FacingDirections = new Dictionary<float, float>();
        rotationPrev = Quaternion.identity;
        input_v_prev = 0f;
        decodedAngle_prev = 0f;
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
        Vector3 prevCurrentPosition = currentPosition;
        input_v_prev = deltaV;
        decodedAngle_prev = deltaH;
        rotationPrev = rotation;

        PlayerMovement?.Invoke();

        // check what has changed.
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
        if (Vector3.Distance(prevCurrentPosition, currentPosition) > 0.008)
        {
            Positions.Add(Time.time, currentPosition);
        }


        // check if we're at the end
        if (Vector3.Distance(currentPosition, flagLocation) < 1)
        {
            RoundEnd = true;
        }

    }

    public void AutoMove()
    {
        CheckPoint();
        anim.SetFloat("animSpeed", walkSpeed);
        var dist = Vector3.Distance(transform.position, currentPosHolder);

        if (dist > 1) // check where we are. if we're not at the coin, move toward it
        {
            Quaternion idealRotation = GetIdealRotation();
            transform.rotation = idealRotation; //* noise; //Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * walkSpeed);
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

    public void KeypressMove()
    {
        // getting input through unity.
        if (commPath.Contains("None"))
        {
            input_v = Input.GetAxis("Vertical"); // forward/back
            decodedAngle = Input.GetAxis("Horizontal"); // left/right
        }
        else
        {
            // read in the next available file 
            string path = string.Format("{0}/{1}.txt", commPath, fileCounter);
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    var line = sr.ReadLine();
                    decodedAngle = float.Parse(line.Split(',')[0]);
                    input_v = float.Parse(line.Split(',')[1]);
                    if (decodedAngle != 0) { print(decodedAngle); }

                }
                fileCounter++;

            }
            catch (Exception) // if the file doesn't exist yet, continue as previous update
            {
                decodedAngle = decodedAngle_prev;
                input_v = input_v_prev;
                print(string.Format("File {0} DNE; interpolating using input_v: {1} & decodedAngle: {2}", fileCounter, input_v, decodedAngle));
            }
        }
        // now we've set decodedAngle and input_v; update movement
        if (decodedAngle != 0)
        {
            // do some smoothing
            decodedAngle = Mathf.SmoothDamp(decodedAngle, 0, ref velocity, smoothTime);
        }
        transform.Rotate(0, decodedAngle * rotateDegrees * Time.deltaTime, 0);
        anim.SetFloat("vertical", input_v);
        anim.SetFloat("animSpeed", walkSpeed);
    }

    public void JoystickMove()
    {
        CheckPoint();

        input_v = Input.GetAxis("JoyY"); // forward/back
        decodedAngle = Input.GetAxis("JoyX"); // left/right
        transform.Rotate(0, decodedAngle * rotateDegrees * Time.deltaTime, 0);
        anim.SetFloat("vertical", input_v);
        anim.SetFloat("animSpeed", walkSpeed);
    }


    //public void JoystickMoveFilereader()
    //{
    //    CheckPoint();
    //    anim.SetFloat("animSpeed", walkSpeed);

    //    var dist = Vector3.Distance(transform.position, currentPosHolder); // how far are we from the current target?
    //    string path = string.Format("{0}/{1}.txt", commPath, fileCounter); // get the file for this update.
    //    if (File.Exists(path))
    //    {
    //        try
    //        {
    //            using (StreamReader sr = new StreamReader(path))
    //            {
    //                string line = sr.ReadLine();
    //                decodedAngle = float.Parse(line.Split(',')[0]); // file is going to have two values: decoded angle and amount game control (confidence)
    //                input_v = float.Parse(line.Split(',')[1]);
    //            }
    //            fileCounter++;
    //        }
    //        catch (Exception)
    //        {
    //            input_v = input_v_prev;// * 0.99f;
    //            decodedAngle = decodedAngle_prev;// * 0.99f;
    //        }
    //    }
    //    else
    //    {
    //        print(string.Format("No file {0}; using previous", fileCounter));
    //        input_v = input_v_prev * 0.99f;
    //        decodedAngle = decodedAngle_prev * 0.99f;
    //    }
    //    // DO YOU WANT CONSTANT ANGLE CHANGE TO BE CONSIDERED?
    //    // E.G. IF SOMEONE IS ONLY HOLDING RIGHT, DO YOU WANT THEM TO SPIN IN CIRCLES?
    //    if (dist > 1) // check where we are and move if we're not done
    //    {
    //        var lookPos = flagLocation - transform.position; // bias towards the flag
    //        lookPos.y = 0;
    //        var newRotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
    //        float error = newRotation.eulerAngles.y * decodedAngle * (1f - -0.1f);
    //        newRotation *= Quaternion.Euler(0f, error, 0f);
    //        Quaternion offset = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * walkSpeed);// * 0.8f);
    //        transform.rotation = offset;
    //        anim.SetFloat("vertical", input_v);
    //        anim.SetFloat("animSpeed", walkSpeed);
    //        print(string.Format("File {0}, rotation {1}", fileCounter, transform.rotation));
    //    }
    //}

    // this searches for a file and reads in the directional update 
    //public void ScannerMove()
    //{
    //    CheckPoint();
    //    anim.SetFloat("animSpeed", walkSpeed);
    //    var dist = Vector3.Distance(transform.position, currentPosHolder); // how far are we from the current target?
    //    string path = string.Format("{0}/scanner_output_{1}.txt", commPath, fileCounter); // get the file for this update.

    //    rotationPrev = GetIdealRotation();
    //    float currSpeed = 0f;
    //    try
    //    {
    //        using (StreamReader sr = new StreamReader(path))
    //        {
    //            currSpeed = walkSpeed;
    //            string line = sr.ReadLine();
    //            decodedAngle = float.Parse(line.Split(',')[0]); // file is going to have two values: decoded angle and amount game control (confidence)
    //            float gameControl = float.Parse(line.Split(',')[1]);
    //            if (dist > 1) // check where we are. if we're not at the coin, move toward it
    //            {
    //                var ideal = currentPosHolder - transform.position; // vector between current location and goal
    //                Vector3 norm = Vector3.Cross(ideal, Vector3.up).normalized;
    //                var tpoint = currentPosHolder; //* decodedAngle * 90f * (1f - gameControl);
    //                print(string.Format("Decoded error: {0}, Weighting: {1},  fileCounter: {2}, idealPoint: {3}, tPoint: {4}", decodedAngle, gameControl, fileCounter, currentPosHolder, tpoint));
    //                var lookPos = tpoint - this.transform.position; // find transform between the next coin and our current position
    //                lookPos.y = 0; // there is an offset because coins appear 0.5 off the ground
    //                var rotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
    //                Quaternion idealRotation = Quaternion.Slerp(this.transform.rotation, rotation, Time.deltaTime * walkSpeed); // smooth interpolation of angles between two positions
    //                Debug.DrawRay(this.transform.position, currentPosHolder, Color.red);
    //                transform.rotation = idealRotation;
    //                //Vector3 C = new Vector3(ideal.x, ideal.y + decodedAngle * 90, ideal.z);


    //            }
    //            else // we are at the coin 
    //            {
    //                if (currentPoint < PointPath.Count - 1) // if there are points left in the path incr
    //                {
    //                    currentPoint++;
    //                    CheckPoint();
    //                }
    //                else // otherwise we're done --> stop moving
    //                {
    //                    anim.SetFloat("vertical", 0);
    //                    anim.SetFloat("horizontal", 0);
    //                }
    //            }
    //        }
    //        fileCounter++;

    //    }
    //    catch (Exception)
    //    {
    //        // transform.rotation = Quaternion.Slerp(transform.rotation, GetIdealRotation(), Time.deltaTime * walkSpeed);
    //        //AutoMove();
    //        // Debug.DrawRay(transform.position, currentPosHolder, Color.red);
    //        anim.SetFloat("vertical", walkSpeed);
    //        anim.SetFloat("horizontal", 0);
    //        //print(string.Format("File {0} DNE; Interpolating using input_v: {1} & rotation: {2}", fileCounter, input_v, rotation));
    //        ////anim.SetFloat("vertical", input_v);
    //        ////transform.rotation = rotation;
    //    }


    //}


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("target"))
        {
            other.gameObject.SetActive(false);
            //ScoreCallback(true);
            print("Trigger");
        }
    }

    private Quaternion GetIdealRotation()
    {
        var lookPos = currentPosHolder - transform.position; // find transform between the next coin and our current position
        lookPos.y = 0; // there is an offset because coins appear 0.5 off the ground
        var rotation = Quaternion.LookRotation(lookPos); // find the quaternion between the desired position and our current position
        return rotation;
        //Quaternion idealRotation = Quaternion.Slerp(this.transform.rotation, rotation, Time.deltaTime * walkSpeed); // smooth interpolation of angles between two positions
        //Debug.DrawRay(this.transform.position, currentPosHolder, Col);
        //return idealRotation
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

        //return transform.rotation;
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
                    Quaternion rotation = GetBCIRotation(decodedAngle, confidence);

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

            //if (movementRoutine == null)
            //{
            //    //rotation = GetIdealRotation();
            //    movementRoutine = MoveRotation(rotation, 0.3f);
            //    StartCoroutine(movementRoutine);
            //}
            //transform.rotation = Quaternion.Slerp(transform.rotation, rotationPrev, Time.deltaTime * walkSpeed);
            anim.SetFloat("vertical", input_v);
            anim.SetFloat("horizontal", 0);
        }
    }
}
