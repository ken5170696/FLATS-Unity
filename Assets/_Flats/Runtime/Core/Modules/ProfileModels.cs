using System;

namespace Flats.Modules
{
    [Serializable] public sealed class ProfileModule { public string id,version,json;public bool requested; }
    [Serializable] public sealed class ModProfile { public string id,name;public ProfileModule[] modules=new ProfileModule[0]; }
    [Serializable] public sealed class ProfileDocument { public int schema=1;public string selected;public ModProfile[] profiles; }
}
