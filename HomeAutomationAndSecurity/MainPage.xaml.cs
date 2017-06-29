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
using System.Threading;
using System.Threading.Tasks;
using Windows.System.Threading;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Media.Capture;
using Windows.Storage.Streams;
using Windows.Media.MediaProperties;
using Windows.Devices.Enumeration;

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
        private const int Led_Pin2 = 23;
        private GpioPin pin1;
        private GpioPin pin2;
        private GpioPinValue pinValue1;
        private GpioPinValue pinValue2;
        private const int ECHO_PIN = 24;
        private const int TRIGGER_PIN = 17;
        private GpioPin pinEcho;
        private GpioPin pinTrigger;
        private DispatcherTimer timer;
        private Stopwatch sw;
        private IRandomAccessStream stream;
        SpeechRecognizer Rec;
        public MainPage()
        {
            this.InitializeComponent();
            ThreadPoolTimer timer = ThreadPoolTimer.CreatePeriodicTimer((t) =>
            {
                httpget();
            }, TimeSpan.FromMinutes(0.1));
            ThreadPoolTimer timer1 = ThreadPoolTimer.CreatePeriodicTimer((t) =>
            {
                initUltrasonic();
            }, TimeSpan.FromMinutes(0.1));
            InitGPIO();
        }
        /// <summary>
        /// Code for working with server.
        /// </summary>
        //Code to get new data from the server
        public async void httpget()
        {

            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();
            Uri requestUri = new Uri("https://api.thingspeak.com/channels/231958/feeds/last");
            Windows.Web.Http.HttpResponseMessage httpResponse;
            try {
                httpResponse = new Windows.Web.Http.HttpResponseMessage();
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
            }
            catch
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                  () =>
                  {
                      status.Text = "Internet is not working!";

                  });
                return;
            }
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
                      if (r.field1 == "\"1\"" || r.field1=="\"0\"") 
                        room1SwitchChangeStatus(Int32.Parse(r.field1[1].ToString()));
                      if (r.field2 == "\"1\"" || r.field2 == "\"0\"")
                          room2SwitchChangeStatus(Int32.Parse(r.field2[1].ToString()));

                  });
        }

        //Code to send data to the server. Return true if value is updated on server or false if not.
        public async Task<bool> http(string field)
        {
            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();
            Uri requestUri = new Uri("https://api.thingspeak.com/update?api_key=CI57AXMBOSM1VQN1&" + field);
            string httpResponseBody = "";
            Windows.Web.Http.HttpResponseMessage httpResponse;
            try {
                httpResponse = new Windows.Web.Http.HttpResponseMessage();
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
            }
            catch
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                  () =>
                  {
                      status.Text = "Internet is not working!";

                  });
                return false;
            }
            httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
            status.Text = httpResponseBody;
            httpClient = null;
            if (httpResponseBody == "0")
                return false;
            else
                return true;
        }



        /// <summary>
        /// Code for voice recognition.
        /// </summary>
        //To initialize Speech Recognizer
        public async void InitSpeechRecognizer(int n)
        {

               if(n==0)
            {
                Rec.Dispose();
                return;
            }
            Rec = new SpeechRecognizer();
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

        //To handle Event by Speech Recognizer. Call TextToSpeech on the basis of that value is updated on server or not and switch the lights.
        private async void Rec_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {

            switch (args.Result.Text)
            {
                case "Turn on light of room one":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                      async () =>
                      {
                          bool b =await  light1Switch(1);
                          string s = b ? "I have turned on the light of room one" : "I have turned on the light of room one, but server can not be updated due to time limit.";
                          tts(s);
                      });

                    break;
                case "Turn off light of room one":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                      async () =>
                      {
                          bool b = await light1Switch(0);
                          string s = b ? "I have turned off the light of room one" : "I have turned off the light of room one, but server can not be updated due to time limit.";
                          tts(s);
                      });

                    break;
                case "Turn on light of room two":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        bool b = await light2Switch(1);
                        string s = b ? "I have turned on the light of room two" : "I have turned on the light of room two, but server can not be updated due to time limit.";
                        tts(s);
                    });
                    break;
                case "Turn off light of room two":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        bool b = await light2Switch(0);
                        string s = b ? "I have turned off the light of room two" : "I have turned off the light of room two, but server can not be updated due to time limit.";
                        tts(s);
                    });
                    break;
                case "Turn on all the lights":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        bool b = await allLightSwitch(1);
                        string s = b ? "I have turned on all the lights" : "I have turned on all the lights, but server can not be updated due to time limit.";
                        tts(s);
                    });
                    break;
                case "Turn off all the lights":
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        bool b = await allLightSwitch(0);
                        string s = b ? "I have turned off all the lights" : "I have turned off all the lights, but server can not be updated due to time limit.";
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




        /// <summary>
        /// Code for workinng with GPIO
        /// </summary>
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
            pinTrigger = gpio.OpenPin(TRIGGER_PIN);
            pinEcho = gpio.OpenPin(ECHO_PIN);

            pinValue1 = GpioPinValue.High;
            pinValue2 = GpioPinValue.High;

            pin1.Write(pinValue1);
            pin2.Write(pinValue2);
            pin1.SetDriveMode(GpioPinDriveMode.Output);
            pin2.SetDriveMode(GpioPinDriveMode.Output);
            pinTrigger.SetDriveMode(GpioPinDriveMode.Output);
            pinEcho.SetDriveMode(GpioPinDriveMode.Input);
            status.Text = "gpio pin initialized.";
        }

        //Code for switching light of Room1
        public void switchLightRoom1(int n)
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                if (n == 1)
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
        }

        //Code for switching light of Room2
        public void switchLightRoom2(int n)
        {
            var gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                if (n == 1)
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
        }

        //Code for Updating UI and sending values to server when all lights are switched. Return string value true if value is updated on server or false if not.
        public async Task<bool> allLightSwitch(int n)
        {
            string s = "";
            if (n==1)
            {
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                s = "field1=1&field2=1";
                bool b = await http(s);
                status.Text = b ? "All lights turned on." : "All lights turned on but server cannot be updated due to time limit.";
                return b;
            }
            else
            {
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = "field1=0&field2=0";
                bool b = await http(s);
                status.Text = b ? "All lights turned off." : "All lights turned off but server cannot be updated due to time limit.";
                return b;
            }

            
        }

        //Code for Updating UI and sending values to server when room1 light is switched. Return string value true if value is updated on server or false if not.
        public async Task<bool> light1Switch(int n)
        {
           
            string s = pinValue2 == GpioPinValue.Low ? "&field2=1" : "&field2=0";
            if (n==1)
            {
                s = "field1=1" + s;
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                bool b = await http(s);
                status.Text = b ? "Room 1 light turned on." : "Room 1 light turned on but server cannot be updated due to time limit.";
                return b;
            }
            else
            {
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = "field1=0" + s;
                room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                bool b = await http(s);
                status.Text = b ? "Room 1 light turned off." : "Room 1 light turned off but server cannot be updated due to time limit.";
                return b;
            }
        }


        //Code for Updating UI and sending values to server when room2 light is switched. Return string value true if value is updated on server or false if not.
        public async Task<bool> light2Switch(int n)
        {
            string s = pinValue1 == GpioPinValue.Low ? "field1=1" : "field1=0";
            if (n==1)
            {
                s = s + "&field2=1";
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                bool b = await http(s);
                status.Text = b ? "Room 2 light turned on." : "Room 2 light turned on but server cannot be updated due to time limit.";
                return b;
            }
            else
            {
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                s = s + "&field2=0";
                bool b = await http(s);
                status.Text = b ? "Room 2 light turned off." : "Room 2 light turned off but server cannot be updated due to time limit.";
                return b;
            }
        }

        //Code for Updating UI  when room1 light is switched from the user app.
        public void room1SwitchChangeStatus(int n)
        {
            
                switchLightRoom1(n);

                if (n==1)
                    room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                else
                    room1Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                status.Text = "Room 1 light switched";
            
        }

        //Code for Updating UI  when room2 light is switched from the user app.
        public void room2SwitchChangeStatus(int n)
        {
            switchLightRoom2(n);
            if(n==1)
                room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            else
                 room2Switch.Background = new SolidColorBrush(Windows.UI.Colors.Red);

        }


        /// <summary>
        /// Code for working with camera
        /// </summary>
        private async void clickPic()
        {
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var cameraDevice = allVideoDevices[0];
            
            var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

            MediaCapture mediaCapture = new MediaCapture();
            await mediaCapture.InitializeAsync(settings);
            StorageFile file;

            string str= DateTime.UtcNow.ToString("yyyy-MMM-dd_HH-mm-ss")+".jpg"; ;
            CreationCollisionOption collisionOption = CreationCollisionOption.GenerateUniqueName;

            file = await KnownFolders.CameraRoll.CreateFileAsync(str, collisionOption);

            await mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);

        }


        /// <summary>
        /// Code to work with Ultrasonic 
        /// It will click a pic whenever the value of distance will be less than 10 cm.
        /// </summary>
        public async void initUltrasonic()
        {

            pinTrigger.Write(GpioPinValue.Low);

            ManualResetEvent mre = new ManualResetEvent(false);
            mre.WaitOne(500);
            Stopwatch pulseLength = new Stopwatch();

            pinTrigger.Write(GpioPinValue.High);
            mre.WaitOne(TimeSpan.FromMilliseconds(0.01));
            pinTrigger.Write(GpioPinValue.Low);
            while (pinEcho.Read() == GpioPinValue.Low)
            {
                //pulseLength.Restart();
            }
            pulseLength.Start();
            
            while (pinEcho.Read() == GpioPinValue.High)
            {
            }
            pulseLength.Stop();
            TimeSpan timeBetween = pulseLength.Elapsed;
            Debug.WriteLine(timeBetween.ToString());
            double distance = timeBetween.TotalSeconds * 17000;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                  () =>
                  {
                      status.Text = distance.ToString();
                  });
            if (distance<=10)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                  () =>
                  {
                      tts("Intruder, Intruder, Intruder, Intruder, Intruder");
                      clickPic();
                  });
                

            }
        }


        /// <summary>
        /// All button Clicks
        /// </summary>
        private async void room1switch_Click(object sender, RoutedEventArgs e)
        {
            if(pinValue1==GpioPinValue.High)
                await light1Switch(1);
            else
                await light1Switch(0);
        }


        private async void room2Switch_Click(object sender, RoutedEventArgs e)
        {
            if(pinValue2 == GpioPinValue.High)
                await light2Switch(1);
            else
                await light2Switch(0);
        }


        private void speechRecognizerSwitch_Click(object sender, RoutedEventArgs e)
        {
            if ((speechRecognizerSwitch.Background as SolidColorBrush).Color != Windows.UI.Colors.Green)
            {
                InitSpeechRecognizer(1);
                speechRecognizerSwitch.Background = new SolidColorBrush(Windows.UI.Colors.Green);
            }
            else
            {
                InitSpeechRecognizer(0);
                speechRecognizerSwitch.Background = new SolidColorBrush(Windows.UI.Colors.Red);
            }
        }

        private void cameraSwitch_Click(object sender, RoutedEventArgs e)
        {
            clickPic();
        }

        private void ultrasonicSwitch_Click(object sender, RoutedEventArgs e)
        {
            ThreadPoolTimer timer = ThreadPoolTimer.CreatePeriodicTimer((t) =>
            {
                initUltrasonic();
            }, TimeSpan.FromMinutes(0.1));
        }
    }
}