using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;

namespace PioPi
{

    public delegate void InformationEventHandler(Object sender, InfoEventArgs e);
    public delegate void ResponseEventHandler(Object sender, SendResponseEventArgs e);

    public interface IReceiver
    {
            event InformationEventHandler SendInformationEvent;
            event ResponseEventHandler SendResponseEvent;

            void ProcessData(string data);

    }

    public class InfoEventArgs : EventArgs
    {
        public InfoEventArgs(string data) { Data = data;}

        public String Data { get; private set; } // readonly
        //public Pioneer.Command MyCommand { get; private set; } // readonly
    }
    public class SendResponseEventArgs : EventArgs
    {
        public SendResponseEventArgs(string data, int timeToWaitBeforeSend) { Data = data; TimeToWaitBeforeSend = timeToWaitBeforeSend; }

        public String Data { get; private set; } // readonly
        public int TimeToWaitBeforeSend { get; private set; } // readonly
    }


    public class PioVSX : IReceiver
    {

        #region Events

            public event InformationEventHandler SendInformationEvent;
            public event ResponseEventHandler SendResponseEvent;


            protected virtual void RaiseSendInfoHandler(string data)
            {
                // Raise the event by using the () operator.
                if (SendInformationEvent != null)
                    SendInformationEvent(this, new InfoEventArgs(data));
            }
            protected virtual void RaiseSendResponseHandler(string data, int timeToWaitBeforeSend)
            {
                // Raise the event by using the () operator.
                if (SendResponseEvent != null)
                    SendResponseEvent(this, new SendResponseEventArgs(data,timeToWaitBeforeSend));
            }

        #endregion

        public PioVSX()
        {

        }


        public string _lastSong {get; private set;}
        public string _lastStation {get; private set;}

        public enum DomoticzDevices
        {

            /*
            // Domoticz API Call
            // https://www.domoticz.com/wiki/Domoticz_API/JSON_URL%27s
            // DeviceId == 90
            // eg. http://192.168.1.2:8080/json.htm?type=command&param=udevice&idx=$idx&nvalue=0&svalue=79
            // VSX1123-Volume        id==90
            // VSX1123-PowerStatus   id==148
            // VSX1123-LastSong      id==149
            */

            VSX1123_Volume        = 90,
            VSX1123_PowerStatus   = 148,
            VSX1123_LastSong      = 149
        }

        public void ProcessData(string data)
        {
                string newResponse = DecodeResponse(data);
                if (!String.IsNullOrWhiteSpace(newResponse))
                {
                    RaiseSendInfoHandler(newResponse);
                }
        }

            private string DecodeResponse(string data)
            {
                string result = "";
                result = data;



                // PowerStatus
                if (data.StartsWith("PWR")) 
                { 
                    int newStatus = 0;
                    if (data == "PWR0")
                    {
                        newStatus = 1; //on
                        result = "Power is ON";
                    }
                    else
                    {
                        newStatus = 0; //off
                        result = "Power is OFF";
                        _lastSong = "";
                        _lastStation = "";
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

                    double newVolume = 0;
                    switch (inputSource)
                    {
                        case InputSource.INTERNET_RADIO:
                        case InputSource.FAVORITES:
                            newVolume = -60;
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
                        RaiseSendResponseHandler($"{volume.ToString("000")}VL", 500);  

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
                    // AST6401000000000000000000000111111010000000000012400000000
                    string frequency = "";
                    InputAudioSignal inputAudioSignal = InputAudioSignal.Unknown;
                    if (Int32.TryParse(data.Substring(3,2), out int i))
                    {
                        try
                        {
                            inputAudioSignal = (InputAudioSignal)i;
                        }
                        catch
                        {
                            //
                        }

                        
                        switch (data.Substring(5,2))
                        {
                            case "00": frequency = "32kHz"; break;
                            case "01": frequency = "44.1kHz"; break;
                            case "02": frequency = "48kHz"; break;
                            case "03": frequency = "88.2kHz"; break;
                            case "04": frequency = "96kHz"; break;
                            case "05": frequency = "176.4kHz"; break;
                            case "06": frequency = "192kHz"; break;
                            case "07": frequency = "Unknown"; break;
                            default:
                                break;
                        }

                    }

                    result = $"AudioSignalChange: {inputAudioSignal.ToString()} {frequency}";

                }

                // Volume
                if (data.StartsWith("VOL")) 
                { 
                    double volume = double.Parse(data.Substring(3));
                    volume = -80.5 + 0.5 * volume;
                    result = $"Volume is set to {volume.ToString()} dB [{data.Substring(3)}]";

                    int deviceId = 90;
                    string url = $"http://domoticz.bem.lan/json.htm?type=command&param=udevice&idx={deviceId.ToString()}&nvalue=0&svalue={volume.ToString()}";
                    SendApiCall(url).Wait(1000);
                }

                // Display - Info
                if (data.StartsWith("FL00")) 
                { 
                    result = "FL00: " + Encoding.ASCII.GetString(ConvertStringToByteArray(data.Substring(4)));
                }

                // Display - Title
                if (data.StartsWith("FL02")) 
                { 
                    //result = "FL02: " + Encoding.ASCII.GetString(ConvertStringToByteArray(data.Substring(4)));
                    result = ""; // for now - Avoid SPAM
                }


    /*
        -- Response NETWORK meta data
        GBH*<CR+LF>
        GCH*<CR+LF>
        GDH*<CR+LF>
        GEH*<CR+LF>"
        GHH*<CR+LF>
    */
                if (data.StartsWith("GBH")) 
                {
                    //result = $"GBH: START `Response NETWORK meta data` ({result})";
                    result = ""; // for now - Avoid SPAM
                }
                if (data.StartsWith("GHH")) 
                {
                    //result = $"GHH: END `Response NETWORK meta data` ({result})";
                    result = ""; // for now - Avoid SPAM
                }
                if (data.StartsWith("GCH")) 
                {
                    result = "";
                }
                if (data.StartsWith("GDH")) 
                {
                    result = "";
                }
                
                // WebRadio Song
                // GEH01020"Lo Moon  - Real Love "
                if (data.StartsWith("GEH")) 
                { 
                    string text = null;
                    if (data.Length > 8) {text = data.Substring(8).Replace("\"","").Trim().Replace("  "," ").Replace("  "," ");}
                    result = "";

                    if (!String.IsNullOrWhiteSpace(text))
                    {
                        switch (data.Substring(0,8))
                        { 
                            case "GEH01020":  //Song
                                if (_lastSong != text && !text.StartsWith("(c)"))
                                {
                                    _lastSong = text;
                                    result = "NewWebRadioSong: " + text;
                                    int deviceId = 149;
                                    text = System.Net.WebUtility.UrlEncode(text);
                                    string url = $"http://domoticz.bem.lan/json.htm?type=command&param=udevice&idx={deviceId.ToString()}&nvalue=0&svalue={text}";
                                    SendApiCall(url).Wait(1000);
                                }
                                break;
                            case "GEH03021":  //Artist
                                result = "NewArtist: " + text;
                                break;
                            case "GEH04022":  //Station
                                if (_lastStation != text)
                                {
                                    _lastStation = text;
                                     result = "NewStation: " + text;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }


                // LM020d ???
                if (data.StartsWith("LM")) 
                {
                    result = $"Listening Mode: {GetLmResult(data.Substring(2))}";
                }


                // Ignore VTA and AU_
                if (data.StartsWith("VTA") || data.StartsWith("AU")) 
                {
                    result = "";
                }

                if (!String.IsNullOrWhiteSpace(result))
                {
                    System.IO.File.AppendAllText("PioPi.log",$"{DateTime.Now.ToString("yyyMMdd.HHmmss")}: {result}\r\n");
                }

                return result;
            }



            private async Task<bool> SendApiCall(string url)
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
        

            //http://dotnetstock.com/technical/convert-byte-array-hexadecimal-string/
            public byte[] ConvertStringToByteArray(String strhex)
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

        public string GetLmResult(string lm)
        {
            string lmResult = "";
            switch (lm)
            {
                case "0101": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0102": lmResult = "[)(]PLII MOVIE"; break;
                case "0103": lmResult = "[)(]PLIIx MUSIC"; break;
                case "0104": lmResult = "[)(]PLII MUSIC"; break;
                case "0105": lmResult = "[)(]PLIIx GAME"; break;
                case "0106": lmResult = "[)(]PLII GAME"; break;
                case "0107": lmResult = "[)(]PROLOGIC"; break;
                case "0108": lmResult = "Neo:6 CINEMA"; break;
                case "0109": lmResult = "Neo:6 MUSIC"; break;
                case "010a": lmResult = "XM HD Surround"; break;
                case "010b": lmResult = "NEURAL SURR  "; break;
                case "010c": lmResult = "2ch Straight Decode"; break;
                case "010d": lmResult = "[)(]PLIIz HEIGHT"; break;
                case "010e": lmResult = "WIDE SURR MOVIE"; break;
                case "010f": lmResult = "WIDE SURR MUSIC"; break;
                case "0110": lmResult = "STEREO"; break;
                case "0111": lmResult = "Neo:X CINEMA"; break;
                case "0112": lmResult = "Neo:X MUSIC"; break;
                case "0113": lmResult = "Neo:X GAME"; break;
                case "0114": lmResult = "NEURAL SURROUND+Neo:X CINEMA"; break;
                case "0115": lmResult = "NEURAL SURROUND+Neo:X MUSIC"; break;
                case "0116": lmResult = "NEURAL SURROUND+Neo:X GAMES"; break;
                case "1101": lmResult = "[)(]PLIIx MOVIE"; break;
                case "1102": lmResult = "[)(]PLIIx MUSIC"; break;
                case "1103": lmResult = "[)(]DIGITAL EX"; break;
                case "1104": lmResult = "DTS +Neo:6 / DTS-HD +Neo:6"; break;
                case "1105": lmResult = "ES MATRIX"; break;
                case "1106": lmResult = "ES DISCRETE"; break;
                case "1107": lmResult = "DTS-ES 8ch "; break;
                case "1108": lmResult = "multi ch Straight Decode"; break;
                case "1109": lmResult = "[)(]PLIIz HEIGHT"; break;
                case "110a": lmResult = "WIDE SURR MOVIE"; break;
                case "110b": lmResult = "WIDE SURR MUSIC"; break;
                case "110c": lmResult = "Neo:X CINEMA "; break;
                case "110d": lmResult = "Neo:X MUSIC"; break;
                case "110e": lmResult = "Neo:X GAME"; break;
                case "0201": lmResult = "ACTION"; break;
                case "0202": lmResult = "DRAMA"; break;
                case "0203": lmResult = "SCI-FI"; break;
                case "0204": lmResult = "MONOFILM"; break;
                case "0205": lmResult = "ENT.SHOW"; break;
                case "0206": lmResult = "EXPANDED"; break;
                case "0207": lmResult = "TV SURROUND"; break;
                case "0208": lmResult = "ADVANCEDGAME"; break;
                case "0209": lmResult = "SPORTS"; break;
                case "020a": lmResult = "CLASSICAL   "; break;
                case "020b": lmResult = "ROCK/POP   "; break;
                case "020c": lmResult = "UNPLUGGED   "; break;
                case "020d": lmResult = "EXT.STEREO  "; break;
                case "020e": lmResult = "PHONES SURR. "; break;
                case "020f": lmResult = "FRONT STAGE SURROUND ADVANCE FOCUS"; break;
                case "0210": lmResult = "FRONT STAGE SURROUND ADVANCE WIDE"; break;
                case "0211": lmResult = "SOUND RETRIEVER AIR"; break;
                case "0301": lmResult = "[)(]PLIIx MOVIE +THX"; break;
                case "0302": lmResult = "[)(]PLII MOVIE +THX"; break;
                case "0303": lmResult = "[)(]PL +THX CINEMA"; break;
                case "0304": lmResult = "Neo:6 CINEMA +THX"; break;
                case "0305": lmResult = "THX CINEMA"; break;
                case "0306": lmResult = "[)(]PLIIx MUSIC +THX"; break;
                case "0307": lmResult = "[)(]PLII MUSIC +THX"; break;
                case "0308": lmResult = "[)(]PL +THX MUSIC"; break;
                case "0309": lmResult = "Neo:6 MUSIC +THX"; break;
                case "030a": lmResult = "THX MUSIC"; break;
                case "030b": lmResult = "[)(]PLIIx GAME +THX"; break;
                case "030c": lmResult = "[)(]PLII GAME +THX"; break;
                case "030d": lmResult = "[)(]PL +THX GAMES"; break;
                case "030e": lmResult = "THX ULTRA2 GAMES"; break;
                case "030f": lmResult = "THX SELECT2 GAMES"; break;
                case "0310": lmResult = "THX GAMES"; break;
                case "0311": lmResult = "[)(]PLIIz +THX CINEMA"; break;
                case "0312": lmResult = "[)(]PLIIz +THX MUSIC"; break;
                case "0313": lmResult = "[)(]PLIIz +THX GAMES"; break;
                case "0314": lmResult = "Neo:X CINEMA + THX CINEMA"; break;
                case "0315": lmResult = "Neo:X MUSIC + THX MUSIC"; break;
                case "0316": lmResult = "Neo:X GAMES + THX GAMES"; break;
                case "1301": lmResult = "THX Surr EX"; break;
                case "1302": lmResult = "Neo:6 +THX CINEMA"; break;
                case "1303": lmResult = "ES MTRX +THX CINEMA"; break;
                case "1304": lmResult = "ES DISC +THX CINEMA"; break;
                case "1305": lmResult = "ES 8ch +THX CINEMA "; break;
                case "1306": lmResult = "[)(]PLIIx MOVIE +THX"; break;
                case "1307": lmResult = "THX ULTRA2 CINEMA"; break;
                case "1308": lmResult = "THX SELECT2 CINEMA"; break;
                case "1309": lmResult = "THX CINEMA"; break;
                case "130a": lmResult = "Neo:6 +THX MUSIC"; break;
                case "130b": lmResult = "ES MTRX +THX MUSIC"; break;
                case "130c": lmResult = "ES DISC +THX MUSIC"; break;
                case "130d": lmResult = "ES 8ch +THX MUSIC"; break;
                case "130e": lmResult = "[)(]PLIIx MUSIC +THX"; break;
                case "130f": lmResult = "THX ULTRA2 MUSIC"; break;
                case "1310": lmResult = "THX SELECT2 MUSIC"; break;
                case "1311": lmResult = "THX MUSIC"; break;
                case "1312": lmResult = "Neo:6 +THX GAMES"; break;
                case "1313": lmResult = "ES MTRX +THX GAMES"; break;
                case "1314": lmResult = "ES DISC +THX GAMES"; break;
                case "1315": lmResult = "ES 8ch +THX GAMES"; break;
                case "1316": lmResult = "[)(]EX +THX GAMES"; break;
                case "1317": lmResult = "THX ULTRA2 GAMES"; break;
                case "1318": lmResult = "THX SELECT2 GAMES"; break;
                case "1319": lmResult = "THX GAMES"; break;
                case "131a": lmResult = "[)(]PLIIz +THX CINEMA"; break;
                case "131b": lmResult = "[)(]PLIIz +THX MUSIC"; break;
                case "131c": lmResult = "[)(]PLIIz +THX GAMES"; break;
                case "131d": lmResult = "Neo:X CINEMA + THX CINEMA"; break;
                case "131e": lmResult = "Neo:X MUSIC + THX MUSIC"; break;
                case "131f": lmResult = "Neo:X GAME + THX GAMES"; break;
                case "0401": lmResult = "STEREO"; break;
                case "0402": lmResult = "[)(]PLII MOVIE"; break;
                case "0403": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0404": lmResult = "Neo:6 CINEMA"; break;
                case "0405": lmResult = "AUTO SURROUND Straight Decode"; break;
                case "0406": lmResult = "[)(]DIGITAL EX"; break;
                case "0407": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0408": lmResult = "DTS +Neo:6"; break;
                case "0409": lmResult = "ES MATRIX"; break;
                case "040a": lmResult = "ES DISCRETE"; break;
                case "040b": lmResult = "DTS-ES 8ch "; break;
                case "040c": lmResult = "XM HD Surround"; break;
                case "040d": lmResult = "NEURAL SURR  "; break;
                case "040e": lmResult = "RETRIEVER AIR"; break;
                case "040f": lmResult = "Neo:X CINEMA"; break;
                case "0410": lmResult = "Neo:X CINEMA "; break;
                case "0501": lmResult = "STEREO"; break;
                case "0502": lmResult = "[)(]PLII MOVIE"; break;
                case "0503": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0504": lmResult = "Neo:6 CINEMA"; break;
                case "0505": lmResult = "ALC Straight Decode"; break;
                case "0506": lmResult = "[)(]DIGITAL EX"; break;
                case "0507": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0508": lmResult = "DTS +Neo:6"; break;
                case "0509": lmResult = "ES MATRIX"; break;
                case "050a": lmResult = "ES DISCRETE"; break;
                case "050b": lmResult = "DTS-ES 8ch "; break;
                case "050c": lmResult = "XM HD Surround"; break;
                case "050d": lmResult = "NEURAL SURR  "; break;
                case "050e": lmResult = "RETRIEVER AIR"; break;
                case "050f": lmResult = "Neo:X CINEMA"; break;
                case "0510": lmResult = "Neo:X CINEMA "; break;
                case "0601": lmResult = "STEREO"; break;
                case "0602": lmResult = "[)(]PLII MOVIE"; break;
                case "0603": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0604": lmResult = "Neo:6 CINEMA"; break;
                case "0605": lmResult = "STREAM DIRECT NORMAL Straight Decode"; break;
                case "0606": lmResult = "[)(]DIGITAL EX"; break;
                case "0607": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0608": lmResult = "(nothing)"; break;
                case "0609": lmResult = "ES MATRIX"; break;
                case "060a": lmResult = "ES DISCRETE"; break;
                case "060b": lmResult = "DTS-ES 8ch "; break;
                case "060c": lmResult = "Neo:X CINEMA"; break;
                case "060d": lmResult = "Neo:X CINEMA "; break;
                case "0701": lmResult = "STREAM DIRECT PURE 2ch"; break;
                case "0702": lmResult = "[)(]PLII MOVIE"; break;
                case "0703": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0704": lmResult = "Neo:6 CINEMA"; break;
                case "0705": lmResult = "STREAM DIRECT PURE Straight Decode"; break;
                case "0706": lmResult = "[)(]DIGITAL EX"; break;
                case "0707": lmResult = "[)(]PLIIx MOVIE"; break;
                case "0708": lmResult = "(nothing)"; break;
                case "0709": lmResult = "ES MATRIX"; break;
                case "070a": lmResult = "ES DISCRETE"; break;
                case "070b": lmResult = "DTS-ES 8ch "; break;
                case "070c": lmResult = "Neo:X CINEMA"; break;
                case "070d": lmResult = "Neo:X CINEMA "; break;
                case "0881": lmResult = "OPTIMUM"; break;
                case "0e01": lmResult = "HDMI THROUGH"; break;
                case "0f01": lmResult = "MULTI CH IN"; break;
                default:
                    lmResult = $"UNKNOWN ({lm})";
                    break;
            }

            return lmResult;
            
        }

    }


}