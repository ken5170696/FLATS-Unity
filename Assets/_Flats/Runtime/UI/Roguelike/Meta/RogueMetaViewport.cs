using UnityEngine;
using UnityEngine.UI;

public class RogueMetaViewport : MonoBehaviour
{
    public CanvasScaler scaler;
    public Vector2 landscape = new Vector2(1920,1080), portrait = new Vector2(720,1560);
    void OnEnable() { Apply(); }
    void Update() { Apply(); }
    void Apply() { bool tall=Screen.height>Screen.width; scaler.referenceResolution=tall?portrait:landscape; scaler.matchWidthOrHeight=tall?0:1; }
}
