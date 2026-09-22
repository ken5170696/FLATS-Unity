using System;

namespace Flats.Modules
{
    // One admission policy for initial join and later room-property changes.
    // Transport callbacks are invoked only on rejection; this never downloads files.
    public sealed class RoomModuleCoordinator
    {
        public bool AdmitLocal(object rule,object requirements,bool ready,Func<string> agreement,Action<string> reject)
        {
            if(rule is int r && r==-1)return true;
            string error=!ready?"Local modules are still loading.":Compare(requirements,agreement());
            if(error.Length==0)return true;
            reject(error);return false;
        }
        public bool AdmitPeer(object rule,object requirements,object peerRequirements,Action reject)
        {
            if(rule is int r && r==-1)return true;
            if(Compare(requirements,peerRequirements).Length==0)return true;
            reject();return false;
        }
        static string Compare(object room,object peer)
        {
            if((room!=null && !(room is string)) || (peer!=null && !(peer is string)))
                return "Room module requirements are invalid or use an unsupported protocol.";
            return SessionModules.Compare(room as string,peer as string);
        }
    }
}
