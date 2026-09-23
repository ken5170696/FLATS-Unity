using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Flats.Modules
{
    // Immutable version directories + an atomic pointer. Running assemblies keep their old path.
    public sealed partial class PackageStore
    {
        readonly object gate = new object();
        readonly IModJson json;
        internal IModJson Codec { get { return json; } }
        readonly HashSet<string> operations = new HashSet<string>(StringComparer.Ordinal);
        sealed class Reservation : IDisposable
        {
            readonly Action release;
            public Reservation(Action action) { release=action; }
            public void Dispose() { release(); }
        }
        public IDisposable Reserve(string id)
        {
            lock(gate)
            {
                ModRules.Id(id);
                if(!operations.Add(id))throw new InvalidOperationException("This module already has an operation in progress");
                return new Reservation(()=>{lock(gate)operations.Remove(id);});
            }
        }
        public string Root { get; private set; }
        public readonly List<string> Notices = new List<string>();
        public PackageStore(string root, IModJson codec)
        {
            Root = Path.GetFullPath(root); json = codec;
            Directory.CreateDirectory(Root); RejectLinks(Root);
        }
        public static void RejectLinks(string path)
        {
            for (var p = new DirectoryInfo(path); p != null; p = p.Parent)
                if (p.Exists && (p.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked module directories are not supported");
        }
        string Under(string relative)
        {
            ModRules.RelativePath(relative);
            string path = Path.GetFullPath(Path.Combine(Root, relative));
            if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escaped module storage");
            RejectLinks(Path.GetDirectoryName(path));
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked module files are not supported");
            return path;
        }
        public void Recover()
        {
            lock (gate)
            {
                RecoverBatch();
                var staging = Under("staging");
                if (Directory.Exists(staging))
                    foreach (var path in Directory.GetDirectories(staging))
                    {
                        // Only our own random transaction names, never user-supplied targets.
                        if (!Guid.TryParseExact(Path.GetFileName(path), "N", out _)) continue;
                        try { bool removed=Directory.Exists(Path.Combine(path,"removed"));DeleteTree(path);if(!removed)Notices.Add("An interrupted download was cleaned up. Retry it from Explore."); } catch (Exception e) { Notices.Add("Could not clean interrupted download: " + e.Message); }
                    }
            }
        }
        static void DeleteTree(string path)
        {
            RejectLinks(path);
            foreach (var file in Directory.GetFiles(path))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Refusing linked file cleanup");
                File.Delete(file);
            }
            foreach (var sub in Directory.GetDirectories(path)) DeleteTree(sub);
            Directory.Delete(path);
        }
        public string BeginStaging()
        {
            lock (gate)
            {
                var path = Under("staging/" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path); return path;
            }
        }
        public void EndStaging(string path)
        {
            lock (gate)
            {
                var full = Path.GetFullPath(path);
                if (Path.GetDirectoryName(full) != Under("staging") || !Guid.TryParseExact(Path.GetFileName(full), "N", out _)) throw new IOException("Invalid staging directory");
                if (Directory.Exists(full)) DeleteTree(full);
            }
        }
        InstalledPackage ReadReceipt(string file, string id)
        {
            if (new FileInfo(file).Length > 128 * 1024) throw new InvalidDataException("Install receipt too large");
            var p = json.Read<InstalledPackage>(File.ReadAllText(file));
            if(p!=null && p.schema>1)throw new NotSupportedException("Newer install receipt; changes blocked");
            if (p == null || p.schema != 1 || p.manifest == null) throw new InvalidDataException("Unsupported install receipt");
            p.manifest.Validate(); ModRules.Hash(p.sha256);
            if (p.manifest.id != id || p.directory != id + "/" + p.manifest.version + "-" + p.sha256) throw new InvalidDataException("Receipt identity does not match directory");
            var content = Under(p.directory + "/manifest.json");
            if (!File.Exists(content) || new FileInfo(content).Length > 96 * 1024) throw new InvalidDataException("Installed manifest missing or too large");
            var manifest = json.Read<PackageManifest>(File.ReadAllText(content)); manifest.Validate();
            if (json.Write(manifest) != json.Write(p.manifest)) throw new InvalidDataException("Installed manifest differs from receipt");
            return p;
        }
        public InstalledPackage[] Scan()
        {
            lock (gate)
            {
                if(recoveryRequired)throw new IOException("An interrupted install requires recovery before modules can be loaded");
                var result = new List<InstalledPackage>();
                foreach (var dir in Directory.GetDirectories(Root).OrderBy(x => x, StringComparer.Ordinal))
                {
                    var id = Path.GetFileName(dir);
                    if (id == "staging") continue;
                    try
                    {
                        ModRules.Id(id); RejectLinks(dir);
                        var current = Under(id + "/current.json");
                        if (!File.Exists(current) && !File.Exists(current + ".previous")) continue;
                        InstalledPackage p;
                        try { p = ReadReceipt(current, id); }
                        catch (NotSupportedException) { throw; }
                        catch (Exception)
                        {
                            p = ReadReceipt(current + ".previous", id);
                            // Preserve damaged state before restoring the last complete pointer.
                            if (File.Exists(current)) File.Move(current, current + ".retained-" + Guid.NewGuid().ToString("N"));
                            File.Copy(current + ".previous", current);
                            Notices.Add("Recovered previous installation: " + id);
                        }
                        result.Add(p);
                    }
                    catch (Exception e) { Notices.Add(id + ": " + e.Message + ". Files retained; repair or reinstall this module."); }
                }
                return result.ToArray();
            }
        }
        void WriteReceipt(InstalledPackage package)
        {
            string path = Under(package.manifest.id + "/current.json"), temporary = path + ".new";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = System.Text.Encoding.UTF8.GetBytes(json.Write(package));
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            // Unsupported atomic replace must fail, leaving the old pointer intact.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous");
            else File.Move(temporary, path);
        }
        public void SetRequested(string id, bool requested)
        {
            lock (gate)
            {
                if(operations.Contains(id))throw new InvalidOperationException("Wait for this module's download to finish");
                var p = Scan().Single(x => x.manifest.id == id);
                p.requested = requested; WriteReceipt(p);
            }
        }
        public void Remove(string id)
        {
            lock (gate)
            {
                if(operations.Contains(id))throw new InvalidOperationException("Cancel this module's download before removing it");
                ModRules.Id(id);
                var path = Under(id);
                if (!Directory.Exists(path)) return;
                // Move the entire installed record into a tombstone transaction, atomically.
                // Physical removal waits until next launch, so a running DLL is never overwritten.
                var tomb = BeginStaging();
                Directory.Move(path, Path.Combine(tomb, "removed"));
            }
        }
        public string ContentPath(InstalledPackage p) { return Under(p.directory); }
        public InstalledPackage Install(string archive, string staging, CatalogItem expected, string source, CancellationToken cancel)
        {
            expected.manifest.Validate(); ModRules.Hash(expected.sha256);
            if (new FileInfo(archive).Length != expected.bytes) throw new InvalidDataException("Download size does not match source");
            using (var file = File.OpenRead(archive)) using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant() != expected.sha256)
                    throw new InvalidDataException("SHA-256 mismatch. Download rejected; previous version retained.");
            cancel.ThrowIfCancellationRequested();
            var content = Path.Combine(staging, "content"); Directory.CreateDirectory(content);
            using (var zip = ZipFile.OpenRead(archive))
            {
                if (zip.Entries.Count > 2048) throw new InvalidDataException("Package contains too many entries");
                long expanded = 0;
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zip.Entries)
                {
                    cancel.ThrowIfCancellationRequested();
                    var name = entry.FullName.TrimEnd('/'); ModRules.RelativePath(name);
                    if (!names.Add(name)) throw new InvalidDataException("Duplicate archive path");
                    int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                    if ((unixType != 0 && unixType != 0x8000 && unixType != 0x4000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Links and special archive entries are not allowed");
                    expanded = checked(expanded + entry.Length);
                    if (expanded > ModRules.MaxExpanded || entry.Length > ModRules.MaxArchive || (entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 200))
                        throw new InvalidDataException("Archive expansion limit exceeded");
                    var dest = Path.GetFullPath(Path.Combine(content, name));
                    if (!dest.StartsWith(content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escaped staging");
                    if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var input = entry.Open()) using (var output = new FileStream(dest, FileMode.CreateNew))
                    {
                        byte[] buffer = new byte[65536]; long count = 0; int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancel.ThrowIfCancellationRequested(); count += read;
                            if (count > entry.Length) throw new InvalidDataException("Archive entry exceeds declared size");
                            output.Write(buffer, 0, read);
                        }
                        if (count != entry.Length) throw new InvalidDataException("Truncated archive entry");
                    }
                }
            }
            string manifestPath = Path.Combine(content, "manifest.json");
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 96 * 1024) throw new InvalidDataException("Root manifest.json missing or too large");
            var manifest = json.Read<PackageManifest>(File.ReadAllText(manifestPath));
            if (manifest == null) throw new InvalidDataException("Empty manifest");
            manifest.Validate();
            if (json.Write(manifest) != json.Write(expected.manifest)) throw new InvalidDataException("Archive manifest differs from catalogue metadata");
            string incompatible = ModRules.Compatibility(manifest);
            if (incompatible.Length > 0) throw new InvalidDataException(incompatible);
            if (manifest.kind == "managed" && !File.Exists(Path.Combine(content, manifest.assembly))) throw new InvalidDataException("Entry assembly is missing");
            lock (gate)
            {
                cancel.ThrowIfCancellationRequested(); // Commit is the cancellation boundary.
                var old = Scan().FirstOrDefault(x => x.manifest.id == manifest.id);
                if(old==null && File.Exists(Under(manifest.id+"/current.json")))throw new InvalidDataException("Existing install receipt is unreadable or newer. Preserve it and repair storage before reinstalling.");
                if (old != null && ModRules.Version(old.manifest.version) > ModRules.Version(manifest.version)) throw new InvalidDataException("Downgrade refused");
                if (old != null && old.manifest.version == manifest.version && old.sha256 != expected.sha256) throw new InvalidDataException("Same version has a different digest; publish a new version");
                var p = new InstalledPackage { schema = 1, manifest = manifest, sha256 = expected.sha256, source = source,
                    directory = manifest.id + "/" + manifest.version + "-" + expected.sha256, requested = old != null && old.requested, imageUrl=expected.imageUrl };
                var destination = Under(p.directory);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (Directory.Exists(destination))
                {
                    RejectLinks(destination);
                    if(Directory.GetFiles(content,"*",SearchOption.AllDirectories).Length!=Directory.GetFiles(destination,"*",SearchOption.AllDirectories).Length)
                        throw new IOException("Retained version contains unexpected files; repair module storage");
                    // An interrupted commit may leave this immutable directory. Verify it before reuse.
                    foreach (var file in Directory.GetFiles(content, "*", SearchOption.AllDirectories))
                    {
                        var other = Path.Combine(destination, file.Substring(content.Length + 1));
                        using (var sha = SHA256.Create())
                        {
                            if (!File.Exists(other)) throw new IOException("Incomplete retained version; repair module storage");
                            using (var a = File.OpenRead(file)) using (var b = File.OpenRead(other))
                                if (!sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b))) throw new IOException("Retained version differs; repair module storage");
                        }
                    }
                }
                else Directory.Move(content, destination);
                WriteReceipt(p); return p;
            }
        }
    }
}
