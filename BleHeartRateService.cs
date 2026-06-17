using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace HeartRateMonitor
{
    public class DiscoveredDevice
    {
        public string Name { get; set; } = string.Empty;
        public ulong Address { get; set; }
        public int Rssi { get; set; }
        public string AddressString => FormatMacAddress(Address);

        private string FormatMacAddress(ulong address)
        {
            string hex = address.ToString("X12");
            return $"{hex.Substring(0, 2)}:{hex.Substring(2, 2)}:{hex.Substring(4, 2)}:{hex.Substring(6, 2)}:{hex.Substring(8, 2)}:{hex.Substring(10, 2)}";
        }
    }

    public class BleHeartRateService
    {
        private BluetoothLEAdvertisementWatcher? _advWatcher;
        private readonly HashSet<ulong> _discoveredAddresses = new();

        private BluetoothLEDevice? _bluetoothDevice;
        private GattDeviceService? _hrService;
        private GattCharacteristic? _hrCharacteristic;
        private GattDeviceService? _batteryService;
        private GattCharacteristic? _batteryCharacteristic;
        private bool _isConnecting = false;

        // GC Prevention: Keep strong static references to active WinRT objects
        private static readonly List<object> _strongGcKeeper = new();

        // Coast Mode fields
        private System.Timers.Timer? _coastTimer;
        private DateTime _lastPacketReceivedTime;
        private int _lastBpm = 0;
        private bool _inCoastMode = false;

        // Events for UI Communication
        public event Action<DiscoveredDevice>? DeviceDiscovered;
        public event Action<int>? HeartRateChanged;
        public event Action<int>? BatteryLevelChanged;
        public event Action<string>? ConnectionStatusChanged;
        public event Action<string>? ErrorOccurred;

        public bool IsScanning => _advWatcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;

        public void StartScanning()
        {
            AppLogger.Info("BLE", "StartScanning called");
            StopScanning();
            _discoveredAddresses.Clear();

            _advWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            // Filter for Heart Rate Service
            _advWatcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(GattServiceUuids.HeartRate);
            _advWatcher.Received += OnAdvertisementReceived;

            try
            {
                _advWatcher.Start();
                AppLogger.Info("BLE", "BLE scanner started successfully");
                ConnectionStatusChanged?.Invoke("Scanning...");
            }
            catch (Exception ex)
            {
                AppLogger.Error("BLE", "Failed to start BLE scanner", ex);
                ErrorOccurred?.Invoke($"Failed to start BLE scanner: {ex.Message}");
            }
        }

        public void StopScanning()
        {
            if (_advWatcher != null)
            {
                AppLogger.Info("BLE", "StopScanning called — stopping watcher");
                _advWatcher.Stop();
                _advWatcher.Received -= OnAdvertisementReceived;
                _advWatcher = null;
                ConnectionStatusChanged?.Invoke("Scan Stopped");
            }
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (_discoveredAddresses.Contains(args.BluetoothAddress))
                return;

            _discoveredAddresses.Add(args.BluetoothAddress);

            string name = args.Advertisement.LocalName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Heart Rate Sensor";
            }

            var device = new DiscoveredDevice
            {
                Name = name,
                Address = args.BluetoothAddress,
                Rssi = args.RawSignalStrengthInDBm
            };

            AppLogger.Info("BLE", $"Device discovered: {device.Name} [{device.AddressString}] RSSI={device.Rssi}dBm");
            DeviceDiscovered?.Invoke(device);
        }

        public async Task<bool> ConnectAsync(ulong bluetoothAddress)
        {
            AppLogger.Info("BLE", $"ConnectAsync called for address: {bluetoothAddress:X12}");
            _isConnecting = true;
            try
            {
                StopScanning();
                Disconnect();

                ConnectionStatusChanged?.Invoke("Connecting...");

                AppLogger.Info("BLE", "Calling BluetoothLEDevice.FromBluetoothAddressAsync...");
                _bluetoothDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (_bluetoothDevice == null)
                {
                    AppLogger.Error("BLE", "FromBluetoothAddressAsync returned null");
                    ErrorOccurred?.Invoke("Could not find the selected Bluetooth device.");
                    ConnectionStatusChanged?.Invoke("Disconnected");
                    return false;
                }
                AppLogger.Info("BLE", $"BluetoothLEDevice obtained: Name={_bluetoothDevice.Name}, ConnectionStatus={_bluetoothDevice.ConnectionStatus}");

                // Add a small delay for connection stabilization
                await Task.Delay(300);

                _bluetoothDevice.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;

                // Discover Heart Rate Service
                AppLogger.Info("BLE", "Discovering HR service (Cached)...");
                var servicesResult = await _bluetoothDevice.GetGattServicesForUuidAsync(GattServiceUuids.HeartRate, BluetoothCacheMode.Cached);
                AppLogger.Info("BLE", $"HR service Cached result: Status={servicesResult.Status}, Count={servicesResult.Services.Count}");
                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    AppLogger.Info("BLE", "Discovering HR service (Uncached)...");
                    servicesResult = await _bluetoothDevice.GetGattServicesForUuidAsync(GattServiceUuids.HeartRate, BluetoothCacheMode.Uncached);
                    AppLogger.Info("BLE", $"HR service Uncached result: Status={servicesResult.Status}, Count={servicesResult.Services.Count}");
                }

                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    AppLogger.Error("BLE", "HR service not found on device");
                    ErrorOccurred?.Invoke("The device does not support the standard Heart Rate Service or is out of range.");
                    Disconnect();
                    return false;
                }
                _hrService = servicesResult.Services[0];

                // Discover Heart Rate Characteristics
                AppLogger.Info("BLE", "Discovering HR characteristics (Cached)...");
                var charResult = await _hrService.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
                AppLogger.Info("BLE", $"HR chars Cached result: Status={charResult.Status}, Count={charResult.Characteristics.Count}");
                if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
                {
                    AppLogger.Info("BLE", "Discovering HR characteristics (Uncached)...");
                    charResult = await _hrService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    AppLogger.Info("BLE", $"HR chars Uncached result: Status={charResult.Status}, Count={charResult.Characteristics.Count}");
                }

                if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
                {
                    AppLogger.Error("BLE", "No HR characteristics found");
                    ErrorOccurred?.Invoke("No characteristics found for the Heart Rate Service.");
                    Disconnect();
                    return false;
                }

                _hrCharacteristic = charResult.Characteristics.FirstOrDefault(c => c.Uuid == GattCharacteristicUuids.HeartRateMeasurement);
                if (_hrCharacteristic == null)
                {
                    AppLogger.Error("BLE", "HeartRateMeasurement characteristic UUID not found");
                    ErrorOccurred?.Invoke("Heart Rate Measurement characteristic not found on this device.");
                    Disconnect();
                    return false;
                }
                AppLogger.Info("BLE", "HeartRateMeasurement characteristic found");

                // Write CCCD to enable Notify
                AppLogger.Info("BLE", "Writing CCCD to enable HR Notify...");
                var notifyStatus = await _hrCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                AppLogger.Info("BLE", $"CCCD write result: {notifyStatus}");

                if (notifyStatus != GattCommunicationStatus.Success)
                {
                    AppLogger.Error("BLE", $"Failed to subscribe to HR notifications: {notifyStatus}");
                    ErrorOccurred?.Invoke("Failed to subscribe to heart rate notifications.");
                    Disconnect();
                    return false;
                }

                _hrCharacteristic.ValueChanged += OnHeartRateValueChanged;
                AppLogger.Info("BLE", "HR ValueChanged handler registered");

                // Discover Battery Service
                try
                {
                    AppLogger.Info("BLE", "Discovering Battery service...");
                    var batteryServicesResult = await _bluetoothDevice.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached);
                    if (batteryServicesResult.Status != GattCommunicationStatus.Success || batteryServicesResult.Services.Count == 0)
                    {
                        batteryServicesResult = await _bluetoothDevice.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached);
                    }

                    if (batteryServicesResult.Status == GattCommunicationStatus.Success && batteryServicesResult.Services.Count > 0)
                    {
                        _batteryService = batteryServicesResult.Services[0];
                        AppLogger.Info("BLE", "Battery service found");

                        var batteryCharResult = await _batteryService.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
                        if (batteryCharResult.Status != GattCommunicationStatus.Success || batteryCharResult.Characteristics.Count == 0)
                        {
                            batteryCharResult = await _batteryService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        }

                        if (batteryCharResult.Status == GattCommunicationStatus.Success && batteryCharResult.Characteristics.Count > 0)
                        {
                            _batteryCharacteristic = batteryCharResult.Characteristics.FirstOrDefault(c => c.Uuid == GattCharacteristicUuids.BatteryLevel);
                            if (_batteryCharacteristic != null)
                            {
                                var readResult = await _batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Cached);
                                if (readResult.Status != GattCommunicationStatus.Success)
                                {
                                    readResult = await _batteryCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                                }

                                if (readResult.Status == GattCommunicationStatus.Success)
                                {
                                    var reader = DataReader.FromBuffer(readResult.Value);
                                    int batteryLevel = reader.ReadByte();
                                    AppLogger.Info("BLE", $"Initial battery level: {batteryLevel}%");
                                    BatteryLevelChanged?.Invoke(batteryLevel);
                                }

                                await _batteryCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                _batteryCharacteristic.ValueChanged += OnBatteryValueChanged;
                            }
                        }
                    }
                    else
                    {
                        AppLogger.Info("BLE", "Battery service not available on device");
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("BLE", $"Battery service discovery failed (non-critical): {ex.Message}");
                }

                // Add references to GC prevention keeper
                lock (_strongGcKeeper)
                {
                    _strongGcKeeper.Clear();
                    _strongGcKeeper.Add(_bluetoothDevice);
                    if (_hrService != null) _strongGcKeeper.Add(_hrService);
                    if (_hrCharacteristic != null) _strongGcKeeper.Add(_hrCharacteristic);
                    if (_batteryService != null) _strongGcKeeper.Add(_batteryService);
                    if (_batteryCharacteristic != null) _strongGcKeeper.Add(_batteryCharacteristic);
                }

                // Initialize Coast Mode check timer
                _lastPacketReceivedTime = DateTime.Now;
                _inCoastMode = false;
                _coastTimer = new System.Timers.Timer(1000);
                _coastTimer.Elapsed += CoastTimer_Elapsed;
                _coastTimer.Start();

                AppLogger.Info("BLE", "Connection successful — invoking Connected status");
                _isConnecting = false;
                ConnectionStatusChanged?.Invoke("Connected");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("BLE", "ConnectAsync failed", ex);
                _isConnecting = false;
                ErrorOccurred?.Invoke($"Connection failed: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        private void CoastTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            double elapsed = (DateTime.Now - _lastPacketReceivedTime).TotalSeconds;

            if (elapsed > 1.8) // We missed a packet (typically received every 1s)
            {
                if (!_inCoastMode)
                {
                    _inCoastMode = true;
                    AppLogger.Warn("BLE", $"Packet lost (elapsed={elapsed:F1}s). Entering Coast Mode (6s concealment)...");
                    ConnectionStatusChanged?.Invoke("Połączenie (Coast Mode)");
                }

                if (elapsed >= 6.0)
                {
                    // Coast Mode Timeout expired
                    AppLogger.Error("BLE", $"Coast Mode expired (elapsed={elapsed:F1}s) — disconnecting officially.");
                    
                    // Stop timer to avoid recursive calls
                    if (_coastTimer != null)
                    {
                        _coastTimer.Stop();
                    }

                    // Run disconnect on background thread
                    Task.Run(() => Disconnect());
                }
                else
                {
                    // Artificially keep drawing last known BPM
                    AppLogger.Debug("BLE", $"Coast Mode: sending artificial HR={_lastBpm} BPM (elapsed={elapsed:F1}s)");
                    HeartRateChanged?.Invoke(_lastBpm);
                }
            }
        }

        private void OnDeviceConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            AppLogger.Info("BLE", $"OnDeviceConnectionStatusChanged: {sender.ConnectionStatus} (isConnecting={_isConnecting}, inCoastMode={_inCoastMode})");
            
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected && !_isConnecting)
            {
                if (!_inCoastMode)
                {
                    _inCoastMode = true;
                    _lastPacketReceivedTime = DateTime.Now; // start timer from now
                    AppLogger.Warn("BLE", "Device disconnected at OS level. Starting 6s Coast Mode...");
                    ConnectionStatusChanged?.Invoke("Połączenie (Coast Mode)");
                }
            }
        }

        private void OnHeartRateValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                var reader = DataReader.FromBuffer(args.CharacteristicValue);
                byte flags = reader.ReadByte();

                int heartRate;
                bool is16Bit = (flags & 0x01) == 0x01;

                if (is16Bit)
                {
                    heartRate = reader.ReadUInt16();
                }
                else
                {
                    heartRate = reader.ReadByte();
                }

                _lastPacketReceivedTime = DateTime.Now;
                _lastBpm = heartRate;

                if (_inCoastMode)
                {
                    _inCoastMode = false;
                    AppLogger.Info("BLE", "Signal recovered. Exiting Coast Mode.");
                    ConnectionStatusChanged?.Invoke("Connected");
                }

                AppLogger.Debug("BLE", $"HR value received: {heartRate} BPM (16bit={is16Bit})");
                HeartRateChanged?.Invoke(heartRate);
            }
            catch (Exception ex)
            {
                AppLogger.Error("BLE", "Failed to parse heart rate measurement", ex);
            }
        }

        private void OnBatteryValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                var reader = DataReader.FromBuffer(args.CharacteristicValue);
                int batteryLevel = reader.ReadByte();
                BatteryLevelChanged?.Invoke(batteryLevel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse battery level: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            AppLogger.Info("BLE", "Disconnect called — disposing BLE resources");
            try
            {
                if (_coastTimer != null)
                {
                    _coastTimer.Stop();
                    _coastTimer.Dispose();
                    _coastTimer = null;
                }
                _inCoastMode = false;

                if (_hrCharacteristic != null)
                {
                    _hrCharacteristic.ValueChanged -= OnHeartRateValueChanged;
                    _hrCharacteristic = null;
                    AppLogger.Debug("BLE", "HR characteristic disposed");
                }

                if (_batteryCharacteristic != null)
                {
                    _batteryCharacteristic.ValueChanged -= OnBatteryValueChanged;
                    _batteryCharacteristic = null;
                    AppLogger.Debug("BLE", "Battery characteristic disposed");
                }

                _hrService?.Dispose();
                _hrService = null;

                _batteryService?.Dispose();
                _batteryService = null;

                if (_bluetoothDevice != null)
                {
                    AppLogger.Info("BLE", $"Disposing BluetoothLEDevice: {_bluetoothDevice.Name}");
                    _bluetoothDevice.ConnectionStatusChanged -= OnDeviceConnectionStatusChanged;
                    _bluetoothDevice.Dispose();
                    _bluetoothDevice = null;
                }

                lock (_strongGcKeeper)
                {
                    _strongGcKeeper.Clear();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("BLE", "Error disposing BLE resources", ex);
            }
            finally
            {
                AppLogger.Info("BLE", "Disconnect complete — invoking Disconnected status");
                ConnectionStatusChanged?.Invoke("Disconnected");
            }
        }
    }
}
