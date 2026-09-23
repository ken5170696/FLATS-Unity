using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// An isolated rendering fixture: the grid must remain visible through each optic.
public static class FlatsScopeVisualCheck
{
    public static void Capture()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var folder = Path.GetFullPath(Environment.GetEnvironmentVariable("FLATS_SCOPE_EVIDENCE") ?? "Builds/ScopeVerification");
        Directory.CreateDirectory(folder);
        for (int x = -5; x <= 5; x++) for (int y = -5; y <= 5; y++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.position = new Vector3(x * .25f, y * .25f, 30);
            tile.transform.localScale = new Vector3(.25f, .25f, .1f);
            var material = new Material(Shader.Find("Unlit/Color"));
            material.color = y == 5 ? Color.red : x == 5 ? Color.green : (x + y) % 2 == 0 ? Color.white : Color.black;
            tile.GetComponent<Renderer>().sharedMaterial = material;
        }
        var world = new GameObject("Fixture main").AddComponent<Camera>();
        world.fieldOfView = 60;
        world.clearFlags = CameraClearFlags.SolidColor;
        world.backgroundColor = Color.blue;
        world.cullingMask = ~(1 << 13);
        world.nearClipPlane = .01f;
        var output = new RenderTexture(1080, 675, 24, RenderTextureFormat.ARGB32);
        output.Create(); world.targetTexture = output;
        var gun = new GameObject("Fixture gun").AddComponent<Camera>();
        gun.CopyFrom(world); gun.cullingMask = 1 << 13; gun.clearFlags = CameraClearFlags.Depth;
        foreach (var name in new[] { "reflex sight", "2x sight", "4x sight", "6x sight", "8x sight" })
        {
            var sight = FlatsSightTarget.Create("sights/" + name);
            sight.transform.position = Vector3.zero;
            sight.transform.rotation = Quaternion.Euler(-90, 0, 0);
            foreach (var renderer in sight.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 13;
            foreach (var canvas in sight.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(true);
            var camera = sight.GetComponentInChildren<Camera>(true);
            camera.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            foreach(var raw in sight.GetComponentsInChildren<RawImage>(true))
                if(raw.uvRect != new Rect(0,0,1,1)) throw new Exception(name + " has collapsed or cropped texture coordinates: " + raw.uvRect);
            Save(camera.targetTexture, Path.Combine(folder, name + "-texture.png"));
            world.Render(); gun.Render();
            Save(output, Path.Combine(folder, name + "-display.png"));
            AssertGrid(output, name);
            Debug.Log("SCOPE_GRID " + name + " camera=" + camera.transform.forward + " fov=" + camera.fieldOfView);
            UnityEngine.Object.DestroyImmediate(sight);
        }
        world.targetTexture = null; gun.targetTexture = null; output.Release();
        UnityEngine.Object.DestroyImmediate(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    static void AssertGrid(RenderTexture texture, string name)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(120, 120, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(texture.width/2-60, texture.height/2-60, 120, 120), 0, 0); pixels.Apply();
        int dark=0, light=0;
        foreach(var color in pixels.GetPixels32())
        {
            if(color.r<40 && color.g<40 && color.b<40) dark++;
            if(color.r>210 && color.g>210 && color.b>210) light++;
        }
        UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.active = previous;
        if(dark<20 || light<20) throw new Exception(name+" did not display the grid: dark="+dark+", light="+light);
        Debug.Log("SCOPE_GRID_PASS "+name+" dark="+dark+" light="+light);
    }

    static void Save(RenderTexture texture, string path)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(pixels); RenderTexture.active = previous;
    }
}
