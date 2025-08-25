using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class GestureReceiver : MonoBehaviour
{
    public int listenPort = 5066;
    public volatile int gesture = 0; 

    UdpClient udpClient;
    Thread receiveThread;

    void Start()
    {
        udpClient = new UdpClient(listenPort);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, listenPort);
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                if (data.Length > 0)
                {
                    gesture = data[0]; 
                }
            }
            catch (System.Exception err)
            {
                Debug.LogError(err.ToString());
            }
        }
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        udpClient.Close();
    }
}