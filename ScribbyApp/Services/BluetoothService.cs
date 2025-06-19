using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace ScribbyApp.Services;

public class BluetoothService : IDisposable
{
    private readonly IBluetoothLE _ble;
    private readonly IAdapter _adapter;
    private IDevice? _connectedDevice;

    public ICharacteristic? PrimaryWriteCharacteristic { get; private set; }
    public ObservableCollection<IDevice> DeviceList { get; }
    public bool IsScanning => _adapter.IsScanning;
    public bool IsConnected => _connectedDevice?.State == DeviceState.Connected;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler? ServiceDiscoveryCompleted;
    public event EventHandler? DeviceDisconnected; // <-- NEW DEDICATED EVENT

    public BluetoothService()
    {
        _ble = CrossBluetoothLE.Current;
        _adapter = CrossBluetoothLE.Current.Adapter;
        DeviceList = new ObservableCollection<IDevice>();

        _adapter.DeviceDiscovered += OnDeviceDiscovered;
        _adapter.ScanTimeoutElapsed += OnScanTimeoutElapsed;
        _adapter.DeviceConnected += OnAdapterDeviceConnected;
        _adapter.DeviceDisconnected += OnAdapterDeviceDisconnected; // This handles ALL disconnects
        _ble.StateChanged += OnStateChanged;
    }

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e)
    {
        if (e.Device != null && !string.IsNullOrEmpty(e.Device.Name) && DeviceList.All(d => d.Id != e.Device.Id))
        {
            MainThread.BeginInvokeOnMainThread(() => DeviceList.Add(e.Device));
        }
    }

    private void OnScanTimeoutElapsed(object? sender, EventArgs e)
    {
        StatusChanged?.Invoke(this, "Scan completed");
    }

    private async void OnAdapterDeviceConnected(object? sender, DeviceEventArgs e)
    {
        _connectedDevice = e.Device;
        StatusChanged?.Invoke(this, $"Connected to {e.Device.Name}. Discovering services...");
        await DiscoverServicesAndCharacteristicsAsync();
    }

    private void OnAdapterDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        // This method is the single source of truth for a disconnection event.
        _connectedDevice = null;
        PrimaryWriteCharacteristic = null;
        StatusChanged?.Invoke(this, "Device disconnected.");

        // --- THIS IS THE KEY ---
        // Fire our dedicated event so any listening page can react properly.
        DeviceDisconnected?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged(object? sender, BluetoothStateChangedArgs e)
    {
        StatusChanged?.Invoke(this, $"Bluetooth: {e.NewState}");
    }

    public async Task StartScanningAsync(int timeoutSeconds = 10)
    {
        if (_ble.State != BluetoothState.On)
        {
            StatusChanged?.Invoke(this, "Bluetooth is not enabled");
            return;
        }
        if (IsScanning) return;

        MainThread.BeginInvokeOnMainThread(() => DeviceList.Clear());
        try
        {
            _adapter.ScanTimeout = timeoutSeconds * 1000;
            StatusChanged?.Invoke(this, "Scanning for devices...");
            await _adapter.StartScanningForDevicesAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Scan error: {ex.Message}");
        }
    }

    public async Task<bool> ConnectToDeviceAsync(IDevice device)
    {
        try
        {
            if (IsScanning)
            {
                await _adapter.StopScanningForDevicesAsync();
            }
            StatusChanged?.Invoke(this, $"Connecting to {device.Name ?? "Unknown"}...");
            await _adapter.ConnectToDeviceAsync(device);
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Connection error: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connectedDevice != null)
        {
            await _adapter.DisconnectDeviceAsync(_connectedDevice);
        }
    }

    public async Task DiscoverServicesAndCharacteristicsAsync()
    {
        // This method remains unchanged...
        if (!IsConnected || _connectedDevice == null)
        {
            StatusChanged?.Invoke(this, "Not connected. Cannot discover services.");
            return;
        }

        try
        {
            PrimaryWriteCharacteristic = null; // Reset
            var servicesList = await _connectedDevice.GetServicesAsync();
            ICharacteristic? firstGeneralWritable = null;

            foreach (var service in servicesList)
            {
                var characteristics = await service.GetCharacteristicsAsync();
                foreach (var characteristic in characteristics)
                {
                    bool isGenerallyWritable = characteristic.CanWrite ||
                                               characteristic.Properties.HasFlag(CharacteristicPropertyType.Write) ||
                                               characteristic.Properties.HasFlag(CharacteristicPropertyType.WriteWithoutResponse);

                    if (isGenerallyWritable)
                    {
                        if (characteristic.Properties.HasFlag(CharacteristicPropertyType.Notify) ||
                            characteristic.Properties.HasFlag(CharacteristicPropertyType.Indicate))
                        {
                            PrimaryWriteCharacteristic = characteristic;
                            goto FoundCharacteristic;
                        }
                        if (firstGeneralWritable == null)
                        {
                            firstGeneralWritable = characteristic;
                        }
                    }
                }
            }

            if (PrimaryWriteCharacteristic == null)
            {
                PrimaryWriteCharacteristic = firstGeneralWritable;
            }

        FoundCharacteristic:
            if (PrimaryWriteCharacteristic != null)
            {
                StatusChanged?.Invoke(this, $"Service discovery complete. Ready.");
            }
            else
            {
                StatusChanged?.Invoke(this, "Discovery complete, but no writable characteristic found.");
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Service discovery error: {ex.Message}");
        }
        finally
        {
            ServiceDiscoveryCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SendCommandAsync(ICharacteristic characteristic, string command)
    {
        // This method remains unchanged...
        try
        {
            var bytes = Encoding.UTF8.GetBytes(command);
            var success = await characteristic.WriteAsync(bytes);
            if (success > 0)
                StatusChanged?.Invoke(this, $"Sent '{command}' successfully.");
            else
                StatusChanged?.Invoke(this, $"Failed to send '{command}'.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Send error: {ex.Message}");
            throw;
        }
    }

    public IDevice? GetCurrentlyConnectedDeviceSomehow()
    {
        return _connectedDevice;
    }

    public void Dispose()
    {
        // Unsubscribe from events
        _adapter.DeviceDiscovered -= OnDeviceDiscovered;
        _adapter.ScanTimeoutElapsed -= OnScanTimeoutElapsed;
        _adapter.DeviceConnected -= OnAdapterDeviceConnected;
        _adapter.DeviceDisconnected -= OnAdapterDeviceDisconnected;
        _ble.StateChanged -= OnStateChanged;
    }
}