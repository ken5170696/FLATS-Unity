using System.IO;

namespace Flats.Modules
{
    // A data import adapts the existing manifest to the built-in renderer. It
    // neither installs the package nor claims to satisfy its session identity.
    public static class WebCrosshairPreset
    {
        public static PackageManifest Validate(PackageManifest manifest)
        {
            if (manifest == null) throw new InvalidDataException("A crosshair manifest is required");
            string problem = ModRules.Compatibility(manifest);
            if (problem.Length > 0) throw new InvalidDataException(problem);
            if (manifest.kind != "crosshair" || manifest.scope != "ClientOnly")
                throw new InvalidDataException("Web imports only client-only crosshair data, never DLL code");
            if (!string.IsNullOrEmpty(manifest.assembly) || !string.IsNullOrEmpty(manifest.entryType) ||
                (manifest.dependencies != null && manifest.dependencies.Length != 0) ||
                (manifest.conflicts != null && manifest.conflicts.Length != 0))
                throw new InvalidDataException("This preset requires package behavior. Use its desktop package instead");
            return manifest;
        }
    }
}
