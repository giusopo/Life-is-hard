using UnityEngine;

[ExecuteAlways]
public class WaterScroller : MonoBehaviour
{
    public float scrollX = 0.1f;
    public float scrollY = 0.1f;
    private Renderer rend;
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (rend == null || rend.sharedMaterial == null) return;
        
        float offsetX = Time.time * scrollX;
        float offsetY = Time.time * scrollY;
        Vector2 offset = new Vector2(offsetX, offsetY);

        if (rend.sharedMaterial.HasProperty(BaseMap))
            rend.sharedMaterial.SetTextureOffset(BaseMap, offset);
        else if (rend.sharedMaterial.HasProperty(MainTex))
            rend.sharedMaterial.SetTextureOffset(MainTex, offset);
    }
}
