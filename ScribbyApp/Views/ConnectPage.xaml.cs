using Plugin.BLE.Abstractions.Contracts;
using ScribbyApp.Services;
using ScribbyApp.ViewModels; // <-- ADD THIS using statement
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ScribbyApp.Views;

// --- THE DeviceViewModel CLASS DEFINITION HAS BEEN REMOVED FROM THIS FILE ---

public partial class ConnectPage : ContentPage
{
    private readonly BluetoothService _bluetoothService;
    public ObservableCollection<DeviceViewModel> DiscoveredDevices { get; } = new();

    public ConnectPage(BluetoothService bluetoothService)
    {
        InitializeComponent();
        _bluetoothService = bluetoothService;
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _bluetoothService.StatusChanged += OnStatusChanged;
        _bluetoothService.ServiceDiscoveryCompleted += OnServiceDiscoveryCompleted;
        _bluetoothService.DeviceDisconnected += OnDeviceDisconnected;

        _bluetoothService.DeviceList.CollectionChanged += OnDeviceListChanged;

        RefreshDeviceList();
        UpdateAllDeviceButtonStates();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bluetoothService.StatusChanged -= OnStatusChanged;
        _bluetoothService.ServiceDiscoveryCompleted -= OnServiceDiscoveryCompleted;
        _bluetoothService.DeviceDisconnected -= OnDeviceDisconnected;
        _bluetoothService.DeviceList.CollectionChanged -= OnDeviceListChanged;
    }

    private void OnDeviceListChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshDeviceList);
    }

    private void RefreshDeviceList()
    {
        DiscoveredDevices.Clear();
        foreach (var device in _bluetoothService.DeviceList)
        {
            DiscoveredDevices.Add(new DeviceViewModel(device));
        }
        UpdateAllDeviceButtonStates();
    }

    private void UpdateAllDeviceButtonStates()
    {
        var connectedDevice = _bluetoothService.GetCurrentlyConnectedDeviceSomehow();
        foreach (var vm in DiscoveredDevices)
        {
            vm.UpdateState(_bluetoothService.IsConnected, connectedDevice);
        }
        DisconnectButton.IsEnabled = _bluetoothService.IsConnected;
        ScanButton.IsEnabled = !_bluetoothService.IsConnected;
    }

    private void OnDeviceDisconnected(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionLabel.Text = "Not Connected";
            ConnectionLabel.TextColor = Colors.Red;
            UpdateAllDeviceButtonStates();
        });
    }

    private void OnStatusChanged(object? sender, string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = status;
        });
    }

    private async void OnServiceDiscoveryCompleted(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_bluetoothService.PrimaryWriteCharacteristic != null)
            {
                StatusLabel.Text = "Device ready. You can now use the Control and Script tabs.";
                ConnectionLabel.Text = $"Connected to: {_bluetoothService.GetCurrentlyConnectedDeviceSomehow()?.Name}";
                ConnectionLabel.TextColor = Colors.Green;
            }
            else
            {
                StatusLabel.Text = "Connection failed: No usable characteristic found.";
                await DisplayAlert("Connection Error", "Could not find a suitable writable characteristic on this device. Cannot proceed.", "OK");
                await _bluetoothService.DisconnectAsync();
            }
            UpdateAllDeviceButtonStates();
        });
    }

    private async void OnScanButtonClicked(object sender, EventArgs e)
    {
        if (_bluetoothService.IsConnected) return;

        ScanButton.IsEnabled = false;
        ScanButton.Text = "Scanning...";

        _bluetoothService.DeviceList.Clear();
        DiscoveredDevices.Clear();

        await _bluetoothService.StartScanningAsync();

        ScanButton.Text = "Start Scan";
    }

    private async void OnConnectButtonClicked(object sender, EventArgs e)
    {
        if (sender is Button && ((Button)sender).CommandParameter is DeviceViewModel vm)
        {
            vm.ButtonText = "Connecting...";
            vm.IsButtonEnabled = false;

            UpdateAllDeviceButtonStates();

            bool connected = await _bluetoothService.ConnectToDeviceAsync(vm.Device);

            if (!connected)
            {
                UpdateAllDeviceButtonStates();
            }
        }
    }

    private async void OnDisconnectButtonClicked(object sender, EventArgs e)
    {
        await _bluetoothService.DisconnectAsync();
    }
}