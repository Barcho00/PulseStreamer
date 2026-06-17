using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HeartRateMonitor
{
    public class ProfileSaveData
    {
        public List<UserProfile> Profiles { get; set; } = new();
        public Guid ActiveProfileId { get; set; }
    }

    public class ProfileManager
    {
        private readonly string _filePath;
        public ObservableCollection<UserProfile> Profiles { get; private set; } = new();
        public Guid ActiveProfileId { get; set; }

        public UserProfile? ActiveProfile => Profiles.FirstOrDefault(p => p.Id == ActiveProfileId);

        public ProfileManager()
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles.json");
        }

        public void LoadProfiles()
        {
            AppLogger.Info("Profiles", "Loading profiles...");
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
                    var data = JsonSerializer.Deserialize<ProfileSaveData>(json);
                    if (data != null && data.Profiles.Count > 0)
                    {
                        Profiles = new ObservableCollection<UserProfile>(data.Profiles);
                        ActiveProfileId = data.ActiveProfileId;
                        AppLogger.Info("Profiles", $"Loaded {Profiles.Count} profiles. Active profile ID: {ActiveProfileId}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Profiles", "Error loading profiles", ex);
            }

            // Fallback: Create default profile if none loaded
            CreateDefaultProfile();
        }

        private System.Windows.Threading.DispatcherTimer? _saveTimer;

        public void SaveProfiles()
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _saveTimer.Tick += (s, e) =>
                {
                    _saveTimer.Stop();
                    ForceSaveProfiles();
                };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public void ForceSaveProfiles()
        {
            try
            {
                var data = new ProfileSaveData
                {
                    Profiles = Profiles.ToList(),
                    ActiveProfileId = ActiveProfileId
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_filePath, json, System.Text.Encoding.UTF8);
                AppLogger.Debug("Profiles", "Profiles saved to disk.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Profiles", "Error saving profiles", ex);
            }
        }

        public UserProfile AddProfile(string name, int age, double weight, string gender)
        {
            var profile = new UserProfile
            {
                Name = name,
                Age = age,
                Weight = weight,
                Gender = gender
            };

            Profiles.Add(profile);
            ActiveProfileId = profile.Id;
            SaveProfiles();
            AppLogger.Info("Profiles", $"Added profile: {name} (Age={age})");
            return profile;
        }

        public bool DeleteProfile(Guid id)
        {
            if (Profiles.Count <= 1)
            {
                AppLogger.Warn("Profiles", "Cannot delete the last remaining profile.");
                return false; // Cannot delete the last remaining profile
            }

            var profileToDelete = Profiles.FirstOrDefault(p => p.Id == id);
            if (profileToDelete != null)
            {
                Profiles.Remove(profileToDelete);
                if (ActiveProfileId == id)
                {
                    ActiveProfileId = Profiles.First().Id;
                }
                SaveProfiles();
                AppLogger.Info("Profiles", $"Deleted profile: {profileToDelete.Name} (ID={id})");
                return true;
            }
            return false;
        }

        private void CreateDefaultProfile()
        {
            AppLogger.Info("Profiles", "Creating default profile");
            Profiles.Clear();
            var defaultProfile = new UserProfile
            {
                Name = "Tomek",
                Age = 30,
                Weight = 75,
                Gender = "Male",
                LastDeviceAddress = string.Empty,
                LastDeviceName = string.Empty
            };
            Profiles.Add(defaultProfile);
            ActiveProfileId = defaultProfile.Id;
            SaveProfiles();
        }
    }
}
