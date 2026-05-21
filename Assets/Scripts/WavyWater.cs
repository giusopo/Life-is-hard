using UnityEngine;

[ExecuteAlways]
public class WavyWater : MonoBehaviour
{
    [Header("Wave Settings")]
    public float waveHeight = 0.04f; 
    public float waveSpeed = 2f;
    public float waveFrequency = 8f;
    
    [Header("Flow Settings")]
    public float scrollX = 0.5f; 
    public float scrollY = 0f; 
    public Vector3 flowDirection = Vector3.zero;
    public bool isRadialFlow = false; 

    [Header("Connection Settings")]
public float topBulge = 1.3f;
    public float bottomBulge = 1.6f;

    private MeshFilter meshFilter;
    private Mesh instanceMesh;
    private Vector3[] baseVertices;
    private Vector2[] baseUVs;
    private Renderer rend;
    private MaterialPropertyBlock propBlock;

    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

    void OnEnable()
    {
        Initialize();
    }

    void Initialize()
    {
        meshFilter = GetComponent<MeshFilter>();
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Mesh currentMesh = meshFilter.sharedMesh;
            // Use sharedMesh properties to create a unique instance
            instanceMesh = Instantiate(currentMesh);
            instanceMesh.name = "WavyWaterInstance_" + gameObject.name;
            meshFilter.mesh = instanceMesh;
            
            baseVertices = instanceMesh.vertices;
            baseUVs = instanceMesh.uv;
        }
    }

    void Update()
    {
        if (instanceMesh == null || baseVertices == null || baseUVs == null)
        {
            Initialize();
            if (instanceMesh == null) return;
        }

        float time = (Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup);
        
        // 1. Vertex Displacement & UV Animation
        Vector3[] vertices = new Vector3[baseVertices.Length];
        Vector2[] currentUVs = new Vector2[baseUVs.Length];
        float waveTime = time * waveSpeed;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            Vector2 uv = baseUVs[i];

            if (flowDirection == Vector3.zero)
            {
                // Basin mode: Circular waves
                float dist = new Vector2(v.x, v.z).magnitude;
                // Formula sin(time - dist) moves waves OUTWARD
                v.y = Mathf.Sin(waveTime - dist * waveFrequency) * waveHeight;
                
                if (isRadialFlow)
                {
                    // Radial UV scroll: Move UVs INWARD to make texture appear to move OUTWARD
                    Vector2 dir = new Vector2(v.x, v.z).normalized;
                    uv -= dir * (time * scrollX);
                }
                else
                {
                    uv += new Vector2(time * scrollX, time * scrollY);
                }
            }
            else
            {
                // Stream mode: Vertical columns
                float posAlongFlow = Vector3.Dot(v, flowDirection.normalized);
                float h = v.y; 
                float scale = 1.0f;
                float edgeThreshold = 0.35f;
                if (h > edgeThreshold) 
                {
                    float t = (h - edgeThreshold) / (0.5f - edgeThreshold);
                    scale = Mathf.Lerp(1.0f, topBulge, Mathf.Clamp01(t));
                }
                else if (h < -edgeThreshold)
                {
                    float t = (-h - edgeThreshold) / (0.5f - edgeThreshold);
                    scale = Mathf.Lerp(1.0f, bottomBulge, Mathf.Clamp01(t));
                }
                
                v.x *= scale;
                v.z *= scale;

                // Waves move DOWN along flow
                float wave = Mathf.Sin(waveTime - posAlongFlow * waveFrequency) * waveHeight;
                v += flowDirection.normalized * wave;
                
                uv += new Vector2(0, time * scrollY);
            }
            vertices[i] = v;
            currentUVs[i] = uv;
        }
        
        instanceMesh.vertices = vertices;
        instanceMesh.uv = currentUVs;
        instanceMesh.RecalculateNormals();

        if (rend != null)
        {
            rend.GetPropertyBlock(propBlock);
            Vector4 st = rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseMapST) ? rend.sharedMaterial.GetVector(BaseMapST) : new Vector4(1,1,0,0);
            
            if (isRadialFlow)
            {
                // Reset global offset as we do it per-vertex
                st.z = 0; st.w = 0; 
            }
            else
            {
                // Apply global offset (useful for Polar UV meshes or linear flow)
                st.z = time * scrollX;
                st.w = time * scrollY;
            }

            propBlock.SetVector(BaseMapST, st);
            propBlock.SetVector(MainTexST, st);
            rend.SetPropertyBlock(propBlock);
        }

        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
        #endif
    }
}









