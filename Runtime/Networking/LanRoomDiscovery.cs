using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Taiyo.Metaverse
{
    // LAN-only UDP discovery. The received endpoint is advertised to the user;
    // discovering a host never starts an experiment or bypasses either Start button.
    public class LanRoomDiscovery : MonoBehaviour
    {
        public bool advertise, connected;
        public ushort advertisedPort=7777;
        public string sessionCode="metaverse", queryProtocol="taiyo-metaverse-find-v1", responseProtocol="taiyo-metaverse-room-v1";
        public int discoveryPort=47778;
        public float refreshSeconds=2,expirySeconds=7;
        public sealed class Host
        {
            public string address,code;
            public ushort port;
            public float seen;
        }
        public readonly List<Host> Hosts=new List<Host>();
        public string Error {get;private set;}="";
        public bool Searching {get;private set;}
        public string LocalAddress {get;private set;}="";
        UdpClient socket;
        float nextQuery,nextOpen;
        string nonce;
        bool boundAsHost;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject multicastLock;
#endif

        protected virtual void Start()
        {
            try
            {
                foreach(var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                foreach(var unicast in adapter.GetIPProperties().UnicastAddresses)
                    if(unicast.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(unicast.Address))
                    {LocalAddress=unicast.Address.ToString();break;}
            }
            catch(Exception){LocalAddress="See Wi-Fi settings";}
        }
        public void Search(){Searching=true;Hosts.Clear();nonce=Guid.NewGuid().ToString("N");nextQuery=0;Open(false);}
        void Open(bool host)
        {
            nextOpen=Time.unscaledTime+refreshSeconds;
            socket?.Dispose();socket=null;boundAsHost=host;Error="";
            try
            {
                socket=new UdpClient(AddressFamily.InterNetwork);socket.EnableBroadcast=true;
                socket.Client.Bind(new IPEndPoint(IPAddress.Any,host?discoveryPort:0));
                socket.Client.Blocking=false;
#if UNITY_ANDROID && !UNITY_EDITOR
                if(multicastLock==null)
                {
                    using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using(var context=activity.Call<AndroidJavaObject>("getApplicationContext"))
                    using(var wifi=context.Call<AndroidJavaObject>("getSystemService","wifi"))
                    {
                        multicastLock=wifi.Call<AndroidJavaObject>("createMulticastLock","Metaverse LAN");
                        multicastLock.Call("setReferenceCounted",false);multicastLock.Call("acquire");
                    }
                }
#endif
            }
            catch(Exception e){Error=e.Message;socket?.Dispose();socket=null;}
        }
        protected virtual void Update()
        {
            bool host=advertise;
            if((host||Searching&&!connected)&&(socket==null||host&&!boundAsHost)&&Time.unscaledTime>=nextOpen)Open(host);
            if(!host&&boundAsHost){socket?.Dispose();socket=null;boundAsHost=false;}
            if(socket==null)return;
            try
            {
                if(Searching&&!host&&!connected&&Time.unscaledTime>=nextQuery)
                {
                    nextQuery=Time.unscaledTime+refreshSeconds;
                    Send(queryProtocol+"|"+nonce,new IPEndPoint(IPAddress.Broadcast,discoveryPort));
                    Send(queryProtocol+"|"+nonce,new IPEndPoint(IPAddress.Loopback,discoveryPort));
                }
                for(int n=0;n<12&&socket.Available>0;n++)
                {
                    IPEndPoint sender=new IPEndPoint(IPAddress.Any,0);byte[] bytes=socket.Receive(ref sender);
                    if(bytes.Length>320)continue;
                    var fields=Encoding.UTF8.GetString(bytes).Split('|');
                    if(host&&fields.Length==2&&fields[0]==queryProtocol&&fields[1].Length==32)
                        Send(responseProtocol+"|"+fields[1]+"|"+advertisedPort+"|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(sessionCode)),sender);
                    else if(!host&&Searching&&fields.Length==4&&fields[0]==responseProtocol&&fields[1]==nonce&&ushort.TryParse(fields[2],out ushort port)&&port>0)
                    {
                        string code;
                        try{code=Encoding.UTF8.GetString(Convert.FromBase64String(fields[3]));}catch(FormatException){continue;}
                        if(code.Length<1||code.Length>48)continue;
                        string address=sender.Address.ToString();var entry=Hosts.Find(h=>h.address==address&&h.port==port);
                        if(entry==null){if(Hosts.Count>=16)continue;entry=new Host{address=address,port=port};Hosts.Add(entry);}
                        entry.code=code;entry.seen=Time.unscaledTime;
                    }
                }
                Hosts.RemoveAll(h=>Time.unscaledTime-h.seen>expirySeconds);
            }
            catch(SocketException e){if(e.SocketErrorCode!=SocketError.WouldBlock)Error=e.Message;}
        }
        void Send(string message,IPEndPoint endpoint){byte[] bytes=Encoding.UTF8.GetBytes(message);socket.Send(bytes,bytes.Length,endpoint);}
        public void Stop()
        {
            socket?.Dispose();socket=null;Searching=false;advertise=false;boundAsHost=false;Hosts.Clear();
#if UNITY_ANDROID && !UNITY_EDITOR
            if(multicastLock!=null){multicastLock.Call("release");multicastLock.Dispose();multicastLock=null;}
#endif
        }
        protected virtual void OnDisable() => Stop();
        protected virtual void OnDestroy() => Stop();
    }
}
