//#define ACCESS_MOBILE_SERVICE
#define ACCESS_IOT_HUB
using IoTDevice;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace RPiIoTHubApp
{
#if (ACCESS_MOBILE_SERVICE)
    using Microsoft.WindowsAzure.MobileServices;
#endif
#if (ACCESS_IOT_HUB)
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;
    using System.Net;
    using Windows.Devices.Gpio;
#endif
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Device Entry Configuration
        string DeviceEntryEndPoint = "http://[MobileAppName].azurewebsites.net";

        // Identifier of this board. this value will be set by this app.
        string deviceId = "";

        // IoT Hub Configuration
        string IoTHubEndpoint = "JPHack.azure-devices.net";
        string DeviceKey = "zPmuVka/fdpCMeryWyqnDCpVijtPgu8OZpYm7M4f9a0=";
        bool IoTServiceAvailabled = true;

        private DispatcherTimer uploadTimer;
        private long uploadIntervalMSec = 10000;

        IoTKitHoLSensor sensor;

        private const int LED_PIN_A = 5;
        private const int LED_PIN_B = 6;
        private const int LED_PIN_C = 13;

        private GpioPin pinA;
        private GpioPin pinB;
        private GpioPin pinC;
        //private GpioPinValue pinValue;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            FixDeviceId();
            var result = await TryConnect();
            sensor = IoTKitHoLSensor.GetCurrent(IoTKitHoLSensor.TemperatureSensor.BME280);

            InitializeUpload();

            InitGPIO();
        }

        async Task<bool> TryConnect()
        {
            bool result = false;
            var client = new Windows.Web.Http.HttpClient();
            client.DefaultRequestHeaders.Add("device-id", deviceId.ToString());
            client.DefaultRequestHeaders.Add("device-message", "Hello from RPi2");
            var response = client.GetAsync(new Uri("http://egholservice.azurewebsites.net/api/DeviceConnect"), HttpCompletionOption.ResponseContentRead);
            response.AsTask().Wait();
            var responseResult = response.GetResults();
            if (responseResult.StatusCode == Windows.Web.Http.HttpStatusCode.Ok)
            {
                result = true;
                var received = await responseResult.Content.ReadAsStringAsync();
                Debug.WriteLine("Recieved - " + received);
            }
            else
            {
                Debug.WriteLine("TryConnect Failed - " + responseResult.StatusCode);
            }
            return result;
        }

        private void FixDeviceId()
        {
            foreach (var hn in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
            {
                IPAddress ipAddr;
                if (!hn.DisplayName.EndsWith(".local") && !IPAddress.TryParse(hn.DisplayName, out ipAddr))
                {
                    deviceId = hn.DisplayName;
                    break;
                }
            }
        }

        void InitializeUpload()
        {
            EntryDevice();
            if (IoTServiceAvailabled)
            {
                SetupIoTHub();
                uploadTimer = new DispatcherTimer();
                uploadTimer.Interval = TimeSpan.FromMilliseconds(uploadIntervalMSec);
                uploadTimer.Tick += UploadTimer_Tick;
                uploadTimer.Start();
            }
        }

        private void UploadTimer_Tick(object sender, object e)
        {
            var sensorReading = sensor.TakeMeasurement();
            lock (this)
            {
                lastTemperature = sensorReading.Temperature;
                lastHumidity = sensorReading.Humidity;
                lastPressure = sensorReading.Pressure;
            }

            uploadTimer.Stop();
            Upload();
            uploadTimer.Start();
        }

        int counter = 0;

        async void Upload()
        {
#if (ACCESS_IOT_HUB)
            var sensorReading = new Models.SensorReading();
            lock (this)
            {
                sensorReading.deviceId = deviceId.ToString();
                sensorReading.time = DateTime.Now;
                sensorReading.Temperature = lastTemperature;
                sensorReading.Humidity = lastHumidity;
                sensorReading.Pressure = lastPressure;
            }
            var payload = JsonConvert.SerializeObject(sensorReading);
            var message = new Message(System.Text.UTF8Encoding.UTF8.GetBytes(payload));
            try
            {
                await deviceClient.SendEventAsync(message);
                Debug.WriteLine("Measured[" + counter++ +"]" + " T=" + sensorReading.Temperature + ", H=" + sensorReading.Humidity + ", P=" + sensorReading.Pressure);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Event Hub Send Failed:" + ex.Message);
            }
#endif
        }


#if (ACCESS_MOBILE_SERVICE)
        MobileServiceClient mobileService;
#endif
        private async void EntryDevice()
        {
#if (ACCESS_MOBILE_SERVICE)
            if (mobileService == null)
            {
                mobileService = new MobileServiceClient(DeviceEntryEndPoint);
            }
            var table = mobileService.GetTable<Models.DeviceEntry>();
            var registered = await table.Where((de) => de.DeviceId == deviceId.ToString()).ToListAsync();

            bool registed = false;
            if (registered != null && registered.Count > 0)
            {
                foreach (var re in registered)
                {
                    if (re.ServiceAvailable)
                    {
                        IoTHubEndpoint = re.IoTHubEndpoint;
                        DeviceKey = re.DeviceKey;
                        Debug.WriteLine("IoT Hub Service Avaliabled");
                    }
                    registed = true;
                    break;
                }
            }
            if (!registed)
            {
                var entry = new Models.DeviceEntry()
                {
                    DeviceId = deviceId.ToString(),
                    ServiceAvailable = false,
                    IoTHubEndpoint = IoTHubEndpoint,
                    DeviceKey = DeviceKey
                };
                await table.InsertAsync(entry);
            }
#else
            IoTServiceAvailabled = true;
#endif
        }
#if (ACCESS_IOT_HUB)
        DeviceClient deviceClient;
        string iotHubConnectionString = "";
#endif
        private void SetupIoTHub()
        {
#if (ACCESS_IOT_HUB)
            iotHubConnectionString = "HostName=" + IoTHubEndpoint + ";DeviceId=" + deviceId + ";SharedAccessKey=" + DeviceKey;
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(iotHubConnectionString, Microsoft.Azure.Devices.Client.TransportType.Http1);
                Debug.Write("IoT Hub Connected.");
                ReceiveCommands();
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
#endif
        }
#if (ACCESS_IOT_HUB)
        async Task ReceiveCommands()
        {
            Debug.WriteLine("\nDevice waiting for commands from IoTHub...\n");
            Message receivedMessage;
            string messageData;

            while (true)
            {
                try
                {
                    receivedMessage = await deviceClient.ReceiveAsync();

                    if (receivedMessage != null)
                    {
                        messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                        Debug.WriteLine("\t{0}> Received message: {1}", DateTime.Now.ToLocalTime(), messageData);

                        if (messageData.CompareTo("R") == 0)
                        {
                            pinA.Write(GpioPinValue.High);
                            pinB.Write(GpioPinValue.Low);
                            pinC.Write(GpioPinValue.High);
                        }
                        else if (messageData.CompareTo("G") == 0)
                        {
                            pinA.Write(GpioPinValue.High);
                            pinB.Write(GpioPinValue.High);
                            pinC.Write(GpioPinValue.Low);
                        }
                        else if (messageData.CompareTo("B") == 0)
                        {
                            pinA.Write(GpioPinValue.Low);
                            pinB.Write(GpioPinValue.High);
                            pinC.Write(GpioPinValue.High);
                        }
                        else if (messageData.CompareTo("A") == 0)
                        {
                            pinA.Write(GpioPinValue.Low);
                            pinB.Write(GpioPinValue.Low);
                            pinC.Write(GpioPinValue.Low);
                        }
                        else
                        {
                            pinA.Write(GpioPinValue.High);
                            pinB.Write(GpioPinValue.High);
                            pinC.Write(GpioPinValue.High);
                        }

                        await deviceClient.CompleteAsync(receivedMessage);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("IoT Hub Receive Failed.");
                    Debug.WriteLine(ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

        }
#endif
        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                pinA = pinB = null;
                Debug.WriteLine("There is no GPIO controller on this device.");
                return;
            }

            pinA = gpio.OpenPin(LED_PIN_A);
            pinA.Write(GpioPinValue.High);
            pinA.SetDriveMode(GpioPinDriveMode.Output);

            pinB = gpio.OpenPin(LED_PIN_B);
            pinB.Write(GpioPinValue.High);
            pinB.SetDriveMode(GpioPinDriveMode.Output);

            pinC = gpio.OpenPin(LED_PIN_C);
            pinC.Write(GpioPinValue.High);
            pinC.SetDriveMode(GpioPinDriveMode.Output);

            Debug.WriteLine("GPIO pin initialized correctly.");
        }

        double lastHumidity = 0;
        double lastPressure = 0;
        double lastTemperature = 0;
    }
}
