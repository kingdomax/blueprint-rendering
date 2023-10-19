using UnityEngine;
using UnityEngine.Rendering;

public class BluePrintRenderer : MonoBehaviour 
{
    public  GameObject      objectToRender;
    public  int             numberOfLayers; // Can use depth masking: provide a termination condition to dynamically adapt the number of rendering pass
    [Range(0f,1f)]
    public  float           objectOpacity = 0.5f;

    private CommandBuffer   cbPeeling;
    private CommandBuffer   cbEdgeMapping;
    private RenderTexture[] peelingIntermidate;
    private RenderTexture   edgeMap;
    private RenderTexture   forwardPass;
    private Camera          mainCam;
    private Material        depthPeelingMat;
    private Material        edgeConstructionMat;
    private Material        combineOnScreen;
    private Mesh            meshToDraw;
    private int             captureSet;

	void Start () 
    {
        if(!SetupCameraAndObject()) { return; }

        depthPeelingMat = new Material(Shader.Find("Unlit/DepthPeeler"));
        edgeConstructionMat = new Material(Shader.Find("Unlit/EdgeMapping"));
        combineOnScreen = new Material(Shader.Find("Unlit/CombineOnScreen"));

        meshToDraw = objectToRender.GetComponent<MeshFilter>().sharedMesh;

        ExecuteBlueprintRendering();
    }

    private void ExecuteBlueprintRendering()
    {
        // 0) Setup 6 textures (normal and depth in the same as article say)
        peelingIntermidate = new RenderTexture[numberOfLayers];
        for (int i = 0; i < peelingIntermidate.Length; i++)
        {
            // Article: Your render textures can be any size you want.
            //          You can render a sprite only as big as the bounding volume of the mesh.
            //          Though compositing it back on a screen space texture is a bit more work, **
            //          so I am going to render the entire screen for each peeling pass.
            //          Also I will render it at the screen resolution, since I don’t want to deal with the minification and magnification aliasing issues
            peelingIntermidate[i] = new RenderTexture(mainCam.pixelWidth, mainCam.pixelHeight, 16, RenderTextureFormat.ARGBFloat)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                useMipMap = false,
            };
        }

        // 1) DEPTH PEELING
        cbPeeling = new CommandBuffer { name = "DepthPeeling" };
        for (int i = 0; i < numberOfLayers; i++)
        {
            // set "_PreviusLayer" use in DepthPeeler.shader    
            cbPeeling.SetGlobalTexture("_PreviusLayer", i == 0 ? Texture2D.blackTexture : peelingIntermidate[i - 1]);

            // Render the depth value of the fragment and its normal to a render texture, the depth texture is used to peel away the mesh in the next pass
            cbPeeling.SetRenderTarget(peelingIntermidate[i]);
            cbPeeling.ClearRenderTarget(true, true, Color.white);
            cbPeeling.DrawMesh(meshToDraw, objectToRender.transform.localToWorldMatrix, depthPeelingMat, 0, 0); // It is not iterate to deeper layer,
                                                                                                                // but it use discard() machanism in shader file
        }
        // cbPeeling.Blit(peelingIntermidate[5], BuiltinRenderTextureType.CameraTarget);
        mainCam.AddCommandBuffer(CameraEvent.AfterSkybox, cbPeeling);

        //2) EXTRACT EDGES FROM EACH LAYER BUFFER
        edgeMap = new RenderTexture(peelingIntermidate[0]);
        forwardPass = new RenderTexture(peelingIntermidate[0]); // use in composing step, but want to initiate 1st layer of mesh
        cbEdgeMapping = new CommandBuffer() { name = "EdgeMapping" };
        for (int i = 0; i < numberOfLayers; i++)
        {
            cbEdgeMapping.SetRenderTarget(edgeMap);
            cbEdgeMapping.ClearRenderTarget(true, true, Color.black);

            // Blit -  Blending/Combine from one render texture into another, potentially using a custom shader
            //         Note that Blit changes the currently active render target.
            //         After Blit executes, dest becomes the active render target.        
            cbEdgeMapping.Blit(peelingIntermidate[i], edgeMap, edgeConstructionMat, 0);
            cbEdgeMapping.Blit(edgeMap, peelingIntermidate[i]); // store edge map back to peelingIntermidate[]
        }
        for (int i = 0; i < numberOfLayers; i++) { cbEdgeMapping.Blit(peelingIntermidate[i], edgeMap, edgeConstructionMat, 1); } // second pass in EdgeMapping.shader

        // 3) COMPOSING BLUEPRINT
        cbEdgeMapping.Blit(BuiltinRenderTextureType.CameraTarget, forwardPass); // blend what camera see with 1st layer of normal map
        cbEdgeMapping.SetGlobalTexture("_ForwadPass", forwardPass); // set as global variable in CombineOnScreen.shader
        cbEdgeMapping.Blit(edgeMap, BuiltinRenderTextureType.CameraTarget, combineOnScreen); // pass edgemap as main parameter into shader file,
                                                                                             // and combine with forwardPass texture
        mainCam.AddCommandBuffer(CameraEvent.AfterSkybox, cbEdgeMapping); // execute command edgemapping right after depth peeling 
    }

    void Update () 
    {
        Shader.SetGlobalFloat("_ForwardPassContribution", objectOpacity);

        //mainCam.RemoveAllCommandBuffers();
        //cbPeeling?.Clear();
        //cbEdgeMapping?.Clear();

        ScreenshotTextures();
    }

    private void OnPreRender()
    {
        //ExecuteBlueprintRendering();
    }

    private bool SetupCameraAndObject()
    {
        if (objectToRender == null)
        {
            Debug.LogError("No object set for blueprint effect on: " + this.gameObject.name);
            this.enabled = false;
            return false;
        }

        mainCam = Camera.main;
        if (mainCam == null) mainCam = GameObject.FindObjectOfType<Camera>();
        if (mainCam == null)
        {
            Debug.LogError("No Camera found in the scene ");
            return false;
        }

        return true;
    }

    private void ScreenshotTextures()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            captureSet++;
            int i = 0;
            foreach (RenderTexture t in peelingIntermidate)
            {
                SaveRenderTexture.Save(t, SaveRenderTexture.OutPutType.JPEG, objectToRender.name + "Set" + captureSet + "Layer" + i);
                i++;
            }

            SaveRenderTexture.Save(edgeMap, SaveRenderTexture.OutPutType.JPEG, objectToRender.name + "Set" + captureSet + "FinalEdge");
        }
    }
}
