using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public sealed partial class ModCenterVerification
{
    async Task GlassUI()
    {
        DontDestroyOnLoad(gameObject);
        string path=Path.Combine(Application.dataPath,"_Flats/Art/Environments/Meshes/Mesh_9.asset");
        byte[] before=File.ReadAllBytes(path);
        await MainClick(0,"Play");await MainClick(0,"Singleplayer");
        for(int i=0;i<6 && !menu.buttons[5].transform.parent.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text=="Warehouse");i++)await MainClick(5,"Singleplayer");
        Check("Warehouse selected through game UI",menu.buttons[5].transform.parent.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text=="Warehouse"));
        await MainClick(0,"Playing");await Until(()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Warehouse");await Task.Delay(1600);
        var glass=FindFirstObjectByType<GlassController>();var baker=glass.GetComponentInChildren<MB3_MeshBaker>();
        var mesh=((DigitalOpus.MB.Core.MB3_MeshCombinerSingle)baker.meshCombiner).GetMesh();
        Check("owned runtime mesh",mesh.name=="FLATS runtime glass"&&(mesh.hideFlags&HideFlags.DontSave)!=0);
        Check("rebuilt UVs finite and colors retained",mesh.vertexCount==16&&mesh.colors.Length==16&&mesh.uv.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)));
        var source=glass.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="Glass").gameObject;
        glass.ChangeGlass(source,true);Check("break removes one quad",mesh.vertexCount==12);
        glass.ChangeGlass(source,false);Check("restore adds quad back",mesh.vertexCount==16);
        Check("source asset bytes unchanged",File.ReadAllBytes(path).SequenceEqual(before));
        await Capture("warehouse-glass-runtime");
    }
}
