using System;

namespace Flats.Modules
{
    public enum DownloadState { Queued, Downloading, Verifying, Installed, Cancelled, Failed, Paused }
    public sealed class DownloadSnapshot
    {
        public string Id, Name, Version, Error;
        public long Received, Total;
        public DownloadState State;
        public bool Busy { get { return State == DownloadState.Queued || State == DownloadState.Downloading || State == DownloadState.Verifying; } }
    }
}
