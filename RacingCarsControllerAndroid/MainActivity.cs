using Android.Util;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.ProgressIndicator;
using Google.Android.Material.Snackbar;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using RacingCarsController.Common;
using Xamarin.Essentials;

namespace RacingCarsControllerAndroid
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;
        private PermissionsHelpers _permissionsHelpers = null!;
        private DeviceItemAdapter _deviceItemAdapter = null!;
        private MessageBroadcaster _messageBroadcaster = null!;
        private readonly System.Timers.Timer _buttonStateTimer = new(TimeSpan.FromMilliseconds(50));
        private IRacingCar? _selectedCar;
        private LinearLayout _container = null!;
        private Button _scanButton = null!;
        private LinearProgressIndicator _scanProgressIndicator = null!;
        private LinearLayout _selectedCarLayout = null!;
        private TextView _selectedCarTextView = null!;
        private TextView _selectedCarBatteryTextView = null!;
        private Button _lightsButton = null!;
        private Button _turboButton = null!;
        private Button _buttonForward = null!;
        private Button _buttonBackward = null!;
        private Button _buttonLeft = null!;
        private Button _buttonRight = null!;

        private bool _isLightsOn;
        private bool _isTurboOn;

        private readonly HashSet<Keycode> _heldKeys = new();
        private bool _analogForward;
        private bool _analogReverse;
        private bool _stickLeft;
        private bool _stickRight;
        private bool _isForwardActive;
        private bool _isReverseActive;
        private bool _isLeftActive;
        private bool _isRightActive;

        private const float StickDeadzone = 0.20f;
        private const float TriggerDeadzone = 0.15f;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Platform.Init(this, savedInstanceState);

            _permissionsHelpers = new PermissionsHelpers(this);
            _adapter.ScanMode = Plugin.BLE.Abstractions.Contracts.ScanMode.Balanced;
            _adapter.DeviceDiscovered += OnDeviceDiscovered;
            _messageBroadcaster = new MessageBroadcaster();
            _buttonStateTimer.Elapsed += OnButtonStateTimerElapsed;

            SetContentView(Resource.Layout.activity_main);

            var devicesList = FindViewById<RecyclerView>(Resource.Id.devicesList)!;
            devicesList.SetLayoutManager(new LinearLayoutManager(this));
            _deviceItemAdapter = new DeviceItemAdapter();
            devicesList.SetAdapter(_deviceItemAdapter);
            _deviceItemAdapter.ItemClick += OnItemClick;

            _container = FindViewById<LinearLayout>(Resource.Id.container)!;
            _scanButton = FindViewById<Button>(Resource.Id.buttonScan)!;
            _scanButton.Click += ScanButton_Click;
            _scanProgressIndicator = FindViewById<LinearProgressIndicator>(Resource.Id.scanProgressIndicator)!;

            FindViewById<Button>(Resource.Id.disconnectButton)!.Click += DisconnectButton_Click;
            _selectedCarLayout = FindViewById<LinearLayout>(Resource.Id.selectedCar_Layout)!;
            _selectedCarLayout.Visibility = ViewStates.Invisible;
            _selectedCarTextView = FindViewById<TextView>(Resource.Id.selectedCarName_Text)!;
            _selectedCarBatteryTextView = FindViewById<TextView>(Resource.Id.selectedCarBattery_Text)!;

            _lightsButton = FindViewById<Button>(Resource.Id.buttonLights)!;
            _lightsButton.Click += OnLightsButtonClicked;
            _turboButton = FindViewById<Button>(Resource.Id.buttonTurbo)!;
            _turboButton.Click += OnTurboButtonClicked;

            _buttonForward = FindViewById<Button>(Resource.Id.buttonForward)!;
            _buttonBackward = FindViewById<Button>(Resource.Id.buttonBackward)!;
            _buttonLeft = FindViewById<Button>(Resource.Id.buttonLeft)!;
            _buttonRight = FindViewById<Button>(Resource.Id.buttonRight)!;
            _buttonForward.Click += OnControlButtonClicked;
            _buttonBackward.Click += OnControlButtonClicked;
            _buttonLeft.Click += OnControlButtonClicked;
            _buttonRight.Click += OnControlButtonClicked;
        }

        public override bool DispatchGenericMotionEvent(MotionEvent e)
        {
            if ((e.Source & (InputSourceType.Gamepad | InputSourceType.Joystick)) != 0)
            {
                var stickX = e.GetAxisValue(Axis.X);
                _stickLeft = stickX < -StickDeadzone;
                _stickRight = stickX > StickDeadzone;

                var rightTrigger = Math.Max(e.GetAxisValue(Axis.Rtrigger), e.GetAxisValue(Axis.Gas));
                var leftTrigger = Math.Max(e.GetAxisValue(Axis.Ltrigger), e.GetAxisValue(Axis.Brake));
                _analogForward = rightTrigger > TriggerDeadzone;
                _analogReverse = leftTrigger > TriggerDeadzone;
                UpdateControllerButtons();
                return true;
            }

            return base.DispatchGenericMotionEvent(e);
        }

        public override bool DispatchKeyEvent(KeyEvent e)
        {
            if ((e.Source & (InputSourceType.Gamepad | InputSourceType.Joystick)) != 0)
            {
                var isDown = e.Action == KeyEventActions.Down;
                switch (e.KeyCode)
                {
                    case Keycode.ButtonA:
                    case Keycode.ButtonR1:
                    case Keycode.DpadUp:
                        SetKeyHeld(e.KeyCode, isDown);
                        UpdateControllerButtons();
                        return true;

                    case Keycode.ButtonX:
                    case Keycode.ButtonL1:
                    case Keycode.DpadDown:
                        SetKeyHeld(e.KeyCode, isDown);
                        UpdateControllerButtons();
                        return true;

                    case Keycode.DpadLeft:
                        SetKeyHeld(e.KeyCode, isDown);
                        UpdateControllerButtons();
                        return true;

                    case Keycode.DpadRight:
                        SetKeyHeld(e.KeyCode, isDown);
                        UpdateControllerButtons();
                        return true;

                    case Keycode.ButtonB:
                        if (isDown && e.RepeatCount == 0) _lightsButton.PerformClick();
                        return true;

                    case Keycode.ButtonY:
                        if (isDown && e.RepeatCount == 0) _turboButton.PerformClick();
                        return true;

                    case Keycode.DpadCenter:
                        return true;
                }
            }

            return base.DispatchKeyEvent(e);
        }

        private void UpdateControllerButtons()
        {
            var forward = _heldKeys.Contains(Keycode.ButtonA)
                || _heldKeys.Contains(Keycode.ButtonR1)
                || _heldKeys.Contains(Keycode.DpadUp)
                || _analogForward;
            var reverse = !forward && (_heldKeys.Contains(Keycode.ButtonX)
                || _heldKeys.Contains(Keycode.ButtonL1)
                || _heldKeys.Contains(Keycode.DpadDown)
                || _analogReverse);
            var left = _heldKeys.Contains(Keycode.DpadLeft) || _stickLeft;
            var right = !left && (_heldKeys.Contains(Keycode.DpadRight) || _stickRight);

            if (_isForwardActive != forward || _isReverseActive != reverse
                || _isLeftActive != left || _isRightActive != right)
            {
                _isForwardActive = forward;
                _isReverseActive = reverse;
                _isLeftActive = left;
                _isRightActive = right;
                SendCommand();
            }
        }

        private void SetKeyHeld(Keycode keyCode, bool isDown)
        {
            if (isDown)
                _heldKeys.Add(keyCode);
            else
                _heldKeys.Remove(keyCode);
        }

        protected override void OnPause()
        {
            _buttonStateTimer.Stop();
            base.OnPause();
            _heldKeys.Clear();
            _analogForward = false;
            _analogReverse = false;
            _stickLeft = false;
            _stickRight = false;
            UpdateControllerButtons();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _buttonStateTimer.Start();
        }

        protected override void OnDestroy()
        {
            _buttonStateTimer.Dispose();
            _adapter.DeviceDiscovered -= OnDeviceDiscovered;
            _messageBroadcaster.CancelBroadcasting();
            base.OnDestroy();
        }

        private void OnButtonStateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e) =>
            MainThread.BeginInvokeOnMainThread(SendCommand);

        private void OnControlButtonClicked(object? sender, EventArgs e) => SendCommand();

        private void OnTurboButtonClicked(object? sender, EventArgs e)
        {
            _isTurboOn = !_isTurboOn;
            _turboButton.Selected = _isTurboOn;
            SendCommand();
        }

        private void OnLightsButtonClicked(object? sender, EventArgs e)
        {
            _isLightsOn = !_isLightsOn;
            _lightsButton.Selected = _isLightsOn;
            SendCommand();
        }

        private void SendCommand()
        {
            if (_messageBroadcaster == null) return;

            var command = new CarCommand(
                _buttonForward.Pressed || _isForwardActive,
                _buttonBackward.Pressed || _isReverseActive,
                _buttonLeft.Pressed || _isLeftActive,
                _buttonRight.Pressed || _isRightActive,
                _isLightsOn,
                _isTurboOn);
            _messageBroadcaster.QueueCommand(command);
        }

        private async void ScanButton_Click(object? sender, EventArgs e) => await ToggleScanAsync();

        private async void DisconnectButton_Click(object? sender, EventArgs e) => await DisconnectCarAsync();

        private void OnDeviceDiscovered(object? sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Device.Name))
                MainThread.BeginInvokeOnMainThread(() => _deviceItemAdapter.Add(e.Device));
        }

        public override void OnRequestPermissionsResult(
            int requestCode,
            string[] permissions,
            Android.Content.PM.Permission[] grantResults)
        {
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private async Task ToggleScanAsync()
        {
            if (_adapter.IsScanning)
                await StopScanAsync();
            else
                await StartScanAsync();
        }

        private async Task StartScanAsync()
        {
            _scanButton.Enabled = false;
            try
            {
                var permissionStatus = await _permissionsHelpers.CheckAndRequestBluetoothPermissions();
                if (permissionStatus != PermissionStatus.Granted)
                {
                    DisplayError("Bluetooth permission is required to scan for cars.");
                    return;
                }

                if (!CrossBluetoothLE.Current.IsOn)
                {
                    DisplayError("Bluetooth is turned off.");
                    return;
                }

                _deviceItemAdapter.Clear();
                await _adapter.StartScanningForDevicesAsync();
                if (_adapter.IsScanning)
                {
                    _scanProgressIndicator.Visibility = ViewStates.Visible;
                    _scanButton.Text = "Stop scan";
                }
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
                DisplayError(ex.Message);
            }
            finally
            {
                _scanButton.Enabled = true;
            }
        }

        private async Task StopScanAsync()
        {
            _scanButton.Enabled = false;
            try
            {
                await _adapter.StopScanningForDevicesAsync();
                _scanProgressIndicator.Visibility = ViewStates.Invisible;
                _scanButton.Text = "Scan devices";
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
                DisplayError(ex.Message);
            }
            finally
            {
                _scanButton.Enabled = true;
            }
        }

        private async void OnItemClick(object? sender, int position)
        {
            var device = _deviceItemAdapter.Get(position);
            await StopScanAsync();
            await OnDeviceSelected(device);
        }

        private async Task OnDeviceSelected(IDevice device)
        {
            await DisconnectCarAsync();
            try
            {
                var car = await ConnectDeviceAsync(device);
                if (car == null) return;

                _selectedCar = car;
                _selectedCar.BatteryLevelChanged += OnBatteryLevelChanged;
                _messageBroadcaster.SetCar(_selectedCar);
                _selectedCarLayout.Visibility = ViewStates.Visible;
                _selectedCarTextView.Text = device.Name;
                SendCommand();
            }
            catch (Exception ex)
            {
                LogMessage(ex.ToString());
                DisplayError(ex.Message);
            }
        }

        private async Task<IRacingCar?> ConnectDeviceAsync(IDevice device)
        {
            await _adapter.ConnectToDeviceAsync(device);
            IRacingCar? car = device.Name is string name && FerrariCar.IsSupportedModel(name)
                ? new FerrariCar(new AndroidBLEDevice(device), new SystemDiagnosticsLogger())
                : device.Name is string brandbaseName && BrandbaseCar.IsSupportedModel(brandbaseName)
                    ? new BrandbaseCar(new AndroidBLEDevice(device), new SystemDiagnosticsLogger())
                    : null;

            if (car == null)
            {
                await _adapter.DisconnectDeviceAsync(device);
                DisplayError("This car model is not supported.");
                return null;
            }

            await car.SubscribeToBatteryNotifications();
            return car;
        }

        private async Task DisconnectCarAsync()
        {
            if (_selectedCar == null) return;

            _messageBroadcaster.CancelBroadcasting();
            _selectedCar.BatteryLevelChanged -= OnBatteryLevelChanged;
            await _selectedCar.DisposeAsync();
            _selectedCar = null;
            _selectedCarLayout.Visibility = ViewStates.Invisible;
            _selectedCarTextView.Text = string.Empty;
            _selectedCarBatteryTextView.Text = string.Empty;
        }

        private void OnBatteryLevelChanged(object? sender, int level)
        {
            MainThread.BeginInvokeOnMainThread(() => _selectedCarBatteryTextView.Text = $"{level}%");
        }

        private void DisplayError(string text) =>
            Snackbar.Make(_container, $"Error: {text}", Snackbar.LengthLong).Show();

        private static void LogMessage(string text)
        {
            Log.Info("APP LOG", text);
            System.Diagnostics.Debug.WriteLine($"APP LOG: {text}");
        }
    }
}
