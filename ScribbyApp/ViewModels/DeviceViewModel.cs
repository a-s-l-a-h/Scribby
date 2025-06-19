using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// Ensure the namespace matches the new folder structure
namespace ScribbyApp.ViewModels;

public class DeviceViewModel : INotifyPropertyChanged
{
    public IDevice Device { get; }

    private string _buttonText = "Connect";
    public string ButtonText
    {
        get => _buttonText;
        set => SetProperty(ref _buttonText, value);
    }

    private bool _isButtonEnabled = true;
    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set => SetProperty(ref _isButtonEnabled, value);
    }

    public DeviceViewModel(IDevice device)
    {
        Device = device;
    }

    public void UpdateState(bool isAnyDeviceConnected, IDevice? connectedDevice)
    {
        bool isThisDeviceConnected = isAnyDeviceConnected && connectedDevice?.Id == Device.Id;

        if (isThisDeviceConnected)
        {
            if (Device.State == DeviceState.Connecting)
            {
                ButtonText = "Connecting...";
                IsButtonEnabled = false;
            }
            else
            {
                ButtonText = "Connected";
                IsButtonEnabled = false;
            }
        }
        else
        {
            ButtonText = "Connect";
            IsButtonEnabled = !isAnyDeviceConnected;
        }
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;
        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    #endregion
}