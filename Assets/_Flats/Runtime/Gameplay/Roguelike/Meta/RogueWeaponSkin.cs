using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>染色只複製材質；Clear 還原完全相同的 sharedMaterials，不改原始資產。</summary>
public sealed class RogueWeaponSkin : MonoBehaviour
{
    readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
    readonly List<Material> owned = new List<Material>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    readonly Dictionary<Renderer, bool> replaced = new Dictionary<Renderer, bool>();
    GameObject accessory;
    public static void Apply(Transform gunModel, RangedWeaponDef def)
    {
        if (gunModel == null) return;
        Clear(gunModel); if (def == null) return;
        var skin = gunModel.GetComponent<RogueWeaponSkin>();
        if (skin == null) skin = gunModel.gameObject.AddComponent<RogueWeaponSkin>();
        if (def.Tint >= 0)
        {
            Color tint = new Color(((def.Tint >> 16) & 255)/255f, ((def.Tint >> 8) & 255)/255f, (def.Tint & 255)/255f);
            foreach (var r in gunModel.GetComponentsInChildren<Renderer>(true))
            {
                // 開鏡鏡片與瞄具維持原色。
                if (r.transform.name.Contains("Sight") || r.transform.name.Contains("Scope") || (gunModel.childCount > 2 && r.transform.IsChildOf(gunModel.GetChild(2)))) continue;
                var mats = r.sharedMaterials; skin.originals[r] = mats;
                var copies = new Material[mats.Length];
                for (int i=0;i<mats.Length;i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]);
                    if (m.shader.name == "Texture Only")
                    {
                        var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){name="RogueTint",filterMode=FilterMode.Point};
                        texture.SetPixel(0,0,tint);texture.Apply();skin.textures.Add(texture);m.mainTexture=texture;
                    }
                    else { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",tint); if (m.HasProperty("_Color")) m.color=tint; }
                    copies[i]=m; skin.owned.Add(m);
                }
                r.sharedMaterials=copies;
            }
        }
        if (string.IsNullOrEmpty(def.Visual)) return;
        var prefab = Resources.Load<GameObject>(def.Visual); if (prefab == null) return;
        Bounds body = new Bounds(Vector3.zero,Vector3.zero); bool first = true;
        foreach (var mesh in gunModel.GetComponentsInChildren<MeshFilter>(true))
        {
            var ancestor=mesh.transform;while(ancestor.parent!=null&&ancestor.parent!=gunModel)ancestor=ancestor.parent;
            if(ancestor.name=="RogueAccessory")continue;
            if (mesh.sharedMesh == null || (gunModel.childCount > 2 && mesh.transform.IsChildOf(gunModel.GetChild(2)))) continue;
            var bounds = mesh.sharedMesh.bounds;
            for (int corner=0;corner<8;corner++)
            {
                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                point=gunModel.InverseTransformPoint(mesh.transform.TransformPoint(point));
                if(first){body=new Bounds(point,Vector3.zero);first=false;}else body.Encapsulate(point);
            }
        }
        skin.accessory = Instantiate(prefab,gunModel,false); skin.accessory.name="RogueAccessory";
        var fire = gunModel.Find("FirePosition");
        if (fire == null && gunModel.childCount > 1) fire = gunModel.GetChild(1);
        // LMG 沿 -X 朝前、+Y 朝上；其餘原版槍沿 +Y 朝前、+Z 朝上。
        Vector3 forward=def.BaseModel==14?Vector3.left:Vector3.up;
        Vector3 up=def.BaseModel==14?Vector3.up:Vector3.forward;
        bool muzzle = def.Visual.Contains("Barrel") || def.Visual.Contains("Brake") || def.Visual.Contains("Breacher") || def.Visual.Contains("Viper");
        float span=Mathf.Max(.3f,def.BaseModel==14?body.size.x:body.size.y), scale=span/3f;
        skin.accessory.transform.localScale=Vector3.one*scale;
        skin.accessory.transform.localRotation=Quaternion.LookRotation(forward,up);
        Vector3 anchor=body.center;
        if(fire!=null) anchor+=up*(Vector3.Dot(fire.localPosition-anchor,up)-.18f*scale);
        if(def.Class==WeaponClass.Handgun&&def.Visual.Contains("Mag"))anchor-=forward*span*.22f;
        if(muzzle&&fire!=null) anchor=fire.localPosition;
        if(!muzzle&&gunModel.childCount>2)
            anchor-=up*Mathf.Max(0,Vector3.Dot(anchor-gunModel.GetChild(2).localPosition,up)+.16f*scale);
        skin.accessory.transform.localPosition=anchor;
        // 保存的替換輪廓只處理指定基底；不動 Gun、開火位置、IK 或開鏡錨點。
        bool replacement=false;
        foreach(Transform child in skin.accessory.transform)
            if(child.name.StartsWith("BaseModel")) { bool match=child.name=="BaseModel"+def.BaseModel;child.gameObject.SetActive(match);replacement|=match; }
        if(replacement)
        {
            var renderer=gunModel.GetComponent<Renderer>();
            if(renderer!=null){skin.replaced[renderer]=renderer.enabled;renderer.enabled=false;}
        }
        foreach (var t in skin.accessory.GetComponentsInChildren<Transform>()) t.gameObject.layer=gunModel.gameObject.layer;
    }
    // Gun 在啟用／停用時切換本機槍的圖層（13 只給槍相機）；配件跟著切，否則瞄具相機與世界相機會看到它。
    void LateUpdate()
    {
        if (accessory == null || accessory.layer == gameObject.layer) return;
        foreach (var t in accessory.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
    }
    public static void Clear(Transform gunModel)
    {
        if (gunModel == null) return; var s=gunModel.GetComponent<RogueWeaponSkin>(); if(s!=null)s.Restore();
    }
    void Restore()
    {
        foreach(var p in replaced) if(p.Key!=null)p.Key.enabled=p.Value;
        replaced.Clear();
        foreach(var p in originals) if(p.Key!=null)p.Key.sharedMaterials=p.Value;
        originals.Clear();
        if(accessory!=null) { accessory.SetActive(false); Release(accessory); } accessory=null;
        foreach(var m in owned) Release(m); owned.Clear();
        foreach(var t in textures) Release(t); textures.Clear();
    }
    static void Release(Object o) { if(Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
    void OnDestroy(){Restore();}
}
