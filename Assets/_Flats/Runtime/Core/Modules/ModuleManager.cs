using System;
using System.Collections.Generic;
using System.Linq;

namespace Flats.Modules
{
    public enum ModuleScope { ClientOnly, RequiredForSession }
    public enum ModuleState { Installed, Enabled, Unavailable, Error }

    // Inclusive minimum, exclusive maximum. Versions are numeric System.Version values.
    public sealed class VersionRange
    {
        public readonly Version Minimum, Maximum;
        public VersionRange(string minimum, string maximum) { Minimum = Version.Parse(minimum); Maximum = Version.Parse(maximum); }
        public bool Contains(Version version) { return version >= Minimum && version < Maximum; }
        public override string ToString() { return ">=" + Minimum + " <" + Maximum; }
    }
    public sealed class ModuleDependency
    {
        public readonly string Id;
        public readonly VersionRange Versions;
        public ModuleDependency(string id, VersionRange versions) { Id = id; Versions = versions; }
    }
    public sealed class ModuleManifest
    {
        public readonly string Id, DisplayName, Description;
        public readonly Version Version;
        public readonly ModuleScope Scope;
        public readonly VersionRange ApiVersions, GameVersions;
        public readonly IReadOnlyList<ModuleDependency> Dependencies;
        public readonly IReadOnlyList<string> Conflicts;
        public ModuleManifest(string id, string version, string name, string description, ModuleScope scope,
            VersionRange api, VersionRange game, ModuleDependency[] dependencies = null, string[] conflicts = null)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !(char.IsLetterOrDigit(c) || c == '.' || c == '-')))
                throw new ArgumentException("Invalid stable module ID");
            Id = id; Version = Version.Parse(version); DisplayName = name; Description = description;
            Scope = scope; ApiVersions = api; GameVersions = game;
            Dependencies = Array.AsReadOnly((ModuleDependency[])(dependencies ?? new ModuleDependency[0]).Clone());
            Conflicts = Array.AsReadOnly((string[])(conflicts ?? new string[0]).Clone());
        }
    }
    // Register cleanup BEFORE making a change. All cleanup runs, even when one action throws.
    public sealed class ModuleLifetime : IDisposable
    {
        readonly Stack<Action> cleanup = new Stack<Action>();
        bool disposed;
        public void Own(Action release) { if (disposed) throw new ObjectDisposedException("ModuleLifetime"); cleanup.Push(release); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            while (cleanup.Count > 0) try { cleanup.Pop()(); } catch (Exception e) { errors.Add(e); }
            if (errors.Count > 0) throw new AggregateException("Module cleanup failed", errors);
        }
    }
    public interface IFirstPartyModule
    {
        ModuleManifest Manifest { get; }
        void Initialize(ModuleLifetime lifetime);
        void Enable(ModuleLifetime lifetime);
        void Disable();
    }
    public sealed class ModuleRecord
    {
        internal readonly IFirstPartyModule Module;
        internal ModuleLifetime Lifetime;
        public ModuleManifest Manifest { get { return Module.Manifest; } }
        public bool Requested { get; internal set; }
        public bool Active { get { return Lifetime != null; } }
        public ModuleState State { get; internal set; }
        public string Reason { get; internal set; }
        internal ModuleRecord(IFirstPartyModule module) { Module = module; Reason = ""; }
    }
    public sealed class ModuleManager : IDisposable
    {
        readonly List<ModuleRecord> records;
        readonly List<ModuleRecord> activationOrder = new List<ModuleRecord>();
        readonly Version api, game;
        readonly bool sessionAgreement;
        public IReadOnlyList<ModuleRecord> Installed { get; private set; }
        public string LastError { get; private set; }
        public ModuleManager(IEnumerable<IFirstPartyModule> modules, string apiVersion, string gameVersion, bool supportsSessionAgreement = false)
        {
            sessionAgreement = supportsSessionAgreement;
            api = Version.Parse(apiVersion); game = Version.Parse(gameVersion);
            records = modules.Select(m => new ModuleRecord(m)).ToList(); Installed = records.AsReadOnly();
            Validate();
        }
        string Inspect(ModuleRecord r, HashSet<ModuleRecord> visiting)
        {
            var m = r.Manifest;
            if (records.Count(x => x.Manifest.Id == m.Id) != 1) return "Duplicate ID: " + m.Id;
            if (m.Scope != ModuleScope.ClientOnly && !sessionAgreement) return "RequiredForSession is not supported: room agreement is unavailable";
            if (!m.ApiVersions.Contains(api)) return "Incompatible API version: requires " + m.ApiVersions;
            if (!m.GameVersions.Contains(game)) return "Incompatible game version: requires " + m.GameVersions;
            if (!visiting.Add(r)) return "Cyclic dependency: " + m.Id;
            foreach (var d in m.Dependencies)
            {
                var matches = records.Where(x => x.Manifest.Id == d.Id).ToArray();
                if (matches.Length != 1) return "Missing or duplicate dependency: " + d.Id;
                if (!d.Versions.Contains(matches[0].Manifest.Version)) return "Incompatible dependency: " + d.Id + " " + d.Versions;
                string error = Inspect(matches[0], visiting);
                if (error != null) return error;
            }
            visiting.Remove(r); return null;
        }
        void Validate()
        {
            foreach (var r in records)
            {
                r.Reason = Inspect(r, new HashSet<ModuleRecord>()) ?? "";
                r.State = r.Reason.Length > 0 ? ModuleState.Unavailable : r.Active ? ModuleState.Enabled : ModuleState.Installed;
            }
        }
        void Order(ModuleRecord r, List<ModuleRecord> ordered)
        {
            if (ordered.Contains(r)) return;
            foreach (var d in r.Manifest.Dependencies) Order(records.Single(x => x.Manifest.Id == d.Id), ordered);
            ordered.Add(r);
        }
        public bool Apply(IEnumerable<string> requestedIds)
        {
            var requested = new HashSet<string>(requestedIds, StringComparer.Ordinal);
            foreach (var r in records) r.Requested = requested.Contains(r.Manifest.Id);
            Validate(); LastError = "";
            foreach (var r in records.Where(x => x.Requested))
            {
                string error = r.Reason;
                if (error.Length == 0)
                    foreach (var d in r.Manifest.Dependencies)
                        if (!requested.Contains(d.Id)) { error = "Enable dependency first: " + d.Id; break; }
                if (error.Length == 0)
                    foreach (var other in records.Where(x => x.Requested && x != r))
                        if (r.Manifest.Conflicts.Contains(other.Manifest.Id) || other.Manifest.Conflicts.Contains(r.Manifest.Id))
                        { error = "Conflict: " + other.Manifest.Id; break; }
                if (error.Length > 0) { r.Reason = error; if (!r.Active) r.State = ModuleState.Unavailable; LastError += r.Manifest.Id + ": " + error + "\n"; }
            }
            if (LastError.Length > 0) return false; // No mutation before the entire requested set is valid.
            var ordered = new List<ModuleRecord>();
            foreach (var r in records.Where(x => x.Requested)) Order(r, ordered);
            var added = new List<ModuleRecord>();
            foreach (var r in ordered.Where(x => !x.Active))
            {
                var lifetime = new ModuleLifetime();
                try
                {
                    r.Module.Initialize(lifetime); r.Module.Enable(lifetime);
                    r.Lifetime = lifetime; r.State = ModuleState.Enabled; added.Add(r);
                }
                catch (Exception e)
                {
                    var errors = new List<string> { e.Message };
                    try { r.Module.Disable(); } catch (Exception cleanup) { errors.Add(cleanup.Message); }
                    try { lifetime.Dispose(); } catch (Exception cleanup) { errors.Add(cleanup.Message); }
                    for (int i = added.Count - 1; i >= 0; i--) Stop(added[i], errors);
                    r.State = ModuleState.Error; r.Reason = string.Join("; ", errors); LastError = r.Manifest.Id + ": " + r.Reason;
                    return false;
                }
            }
            var stopErrors = new List<string>();
            for (int i = activationOrder.Count - 1; i >= 0; i--)
                if (!ordered.Contains(activationOrder[i])) Stop(activationOrder[i], stopErrors);
            activationOrder.Clear(); activationOrder.AddRange(ordered);
            LastError = string.Join("; ", stopErrors); return stopErrors.Count == 0;
        }
        static void Stop(ModuleRecord r, List<string> errors)
        {
            var lifetime = r.Lifetime; r.Lifetime = null;
            try { r.Module.Disable(); } catch (Exception e) { errors.Add(e.Message); }
            try { if (lifetime != null) lifetime.Dispose(); } catch (Exception e) { errors.Add(e.Message); }
            r.State = ModuleState.Installed;
        }
        public void Dispose()
        {
            var errors = new List<string>();
            for (int i = activationOrder.Count - 1; i >= 0; i--) Stop(activationOrder[i], errors);
            activationOrder.Clear(); LastError = string.Join("; ", errors);
        }
    }
}
