using UnityEngine;
using System.Collections;
using UnityOSC;


public class OSCMaster : MonoBehaviour {

    OSCServer server;
    OSCClient client;

    static OSCMaster instance;

    public int port = 6000;
    OSCControllable[] controllables;

    public string defaultRemoteHost = "127.0.0.1";
    public int defaultRemotePort = 6001;

    
	void Awake()
    {
        client = new OSCClient(System.Net.IPAddress.Loopback, 7000, false);
    }

	void Start () {
        instance = this;

        server = new OSCServer(port);
        server.PacketReceivedEvent += packetReceived;
        server.Connect();

        controllables = FindObjectsOfType<OSCControllable>();
	}

    void packetReceived(OSCPacket p)
    {
        //Debug.Log("Received packet");
        OSCMessage m = (OSCMessage)p;
        string[] addSplit = m.Address.Split(new char[] { '/' });

        if (addSplit.Length != 3) return;

        string target = addSplit[1];
        string property = addSplit[2];


        
        OSCControllable c = getControllableForID(target);
        if (c == null) return;
        
        
        c.setProp(property, m.Data);
    }

    OSCControllable getControllableForID(string id)
    {
        foreach(OSCControllable c in controllables)
        {
            if (c.oscName == id) return c;
        }
        return null;
    }
	
	// Update is called once per frame
	void Update () {
        server.Update();
	}


    void OnDestroy()
    {
        server.Close();
    }

    public static void sendMessage(OSCMessage m, string host = "", int port = 0)
    {
        if (host == "") host = instance.defaultRemoteHost;
        if (port == 0) port = instance.defaultRemotePort;
        instance.client.SendTo(m,host,port);
    }

    public static void sendMessage(string address, object[] args, string host = "", int port = 0)
    {
        OSCMessage m = new OSCMessage(address);
        for (int i = 0; i < args.Length; i++)
        {
            m.Append(args[i]);
        }

        sendMessage(m, host, port);
    }
}
