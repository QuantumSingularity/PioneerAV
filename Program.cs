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

        public static string _lastSong = "";

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
        private static ManualResetEvent connectDone =   
            new ManualResetEvent(false);  
        private static ManualResetEvent sendDone =   
            new ManualResetEvent(false);  
        private static ManualResetEvent receiveDone =   
            new ManualResetEvent(false);  

        // The response from the remote device.  
        private static String response = String.Empty;  

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
                Socket client = new Socket(ipAddress.AddressFamily,  
                    SocketType.Stream, ProtocolType.Tcp);  

                // Connect to the remote endpoint.  
                client.BeginConnect( remoteEP, new AsyncCallback(ConnectCallback), client);  
                connectDone.WaitOne();  

                // Send test data to the remote device.  
                Send(client,"?V\r\n");  
                sendDone.WaitOne();  

                // Send test data to the remote device.  
                Send(client,"?P\r\n");  
                sendDone.WaitOne();  


                bool ok = true;
                int responseNumber = 0;
                while (ok)
                {
                    // Receive the response from the remote device.  
                    Receive(client);  
                    receiveDone.WaitOne();  
                    // Write the response to the console.  
                    responseNumber +=1;
                    string newResponse = DecodeResponse(response, client);
                    if (!String.IsNullOrWhiteSpace(newResponse))
                    {
                        Console.WriteLine($"{responseNumber.ToString()}: {newResponse}");  
                    }
                }

                // Release the socket.  
                client.Shutdown(SocketShutdown.Both);  
                client.Close();  

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

        private static void Receive(Socket client) {  
            try {  
               
                receiveDone.Reset();

                // Create the state object.  
                StateObject state = new StateObject();  
                state.workSocket = client;  

                // Begin receiving the data from the remote device.  
                client.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0,  
                    new AsyncCallback(ReceiveCallback), state);  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  

        private static void ReceiveCallback( IAsyncResult ar ) {  
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
                    if (index >= 0) 
                    {
                        response = state.sb.ToString().Substring(0,index);
                        if (state.sb.Length == index+2)
                        {
                            state.sb.Clear();
                        }
                        else
                        {
                            state.sb.Remove(0,index+2);
                        }
                        // string x = state.sb.ToString();

                        if (response.Length > 0)
                        {
                            // Signal that all bytes have been received.  
                            receiveDone.Set();
                        }
                        else
                        {
                            // Get the rest of the data.  
                            client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);  
                        }

                        //index = state.sb.ToString().IndexOf("\r\n");
                    }
                    else
                    {
                        // Get the rest of the data.  
                        client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);  
                    }                
                } 
                else 
                {  
                        // Get the rest of the data.  
                        client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);  
                    // None - Wait
                    //response = "";
                }  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  

        private static void Send(Socket client, String data) {  
            sendDone.Reset();
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);  

            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,  
                new AsyncCallback(SendCallback), client);  
        }  

        private static void SendCallback(IAsyncResult ar) {  
            try {  
                // Retrieve the socket from the state object.  
                Socket client = (Socket) ar.AsyncState;  

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);  
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);  

                // Signal that all bytes have been sent.  
                sendDone.Set();  
            } catch (Exception e) {  
                Console.WriteLine(e.ToString());  
            }  
        }  

        public static int Main(String[] args) {  
            StartClient();  

            // string x = "7320202D204D6F74696F6E205369";
            // byte[] raw = ConvertStringToByteArray(x);
            // string y = Encoding.ASCII.GetString(raw);
            return 0;  
        }  

        //http://dotnetstock.com/technical/convert-byte-array-hexadecimal-string/
        public static byte[] ConvertStringToByteArray(String strhex)
        {
            int NumberChars = strhex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(strhex.Substring(i, 2), 16);
            return bytes;
        }


        public enum InputSource
        {
              Unknown = 0
            , BD = 25
            , DVD = 4
            , SAT_CBL = 6
            , DVR_BDR = 15
            , VIDEO_1_VIDEO = 10
            , HDMI_1 = 19
            , HDMI_2 = 20
            , HDMI_3 = 21
            , HDMI_4 = 22
            , HDMI_5 = 23
            , HDMI_6 = 24
            , HDMI_7 = 34
            , HDMI_8 = 35
            , NETWORK_cyclic = 26
            , INTERNET_RADIO = 38
            , PANDORA = 41
            , MEDIA_SERVER = 44
            , FAVORITES = 45
            , iPod_USB = 17
            , TV = 5
            , CD = 1
            , USB_DAC = 13
            , TUNER = 2
            , PHONO = 0
            , MULTI_CH_IN = 12
            , ADAPTER_PORT = 33
            , MHL = 48
            , HDMI_cyclic = 31
        }

       public enum InputAudioSignal   
        {   
            Unknown = 999
            , ANALOG = 0
            , ANALOG_2 = 1
            , ANALOG_3 = 2
            , PCM = 3
            , PCM_2 = 4
            , DOLBY_DIGITAL = 5
            , DTS = 6
            , DTS_ES_Matrix = 7
            , DTS_ES_Discrete = 8
            , DTS_96_24 = 9
            , DTS_96_24_ES_Matrix = 10
            , DTS_96_24_ES_Discrete = 11
            , MPEG_2_AAC = 12
            , WMA9_Pro = 13
            , DSD_HDMI_or_File_via_DSP_route = 14
            , HDMI_THROUGH = 15
            , DOLBY_DIGITAL_PLUS = 16
            , DOLBY_TrueHD = 17
            , DTS_EXPRESS = 18
            , DTS_HD_Master_Audio = 19
            , DTS_HD_High_Resolution = 20
            , DTS_HD_High_Resolution_2 = 21
            , DTS_HD_High_Resolution_3 = 22
            , DTS_HD_High_Resolution_4 = 23
            , DTS_HD_High_Resolution_5 = 24
            , DTS_HD_High_Resolution_6 = 25
            , DTS_HD_High_Resolution_7 = 26
            , DTS_HD_Master_Audio_2 = 27
            , DSD_HDMI_or_File_via_DSD_DIRECT_route = 28
            , MP3 = 64
            , WAV = 65
            , WMA = 66
            , MPEG4_AAC = 67
            , FLAC = 68
            , ALAC_Apple_Lossless = 69
            , AIFF = 70
            , DSD_USB_DAC = 71
        }   

        public static string DecodeResponse(string data,Socket client)
        {
            string result = "";
            result = data;

            // Domoticz API Call
            // https://www.domoticz.com/wiki/Domoticz_API/JSON_URL%27s
            // DeviceId == 90
            // eg. http://192.168.1.2:8080/json.htm?type=command&param=udevice&idx=$idx&nvalue=0&svalue=79
            // VSX1123-Volume        id==90
            // VSX1123-PowerStatus   id==148
            // VSX1123-CurrentSource id==149

            // PowerStatus
            if (data.StartsWith("PWR")) 
            { 
                int newStatus = 0;
                if (data == "PWR0")
                {
                    newStatus = 1; //on
                }
                else
                {
                    newStatus = 0; //off
                }
                int deviceId = 148;
                string url = $"http://domoticz.bem.lan/json.htm?type=command&param=udevice&idx={deviceId.ToString()}&nvalue={newStatus.ToString()}&svalue=";
                SendApiCall(url).Wait(1000);

            }

            // InputSource
            if (data.StartsWith("FN")) 
            {
                InputSource inputSource = InputSource.Unknown;
                if (Int32.TryParse(data.Substring(2), out int i))
                {
                    try
                    {
                        inputSource = (InputSource)i;
                    }
                    catch
                    {
                        //
                    }
                }

                double newVolume = -1;
                switch (inputSource)
                {
                    case InputSource.INTERNET_RADIO:
                    case InputSource.FAVORITES:
                        newVolume = -55;
                        break;
                    case InputSource.SAT_CBL: //DreamBox
                        newVolume = -30;
                        break;
                    case InputSource.BD:  //Kodi
                        newVolume = -30;
                        break;
                    case InputSource.DVR_BDR:  //ChromeCast
                        newVolume = -40;
                        break;
                    case InputSource.TUNER:  //Tuner
                        newVolume = -40;
                        break;
                    default:
                        newVolume = -60;
                        break;
                }
                if (newVolume < 0)
                {

                    result = $"SourceChange: {inputSource.ToString()} - Setting Volume to {newVolume.ToString()} dB.";
                    // Send test data to the remote device.  
                    // -58 dB [045]

                    //int volume = -80.5 + 0.5 * newVolume;
                    int volume = Math.Abs((int)((80.5 + newVolume) * 2));
                    
                    result += $" --> Will be: VOL{volume.ToString("000")}";
                    Thread.Sleep(1000);
                    Send(client,$"{volume.ToString("000")}VL\r\n");  
                    sendDone.WaitOne();  

                }
                else
                {
                    result = $"SourceChange: {inputSource.ToString()}";
                }

            }

            // 
            // InputSource
            if (data.StartsWith("AST")) 
            {
                InputAudioSignal inputAudioSignal = InputAudioSignal.Unknown;
                if (Int32.TryParse(data.Substring(3), out int i))
                {
                    try
                    {
                        inputAudioSignal = (InputAudioSignal)i;
                    }
                    catch
                    {
                        //
                    }
                }

                result = $"AudioSignalChange: {inputAudioSignal.ToString()}";

            }

            // Volume
            if (data.StartsWith("VOL")) 
            { 
                double volume = double.Parse(data.Substring(3));
                volume = -80.5 + 0.5 * volume;
                result = $"VOL: {volume.ToString()} dB [{data.Substring(3)}]";

                int deviceId = 90;
                string url = $"http://domoticz.bem.lan/json.htm?type=command&param=udevice&idx={deviceId.ToString()}&nvalue=0&svalue={volume.ToString()}";
                SendApiCall(url).Wait(1000);
            }

            // Display
            if (data.StartsWith("FL02")) 
            { 
                //result = "FL02: " + Encoding.ASCII.GetString(ConvertStringToByteArray(data.Substring(4)));
                result = ""; // for now - Avoid SPAM
            }


            // WebRadio Song
            // GEH01020"Lo Moon  - Real Love "
            if (data.StartsWith("GEH01020")) 
            { 
                string song = data.Substring(8).Replace("\"","").Trim().Replace("  "," ").Replace("  "," ");
                if (!String.IsNullOrWhiteSpace(song))
                {
                    if (_lastSong != song)
                    {
                        _lastSong = song;
                        result = "NewWebRadioSong: " + song;
                        int deviceId = 149;
                        song = System.Net.WebUtility.UrlEncode(song);
                        string url = $"http://domoticz.bem.lan/json.htm?type=command&param=udevice&idx={deviceId.ToString()}&nvalue=0&svalue={song}";
                        SendApiCall(url).Wait(1000);
                    }
                }
            }

            if (!String.IsNullOrWhiteSpace(result))
            {
                System.IO.File.AppendAllText("PioPi.log",$"{DateTime.Now.ToString("yyyMMdd.HHmmss")}: {result}\r\n");
            }

            return result;
        }

        static async Task<bool> SendApiCall(string url)
        {
            bool result = false;
            using (HttpClient client = new HttpClient())
            {
                try
                {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var x = await response.Content.ReadAsStringAsync();
                    result = true;
                }
                }
                catch (Exception ex)
                {
                    string q = "";
                }


            }
            return result;
        }
    }    

    }

}