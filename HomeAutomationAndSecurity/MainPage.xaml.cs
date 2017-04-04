using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Gpio;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Devices.Sensors;
using Windows.Devices.Sensors.Custom;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;

namespace HomeAutomationAndSecurity
{
    public class RootObject
    {

        public string field1 { get; set; }
        public string field2 { get; set; }
    }
    public sealed partial class MainPage : Page
    {
        private const int Led_Pin1 = 5;
        private const int Led_Pin2 = 6;
        private GpioPin pin1;
        private GpioPin pin2;
        private GpioPinValue pinValue1;
        private GpioPinValue pinValue2;
        private const int ECHO_PIN = 23;
        private const int TRIGGER_PIN = 18;
        private GpioPin pinEcho;
        private GpioPin pinTrigger;
        private DispatcherTimer timer;
        private Stopwatch sw;
        public MainPage()
        {
            this.InitializeComponent();
            var timer = new System.Threading.Timer((e) =>
            {
                httpget();
            }, null, 0, Convert.ToInt32(TimeSpan.FromMinutes(0.016).TotalMilliseconds));
        }
        /////////////////Code for working with server///////////////////////
        //Code to get new data from the server
        public async void httpget()
        {
            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();
            Uri requestUri = new Uri("https://api.thingspeak.com/channels/231958/feeds/last");
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            httpResponse = await httpClient.GetAsync(requestUri);
            httpResponse.EnsureSuccessStatusCode();
            var jsonString = await httpResponse.Content.ReadAsStringAsync();
            JsonObject root = JsonObject.Parse(jsonString);
            RootObject r = new RootObject();
            foreach (var item in root)
            {
                if (item.Key == "field1")
                    r.field1 = item.Value.ToString();
                if (item.Key == "field2")
                    r.field2 = item.Value.ToString();
            }
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                  () =>
                  {
                      if (r.field1 == "\"1\"" && (room1Switch.Background as SolidColorBrush).Color != Windows.UI.Colors.Green)
                          room1SwitchChangeStatus();
                      else if (r.field1 == "\"0\"" && (room1Switch.Background as SolidColorBrush).Color != Windows.UI.Colors.Red)
                          room1SwitchChangeStatus();
                      if (r.field2 == "\"1\"" && (room2Switch.Background as SolidColorBrush).Color != Windows.UI.Colors.Green)
                          room2SwitchChangeStatus();
                      else if (r.field2 == "\"0\"" && (room2Switch.Background as SolidColorBrush).Color != Windows.UI.Colors.Red)
                          room2SwitchChangeStatus();

                  });
        }

        //Code to send data to the server
        public async Task<bool> http(string field)
        {
            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();
            Uri requestUri = new Uri("https://api.thingspeak.com/update?api_key=CI57AXMBOSM1VQN1&" + field);
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";
            httpResponse = await httpClient.GetAsync(requestUri);
            httpResponse.EnsureSuccessStatusCode();
            httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
            status.Text = httpResponseBody;
            httpClient = null;
            if (httpResponseBody == "0")
                return false;
            else
                return true;
        }



        ///////////////////////////////////Code for voice recognition///////////////////////////////////
        //To initialize Speech Recognizer
        public async void InitSpeechRecognizer()
        {
            SpeechRecognizer Rec = new SpeechRecognizer();
            Rec.ContinuousRecognitionSession.ResultGenerated += Rec_ResultGenerated;

            StorageFile Store = await Package.Current.InstalledLocation.GetFileAsync(@"GrammarFile.xml");
            SpeechRecognitionGrammarFileConstraint constraint = new SpeechRecognitionGrammarFileConstraint(Store);
            Rec.Constraints.Add(constraint);
            SpeechRecognitionCompilationResult result = await Rec.CompileConstraintsAsync();
            if (result.Status == SpeechRecognitionResultStatus.Success)
            {
                status.Text = "Speech Recognition started.";
                tts(status.Text);
                Rec.UIOptions.AudiblePrompt = "Speech Recognition started.";
                await Rec.ContinuousRecognitionSession.StartAsync();
            }
        }

        //To handle Event by Speech Recognizer
        private async void Rec_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {

            switch (args.Result.Text)
            {
                case "switch light of room one":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                      async () =>
                      {
                          bool b =await  light1Switch();
                          string s = b ? "I have switched the light of room one" : "I have switched the light of room one, but server can not be updated due to time limit.";
                          tts(s);
                      });

                    break;
                case "switch light of room two":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        bool b = await light2Switch();
                        string s = b ? "I have switched the light of room two" : "I have switched the light of room two, but server can not be updated due to time limit.";
                        tts(s);
                    });
                    break;
                case "switch all lights":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        bool b = await allLightSwitch();
                        string s = b ? "I have switched all the lights" : "I have switched all the lights, but server can not be updated due to time limit.";
                        tts(s);
                    });
                    break;
                default:
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                   () =>
                   {
                       tts("Sorry I didn't get you.");
                   });
                    break;
            }
        }

        //Code for Text to Speech
        public async void tts(string text)
        {
            var media = new MediaElement();
            var s = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            var stream = await s.SynthesizeTextToStreamAsync(text);
            media.SetSource(stream, stream.ContentType);
            media.Play();
        }




        /////////////////////////////Code for working with Gpio////////////////////////////////
        //Code to initialize Gpio
        public void InitGPIO()
        {
            var gpio = GpioController.GetDefault();
            if (gpio == null)
            {
                pin1 = null;
                pin2 = null;
                status.Text = "Gpio Pin Not Initialized";
                return;
            }
            pin1 = gpio.OpenPin(Led_Pin1);
            pin2 = gpio.OpenPin(Led_Pin2);
            pinValue1 = GpioPinValue.High;
            pinValue2 = GpioPinValue.High;
            pin1.Write(pinValue1);
            pin1.Write(pinValue1);
            pin1.Write(pinValue1);
            pin1.SetDriveMode(GpioPinDriveMode.Output);
            pin2.SetDriveMode(GpioPinDriveMode.Output);
            status.Text = "gpio pin initialized.";
        }

        //Code for working with Lights
        public void switchLightRoom1()
        {
            if (pinValue1 == GpioPinValue.High)
            {
                pinValue1 = GpioPinValue.Low;
                pin1.Write(pinValue1);
            }
            else
            {
                pinValue1 = GpioPinValue.High;
                pin1.Write(pinValue1);
            }
        }

        public void switchLightRoom2()
        {
            if (pinValue2 == GpioPinValue.High)
            {
                pinValue2 = GpioPinValue.Low;
                pin2.Write(pinValue2);
            }
            else
            {
                pinValue2 = GpioPinValue.High;
                pin2.Write(pinValue2);
            }
        }

        public async Task<bool> allLightSwitch()
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                switchLightRoom1();
                switchLightRoom2();
            }
            string s = "";
            if (pinValue1==GpioPinValue.Low)
            {
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                s = "field1=1";
            }
            else
            {
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = "field1=0";
            }

            if (pinValue2==GpioPinValue.Low)
            {
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                s = s + "&field2=1";
            }
            else
            {
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = s + "&field2=0";
            }
            bool b = await  http(s);
            status.Text = b ? "All lights switched." : "All light switched but server cannot be updated due to time limit." ;
            return b;
        }

        private async void room1switch_Click(object sender, RoutedEventArgs e)
        {
            await light1Switch();
        }

        public async Task<bool> light1Switch()
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                switchLightRoom1();
            }
            string s = pinValue2 == GpioPinValue.Low ? "&field2=1" : "&field2=0";
            if (pinValue1 == GpioPinValue.Low)
            {
                s = "field1=1" + s;
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            }
            else
            {
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = "field1=0" + s;
            }
            bool b = await http(s);
            status.Text = b ? "Room 1 light switched" : "Room 1 light switched but server cannot be updated due to time limit.";
            return b;
        }
        private async void room2Switch_Click(object sender, RoutedEventArgs e)
        {
            await light2Switch();
        }

        public async Task<bool> light2Switch()
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                switchLightRoom2();
            }
            string s = pinValue1 == GpioPinValue.Low ? "&field2=1" : "&field2=0";
            if (pinValue2 == GpioPinValue.Low)
            {
                s = s + "&field2=1";
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            }
            else
            {
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = s + "&field2=0";
            }
            bool b = await http(s);
            status.Text = b ? "Room 2 light switched" : "Room 2 light switched but server cannot be updated due to time limit.";
            return b;
        }

        public void room1SwitchChangeStatus()
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                switchLightRoom1();

                if (pinValue1 == GpioPinValue.Low)
                    room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                else
                    room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                status.Text = "Room 1 light switched";
            }
        }

        public void room2SwitchChangeStatus()
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                switchLightRoom2();

                if (pinValue2 == GpioPinValue.Low)
                    room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                else
                    room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                status.Text = "Room 2 light switched";
            }
        }

        private void speechRecognizerSwitch_Click(object sender, RoutedEventArgs e)
        {
            InitSpeechRecognizer();
        }
    }
}
