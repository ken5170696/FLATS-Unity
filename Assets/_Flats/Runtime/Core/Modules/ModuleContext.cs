using System;
using System.Collections.Generic;
using System.Linq;

namespace Flats.Modules
{
    // Mod API 1.2.0. The host attaches a context to a managed module that implements
    // IModuleContextReceiver before Initialize. It gives the module its installed
    // directory (for AssetBundles and data files beside the entry assembly) and the
    // player's current values for the settings its manifest declares, and raises
    // SettingsChanged whenever the Mod Center saves new values for the running module.
    public interface IModuleContext
    {
        // Absolute path of the immutable installed package directory.
        string Directory { get; }
        // One normalized value per declared setting, in manifest order. Empty when the
        // manifest declares no settings.
        IReadOnlyList<SettingValue> Settings { get; }
        // Raised on the Unity main thread with the new normalized values. Modules read
        // Settings or the argument; both hold the same values.
        event Action<IReadOnlyList<SettingValue>> SettingsChanged;
        string GameVersion { get; }
        string ApiVersion { get; }
    }

    public interface IModuleContextReceiver
    {
        void Attach(IModuleContext context);
    }

    // Host-side implementation. Values are always normalized against the manifest so a
    // module never sees an unknown id, an out-of-range number or a missing setting.
    public sealed class ModuleContext : IModuleContext
    {
        readonly ModuleSettingSpec[] specs;
        SettingValue[] values;
        public string Directory { get; }
        public string GameVersion { get; }
        public string ApiVersion { get; }
        public IReadOnlyList<SettingValue> Settings => Array.AsReadOnly(values.Select(Copy).ToArray());
        public event Action<IReadOnlyList<SettingValue>> SettingsChanged;

        public ModuleContext(string directory, ModuleSettingSpec[] settings, IEnumerable<SettingValue> stored,
            string gameVersion = ModRules.GameVersion, string apiVersion = ModRules.ApiVersion)
        {
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
            specs = settings ?? new ModuleSettingSpec[0];
            values = ModuleSettingsSchema.Normalize(specs, stored);
            GameVersion = gameVersion; ApiVersion = apiVersion;
        }

        // Returns true when at least one value changed; only then are listeners notified.
        public bool Update(IEnumerable<SettingValue> stored)
        {
            var next = ModuleSettingsSchema.Normalize(specs, stored);
            bool changed = next.Length != values.Length || next.Where((v, i) => v.id != values[i].id || v.value != values[i].value).Any();
            values = next;
            if (!changed) return false;
            SettingsChanged?.Invoke(Settings);
            return true;
        }

        static SettingValue Copy(SettingValue v) => new SettingValue { id = v.id, value = v.value };
    }
}
