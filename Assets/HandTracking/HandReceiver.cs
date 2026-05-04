using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

public class HandReceiver : MonoBehaviour
{
    public static Vector2 indexPos;
    public static Vector2 thumbPos;
    public static bool pinch;
    public static bool pinchDown { get; private set; }
    private static bool prevPinch = false;

    TcpListener listener;
    Thread thread;
    private volatile bool running = false;

    void Start()
    {
        if (FindObjectsOfType<HandReceiver>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        running = true;
        thread = new Thread(StartServer);
        thread.IsBackground = true;
        thread.Start();
    }

    void Update()
    {
        pinchDown = pinch && !prevPinch;
        prevPinch = pinch;
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDisable()
    {
        Shutdown();
    }

    void Shutdown()
    {
        running = false;
        try { listener?.Stop(); } catch {}
        try { thread?.Interrupt(); } catch {}
    }

    void StartServer()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, 5055);
            listener.Start();

            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { break; }

                var reader = new StreamReader(client.GetStream());

                while (running)
                {
                    try
                    {
                        string line = reader.ReadLine();
                        if (line == null) break;

                        HandData data = JsonUtility.FromJson<HandData>(line);
                        indexPos = new Vector2(data.index_x, data.index_y);
                        thumbPos = new Vector2(data.thumb_x, data.thumb_y);
                        pinch = data.pinch;
                    }
                    catch (ThreadInterruptedException) { break; }
                    catch (IOException) { break; }
                }

                client.Close();
            }
        }
        catch (ThreadAbortException) { }
        finally
        {
            listener?.Stop();
        }
    }

    public static bool PinchDown()
    {
        return pinchDown;
    }

    [System.Serializable]
    public class HandData
    {
        public float index_x;
        public float index_y;
        public float thumb_x;
        public float thumb_y;
        public bool pinch;
    }
}