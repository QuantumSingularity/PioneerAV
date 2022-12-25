using System;
using MQTTnet;
using MQTTnet.Client;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace PioPi
{

    public class ApplicationMessageReceivedEventArgs : EventArgs
    {
        public ApplicationMessageReceivedEventArgs(string topic, string payload) { Topic = topic; Payload = payload; }

        public String Topic { get; private set; } // readonly
        public String Payload { get; private set; } // readonly
    }


    public class MQTT
    {


            public delegate void ApplicationMessageReceivedHandler(Object sender, ApplicationMessageReceivedEventArgs e);

            public event ApplicationMessageReceivedHandler SendApplicationMessageReceivedHandler;

            protected virtual void RaiseSendResponseHandler(string topic, string payload)
            {
                // Raise the event by using the () operator.
                if (SendApplicationMessageReceivedHandler != null)
                    SendApplicationMessageReceivedHandler(this, new ApplicationMessageReceivedEventArgs(topic,payload));
            }


        protected IMqttClient _mqttClient;
        protected bool _mustStop = false;
        protected bool _isConnected = false;

        public MQTT()
        {
        }

        public async Task<bool> Start()
        {
            //Console.WriteLine("Hello World!");

            _mustStop = false;
            _isConnected = false;

            // Create a new MQTT client.
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // Use TCP connection.
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("mqtt.bem.lan", 1883) // Port is optional
                .WithClientId("PioPi")
                .Build();


            // new for mqttnet 4
            _mqttClient.DisconnectedAsync += async e =>
            {

                _isConnected = false;
                Console.WriteLine("### MQTT - DISCONNECTED FROM SERVER ###");
                await Task.Delay(TimeSpan.FromSeconds(5));

                if (e.ClientWasConnected)
                {

                    try
                    {
                        if (_mustStop)
                        {
                            Console.WriteLine("### MQTT - DO NOT RECONNECT - MustStop is Active ###");
                        }
                        else
                        {
                            // Use the current options as the new options.
                            await _mqttClient.ConnectAsync(_mqttClient.Options);
                            Console.WriteLine("### MQTT - RECONNECTED ###");
                        }
                    }
                    catch
                    {
                        Console.WriteLine("### MQTT - RECONNECTING FAILED ###");
                    }

                }
            };

            // only for old versions of mqttnet
            /*
            _mqttClient.Disconnected += async (s, e) =>
            {
                _isConnected = false;
                Console.WriteLine("### MQTT - DISCONNECTED FROM SERVER ###");
                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    if (_mustStop)
                    {
                        Console.WriteLine("### MQTT - DO NOT RECONNECT - MustStop is Active ###");
                    }
                    else
                    {
                        await _mqttClient.ConnectAsync(options);
                        Console.WriteLine("### MQTT - RECONNECTED ###");
                    }
                }
                catch
                {
                    Console.WriteLine("### MQTT - RECONNECTING FAILED ###");
                }


            };
            */

            // new for mqttnet 4
            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                Console.WriteLine("### MQTT - RECEIVED APPLICATION MESSAGE ###");
                Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
                Console.WriteLine();

                RaiseSendResponseHandler(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.Payload));

                return Task.CompletedTask;
            };

            // only for old versions of mqttnet
            /*
            _mqttClient.ApplicationMessageReceived += (s, e) =>
            {
                Console.WriteLine("### MQTT - RECEIVED APPLICATION MESSAGE ###");
                Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
                Console.WriteLine();

                RaiseSendResponseHandler(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.Payload));

            };
            */



            _mqttClient.ConnectedAsync += async e =>
            {
                _isConnected = true;
                Console.WriteLine("### MQTT - CONNECTED WITH SERVER ###");

                // Subscribe to a topic
                //await _mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic("/EspEasy_Meterkast/BME280/Pressure").Build());
                await _mqttClient.SubscribeAsync("PioPi/Power/set");
                await _mqttClient.SubscribeAsync("PioPi/Volume/set");
                await _mqttClient.SubscribeAsync("PioPi/ListeningMode/set");
                await _mqttClient.SubscribeAsync("PioPi/AudioDelay/set");
                // HomeAssistant/PioPi/Volume/Set^M


                /*
                var mqttFactory = new MqttFactory();
                var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(
                        f =>
                        {
                            f.WithTopic("mqttnet/samples/topic/2");
                        })
                    .Build();

                await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                */
                //Console.WriteLine("### SUBSCRIBED ###");

                //return Task.CompletedTask;

            };

            // only for old versions of mqttnet
            /*
            _mqttClient.Connected += async (s, e) =>
            {
                _isConnected = true;
                Console.WriteLine("### MQTT - CONNECTED WITH SERVER ###");

                // Subscribe to a topic
                //await _mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic("/EspEasy_Meterkast/BME280/Pressure").Build());
                await _mqttClient.SubscribeAsync("HomeAssistant/PioPi/Power/set");
                await _mqttClient.SubscribeAsync("HomeAssistant/PioPi/Volume/set");
                await _mqttClient.SubscribeAsync("PioPi/ListeningMode/set");
                // HomeAssistant/PioPi/Volume/Set^M

                //Console.WriteLine("### SUBSCRIBED ###");
            };
            */

            await _mqttClient.ConnectAsync(options);


    //        Console.ReadLine();

  //          mqttClient.DisconnectAsync().Wait();
            
//            Console.WriteLine("THNX for using....");

//            Console.ReadLine();

            

	        return true;

        }

/*
        public void WaitForConnection(int timeOut)
        {
            while (!_isConnected && timeOut > 0)
            {
                System.Threading.Thread.Sleep(100);
                timeOut -= 100;
            }

        }
*/

        public async Task<bool> Stop()
        {
            _mustStop = true;
            await _mqttClient.DisconnectAsync();
            return true;
        }


        public async Task<bool> Publish(string topic, string payload)
        {

            if (_mqttClient.IsConnected)
            {
                if (payload.Length > 10 && payload.Contains("+"))
                {
                    payload = System.Net.WebUtility.UrlDecode(payload);
                }

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    //.WithExactlyOnceQoS()
                    .WithRetainFlag()
                    .Build();
        
                await _mqttClient.PublishAsync(message);

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsStarted
        {
            get {
                return _mqttClient.IsConnected;
            }
        }

    }
}
