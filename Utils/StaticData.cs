namespace VolumeBox.Toolbox
{
    public static class StaticData
    {
        public const string SettingsResourcesPath = "Toolbox/Settings.asset";

        private static SettingsData _settings;

        public static SettingsData Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = ResourcesUtils.ResolveScriptable<SettingsData>(SettingsResourcesPath);
                }

                return _settings;
            }
        }

        public static bool HasSettings => ResourcesUtils.HasScriptable(SettingsResourcesPath);

        public static void CreateSettings()
        {
            _settings = ResourcesUtils.ResolveScriptable<SettingsData>(SettingsResourcesPath);
        }
    }
}
