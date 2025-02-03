using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

public class Client : MonoBehaviour
{
    // TCP connection related variables
    private Thread mThread;
    string path = Directory.GetCurrentDirectory();
    public int messageCounter = 0;
    public string bicIP = "127.0.1.1";//"192.168.137.5"; //; // BIC IP -- check this with python! if there's an error, redo it.
    public string elbIP = "127.0.0.1"; // ELB IP
    public string connectionIP;
    public int connectionPort = 40000;
    IPAddress localAdd;
    TcpListener listener;
    TcpClient client;

    public bool connected;
    public bool disconnected;
    public string outgoingMessage;
    public string incomingMessage;
    public bool TryToConnect;
    public string LastMessageSent;
    public bool GotRoundUp;
    public string RoundDirname;
    public float waitTime = 0.001f;
    public int calibration_TR_count;

    // Start is called before the first frame update
    void Start()
    {
        outgoingMessage = null;
        incomingMessage = null;
        TryToConnect = false;
        LastMessageSent = null;
        GotRoundUp = false;
        calibration_TR_count = -1;
    }

    private void OnEnable()
    {
        if (TryToConnect)
        {

            if (path.Contains("watts"))
            {
                connectionIP = bicIP; //GetLocalIPAddress();
            }
            else
            {
                connectionIP = elbIP;
            }
            //connectionIP = GetLocalIPAddress();
            print(String.Format("Will try to connect to {0}:{1}", connectionIP, connectionPort));
            ThreadStart ts = new ThreadStart(GetInfo);
            mThread = new Thread(ts);
            mThread.Start();
        }
    }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                print(ip.ToString());

                return ip.ToString();
            }
        }
        throw new System.Exception("No network adapters with an IPv4 address in the system!");
    }

    private void GetInfo()
    {
        try
        {
            // starts the server
            localAdd = IPAddress.Parse(connectionIP);
            listener = new TcpListener(IPAddress.Any, connectionPort);
            listener.Start();
            client = listener.AcceptTcpClient();
            print("Connected to " + client.ToString());
            connected = true;
            SendAndReceiveData("Connected!");
        }

        catch (Exception e)
        {
            connected = false;
            Debug.Log("Connect exception " + e);
            disconnected = true;
            listener.Stop();
        }
    }

    public int SendAndReceiveData(string ToSend)
    {
        NetworkStream nwStream = null;
        byte[] buffer = new byte[client.ReceiveBufferSize];
        // Handle neat closing of the stream.
        try
        {
            nwStream = client.GetStream();
        }
        catch (InvalidOperationException)
        {
            print("Can't read stream");
            return 2;
        }
        //---receiving Data from the Host----
        int bytesRead = 0;
        try
        {
            bytesRead = nwStream.Read(buffer, 0, client.ReceiveBufferSize); //Getting data in Bytes from Python
        }
        catch (SocketException)
        {
            print("Socket no longer connected");
            return 1;
        }

        if (ToSend != null)// & messageCounter >= 1)
        {
            byte[] myWriteBuffer = Encoding.ASCII.GetBytes(ToSend); //Converting string to byte data
            nwStream.Write(myWriteBuffer, 0, myWriteBuffer.Length);
            LastMessageSent = ToSend;
        }

        // If the stream is open, read in data.
        string dataReceived = Encoding.UTF8.GetString(buffer, 0, bytesRead); //Converting byte data to string
        if (dataReceived.Length > 1)
        {
            incomingMessage = dataReceived;
            if (incomingMessage.Contains("End"))
            {
                GotRoundUp = true;
            }

            // figure out which TR of calibration we're on
            if (incomingMessage.Contains("Calib"))
            {
                print(String.Format("{0}", incomingMessage));
                string[] splitArray = incomingMessage.Split(char.Parse("_"));
                calibration_TR_count = int.Parse(splitArray[1]);
            }

            messageCounter++;

            if (incomingMessage.Contains("Quitting"))
            {
                return 3;
            }
            incomingMessage = null;

        }

        return 0;


    }

    // Update is called once per frame
    void Update()
    {
        if (outgoingMessage != null)
        {
            if (outgoingMessage.Length > 1 & string.Compare(outgoingMessage, LastMessageSent) != 0)
            {
                print(String.Format("Outgoing message: {0}", outgoingMessage));

                int status = SendAndReceiveData(outgoingMessage);

                StartCoroutine(Wait(.01f));
                if (LastMessageSent.Contains("FinalEnd") || status > 0)
                {
                    disconnected = true;
                    EndGame();
                }
                //LastMessageSent = outgoingMessage;
                //StartCoroutine(Wait(2));
            }
            // SendAndReceiveData(null);
            outgoingMessage = null;
        }


    }
    System.Collections.IEnumerator Wait(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
    }


    public void EndGame()
    {
        if (TryToConnect)
        {
            try
            {
                client.Close();

            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }

            // You must close the tcp listener
            try
            {
                listener.Stop();
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }

            mThread.Abort();
        }
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
             Application.Quit();
#endif
    }

    // So that all of this happens either when the app ends or when it receives end message

    void OnApplicationQuit()
    {
        EndGame();

    }

}
