using System;  
using System.Net;  
using System.Net.Sockets;  
using System.Threading;  
using System.Text;  
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Runtime.Loader;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

/*
    https://arnowelzel.de/wp/en/control-av-receivers-by-pioneer-over-the-network
*/

namespace PioPi
{

    public class SynchronousSocketClient {  


        // State object for receiving data from remote device.  
        public class StateObject {  
            // Client socket.  
            public Socket workSocket = null;  
            // Size of receive buffer.  
            public const int BufferSize = 256;  
            // Receive buffer.  
            public byte[] buffer = new byte[BufferSize];  
            // Received data string.  
            public StringBuilder sb = new StringBuilder();  
        }  

        public class AsynchronousClient {  
            // The port number for the remote device.  
            private const int port = 8102;  


            // ManualResetEvent instances signal completion.  
            private static ManualResetEvent connectDone = new ManualResetEvent(false);  
            private static AutoResetEvent sendDone = new AutoResetEvent(false);  

            private static Socket _client = null;

            public static void StopClient()
            {
                        // Release the socket.  
                    _client.Shutdown(SocketShutdown.Both);  
                    _client.Close();  
                    _isRunning = false;
            }

            private static bool _isRunning = false;

            public static bool IsRunning
            {
                get { return _isRunning; }
            }

            private static void StartClient() {  
                // Connect to a remote device.  
                try {  
                    _isRunning = true;
                    // Establish the remote endpoint for the socket.  
                    // The name of the   
                    // remote device is "host.contoso.com".  
                    IPHostEntry ipHostInfo = Dns.GetHostEntry("vsx1123.bem.lan");  
                    IPAddress ipAddress = ipHostInfo.AddressList[0];  
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);  

                    // Create a TCP/IP socket.  
                    _client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);  

                    // Connect to the remote endpoint.  
                    _client.BeginConnect( remoteEP, new AsyncCallback(ConnectCallback), _client);  
                    connectDone.WaitOne();  


                    // Create the state object.  
                    StateObject state = new StateObject();  
                    state.workSocket = _client;  

                    // Begin receiving the data from the remote device.  
                    _client.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);  

                    // Send test data to the remote device.  POWERSTATUS
                    Send("?P\r\n");  
                    sendDone.WaitOne();  

                    // Send test data to the remote device.  AUDIOINFO
                    Send("?AST\r\n");  
                    sendDone.WaitOne();  

                    // Send test data to the remote device.  INPUTSOURCE
                    Send("?F\r\n");  
                    sendDone.WaitOne();  

                    // Send test data to the remote device.  VOLUMESTATUS
                    Send("?V\r\n");  
                    sendDone.WaitOne();  

                    // Send test data to the remote device.  LISTENINGMODE
                    Send("?L\r\n");  
                    sendDone.WaitOne();  

                } catch (Exception e) {  
                    Console.WriteLine(e.ToString());  
                    _isRunning = false;
                }  
            }  

            private static void ConnectCallback(IAsyncResult ar) {  
                try {  
                    // Retrieve the socket from the state object.  
                    Socket client = (Socket) ar.AsyncState;  

                    // Complete the connection.  
                    client.EndConnect(ar);  

                    Console.WriteLine("Socket connected to {0}",  
                        client.RemoteEndPoint.ToString());  

                    // Signal that the connection has been made.  
                    connectDone.Set();  
                } catch (Exception e) {  
                    Console.WriteLine(e.ToString());  
                }  
            }  

            private static void ReceiveCallback(IAsyncResult ar ) {  
                try {  
                    // Retrieve the state object and the client socket   
                    // from the asynchronous state object.  
                    StateObject state = (StateObject) ar.AsyncState;  
                    Socket client = state.workSocket;  

                    // Read data from the remote device.  
                    int bytesRead = client.EndReceive(ar);  

                    if (bytesRead > 0) {  
                        // There might be more data, so store the data received so far.  
                        state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));  
                    }

                    if (state.sb.Length > 0)
                    {
                        int index = state.sb.ToString().IndexOf("\r\n");

                        while (index >= 0) 
                        {
                            // The response from the remote device.  
                            string response = state.sb.ToString().Substring(0,index);
                            if (state.sb.Length == index+2)
                            {
                                state.sb.Clear();
                            }
                            else
                            {
                                state.sb.Remove(0,index+2);
                            }

                            if (response.Length > 0)
                            {
                                // Signal that all bytes have been received.  
                                DataReceived(response);
                            }

                            index = state.sb.ToString().IndexOf("\r\n");
                        }

                    } 

                    // Get the rest of the data.  
                    client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);  
                    
                } catch (Exception e) {  
                    Console.WriteLine(e.ToString());  
                }  
            }  

            private static void Send(String data) {  
                // Convert the string data to byte data using ASCII encoding.  
                byte[] byteData = Encoding.ASCII.GetBytes(data);  

                // Begin sending the data to the remote device.  
                _client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), _client);  
            }  

            private static void SendCallback(IAsyncResult ar) {  
                try {  
                    // Retrieve the socket from the state object.  
                    Socket client = (Socket) ar.AsyncState;  

                    // Complete sending the data to the remote device.  
                    int bytesSent = client.EndSend(ar);  
                    //Console.WriteLine("Sent {0} bytes to server.", bytesSent);  

                    // Signal that all bytes have been sent.  
                    sendDone.Set();  
                } catch (Exception e) {  
                    Console.WriteLine(e.ToString());  
                }  
            }  


            public static IReceiver _pioVSX;

            public static int Main(String[] args) {  

                AssemblyLoadContext.Default.Unloading += SigTermEventHandler; //register sigterm event handler. Don't forget to import System.Runtime.Loader!
                Console.CancelKeyPress += CancelHandler; //register sigint event handler

                List<InputSourceProperties> inputSources = new List<InputSourceProperties>();

                IConfiguration config;
                string logFile = "";

                try
                {
                    config = new ConfigurationBuilder()
                    .AddJsonFile($"appsettings.json", optional:false, reloadOnChange:true)
                   .Build();
                    logFile = $"/home/piopi/piopi.log";
                }
                catch
                {
                    config = new ConfigurationBuilder()
                    .AddJsonFile($"/home/bem/Projects/BeM_Apps/PioPi/appsettings.json", optional:false, reloadOnChange:true)
                    .Build();
                    logFile = $"/home/bem/Projects/BeM_Apps/PioPi/piopi.log";
                }

                foreach ( string item in Enum.GetNames(typeof(PioPi.PioVSX.InputSource)))
                {

                    string name = config[$"InputSource:{item}:Name"];

                    if (name != null)
                    {
                        int volume = -1;
                        bool isWebradio = false;

                        int.TryParse(config[$"InputSource:{item}:Volume"], out volume);
                        isWebradio = ((config[$"InputSource:{item}:IsWebRadio"])?.ToLower() == "true");


                        InputSourceProperties inputSource = new InputSourceProperties();
                        inputSource.Source = (PioPi.PioVSX.InputSource)Enum.Parse(typeof(PioPi.PioVSX.InputSource), item);
                        inputSource.Name = name;
                        inputSource.Volume = volume;
                        inputSource.IsWebradio = isWebradio;

                        inputSources.Add(inputSource);

                    }

                }

                



                _pioVSX = new PioVSX(inputSources,logFile);
                _pioVSX.SendInformationEvent += InformationEventHandler;
                _pioVSX.SendResponseEvent += ResponseEventHandler;
                _pioVSX.Start();

                StartClient();  

                while (IsRunning)
                {
                    Thread.Sleep(2000);
                }

                _pioVSX.Stop();
                _pioVSX.SendInformationEvent -= InformationEventHandler;
                _pioVSX.SendResponseEvent -= ResponseEventHandler;
                _pioVSX = null;

                return 0;  
            }  

            private static void SigTermEventHandler(AssemblyLoadContext obj)
            {
                System.Console.WriteLine("Unloading...");
                StopClient();
            }

            private static void CancelHandler(object sender, ConsoleCancelEventArgs e)
            {	     
                System.Console.WriteLine("Exiting...");
                StopClient();
            }



            public static int _responseNumber = 0;
            public static void DataReceived(string data)
            {
                _pioVSX.ProcessData(data);
            }

            public static void InformationEventHandler(Object sender, InfoEventArgs e)
            {
                _responseNumber +=1;
                if (!String.IsNullOrWhiteSpace(e.Data))
                {
                    Console.WriteLine($"{_responseNumber.ToString("00000")} {DateTime.Now.ToString("yyyyMMdd.HHmmss")}: {e.Data}");  
                }
            }
            public static void ResponseEventHandler(Object sender, SendResponseEventArgs e)
            {
                if (!String.IsNullOrWhiteSpace(e.Data))
                {
                    if (e.TimeToWaitBeforeSend > 0)
                    {
                        Thread.Sleep(e.TimeToWaitBeforeSend);
                    }
                    Console.WriteLine($"       >>>> SENDDATA: {e.Data}");

                    // Send test data to the remote device.
                    Send($"{e.Data}\r\n");  
                    sendDone.WaitOne();  
                    
                }

            }

        }

    }

}