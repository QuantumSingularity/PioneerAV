using System;  
using System.Net;  
using System.Net.Sockets;  
using System.Threading;  
using System.Text;  
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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



            private static void StartClient() {  
                // Connect to a remote device.  
                try {  
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



                    // Send test data to the remote device.  VOLUMESTATUS
                    Send("?V\r\n");  
                    sendDone.WaitOne();  

                    // Send test data to the remote device.  POWERSTATUS
                    Send("?P\r\n");  
                    sendDone.WaitOne();  

                    // Send test data to the remote device.  AUDIOINFO
                    Send("?AST\r\n");  
                    sendDone.WaitOne();  


                    /*
                    bool ok = true;
                    while (ok)
                    {
                        Thread.Sleep(1000);
                    }
                    */
                    
                    // Wait for [ENTER]
                    Console.ReadLine();

                    // Release the socket.  
                    _client.Shutdown(SocketShutdown.Both);  
                    _client.Close();  

                } catch (Exception e) {  
                    Console.WriteLine(e.ToString());  
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


            public static PioVSX _pioVSX;

            public static int Main(String[] args) {  

                _pioVSX = new PioVSX();
                _pioVSX.SendInformationEvent += InformationEventHandler;
                _pioVSX.SendResponseEvent += ResponseEventHandler;

                StartClient();  

                _pioVSX.SendInformationEvent -= InformationEventHandler;
                _pioVSX.SendResponseEvent -= ResponseEventHandler;
                _pioVSX = null;

                return 0;  
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
                    Console.WriteLine($"{_responseNumber.ToString()}: {e.Data}");  
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