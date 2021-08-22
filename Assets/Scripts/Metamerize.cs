using UnityEngine;
using System.Collections.Generic;

public enum WhichFilters
{
    ALL,
    EVEN,
    ODD
}
public class Metamerize : MonoBehaviour
{

    private const int MAXIMUM_BUFFER_SIZE = 8192;

    [Header("Can be changed in real time")]
    public bool justBlur = false;
    public bool FullL0Approach;
    public bool combinedMatrix;

    public WhichFilters UseWhichFilters = WhichFilters.ALL;
    public bool isLinearColorspace;

    public float foveaX = 0.5f;
    public float foveaY = 0.5f;
    public float meanDepth = 3.0f;
    public float foveaSize = 0.1f;

    public bool usingHMDFoveation;
    private FoveationLUT foveationLUT;
    [Range(0.0f, 0.3f)]
    public float LUTAlpha = 0.1f;
    public int LUTDownsample = 1;
    public FoveationMode LUTMode = FoveationMode.QUADRATIC;

    [Header("Trigger full re-init")]
    public int pyramidDepth = 5;
    private int m_pyramidDepth;
    public int nbands = 2;
    private int m_nBands;
    public bool hiq = false;
    private bool m_hiq;
    public bool preFilter = false;
    private bool m_preFilter;

    private int m_size = 0;

    [Header("Trigger noise re-init")]
    public bool newNoise;
    private bool m_newNoise;
    public bool preLoadNoise;
    private bool m_preLoadNoise;

    //DebugVariables
    public enum DisplayMode
    {
        NORMAL,
        INPUT_YCBCR,
        SHOW_STEERABLE,
        SHOW_STATS
    }
    [Header("Debug Variables")]
    public DisplayMode displayMode;
    public int showStep;
    public int showBand;
    public int showMip;
    public int showDisplayMode;
    public bool showTransformRect;

    // Start is called before the first frame update


    //Texture synthesis

    Camera m_camera;
    RenderTexture[] m_noiseTile;



    List<RenderTexture>[] band_tex;

    //Analysis textures
    RenderTexture m_input;
    RenderTexture m_loOutput;
    RenderTexture[] m_band_means;
    RenderTexture[] m_band_sdevs;

    //List of textures of different mip levels that autogenerate mips
    List<RenderTexture> m_aux_genMips;
    List<RenderTexture> m_aux_genMipsb;

    //List of textures of different resolutions that dont do mips
    List<RenderTexture> m_aux_noMips;

    //One aux texture with manual mips
    RenderTexture m_aux_noGenmips;

    //One more aux texture for filtering H
    RenderTexture m_hfiltered;
    RenderTexture m_loPass;


    private void Awake()
    {
        foveationLUT = gameObject.AddComponent<FoveationLUT>();
        foveationLUT.Metamerize = this;
    }

    void OnEnable()
    {
        m_noiseTile = null;

        m_hfiltered = null;
        m_loPass = null;

        m_aux_genMips = null;
        m_aux_genMipsb = null;
        m_aux_noMips = null;
        m_aux_noGenmips = null;

        m_input = null;
        m_loOutput = null;
        band_tex = null;
        m_band_means = null;
        m_band_sdevs = null;



    }

    private void OnDisable()
    {
        print("Cleaning up...");
        //Analysis
        if (m_input != null) m_input.Release();
        if (m_loOutput != null) m_loOutput.Release();

        if (band_tex != null)
        {
            foreach (List<RenderTexture> t in band_tex)
            {
                foreach (RenderTexture tex in t)
                {
                    tex.Release();
                }
            }
            band_tex = null;
        }

        if (m_band_means != null)
        {
            foreach (RenderTexture r in m_band_means)
            {
                r.Release();
            }
            m_band_means = null;
        }

        if (m_band_sdevs != null)
        {
            foreach (RenderTexture r in m_band_sdevs)
            {
                r.Release();
            }
            m_band_sdevs = null;
        }

        //Synthesis
        if (m_hfiltered != null) m_hfiltered.Release();
        if (m_loPass != null) m_loPass.Release();

        if (m_aux_noGenmips != null) m_aux_noGenmips.Release();

        if (m_aux_genMips != null)
        {
            foreach (RenderTexture t in m_aux_genMips)
            {
                t.Release();
            }
            m_aux_genMips = null;
        }

        if (m_aux_genMipsb != null)
        {
            foreach (RenderTexture t in m_aux_genMipsb)
            {
                t.Release();
            }
            m_aux_genMipsb = null;
        }

        if (m_aux_noMips != null)
        {
            foreach (RenderTexture t in m_aux_noMips)
            {
                t.Release();
            }
            m_aux_noMips = null;
        }

        //Noise Tile
        if (m_noiseTile != null)
        {
            foreach (RenderTexture t in m_noiseTile)
            {
                t.Release();
            }
            m_noiseTile = null;
        }
    }
    #region shaders

    private Shader m_sdevShader;
    public Shader sdevShader
    {
        get
        {
            if (m_sdevShader == null)
                m_sdevShader = Shader.Find("Hidden/SdevTransform");

            return m_sdevShader;
        }
    }

    private Material m_sdevMaterial;
    public Material sdevMaterial
    {
        get
        {
            if (m_sdevMaterial == null)
            {
                if (sdevShader == null || sdevShader.isSupported == false)
                    return null;

                m_sdevMaterial = new Material(sdevShader);
            }

            return m_sdevMaterial;
        }
    }

    private Shader m_squareShader;
    public Shader squareShader
    {
        get
        {
            if (m_squareShader == null)
                m_squareShader = Shader.Find("Hidden/SquarePixel");

            return m_squareShader;
        }
    }

    private Material m_squareMaterial;
    public Material squareMaterial
    {
        get
        {
            if (m_squareMaterial == null)
            {
                if (squareShader == null || squareShader.isSupported == false)
                    return null;

                m_squareMaterial = new Material(squareShader);
            }

            return m_squareMaterial;
        }
    }

    private Shader m_meanShader;
    public Shader meanShader
    {
        get
        {
            if (m_meanShader == null)
                m_meanShader = Shader.Find("Hidden/MeanMipsTransform");

            return m_meanShader;
        }
    }

    private Material m_meanMaterial;
    public Material meanMaterial
    {
        get
        {
            if (m_meanMaterial == null)
            {
                if (meanShader == null || meanShader.isSupported == false)
                    return null;

                m_meanMaterial = new Material(meanShader);
            }

            return m_meanMaterial;
        }
    }


    private Shader m_meanBasicShader;
    public Shader meanBasicShader
    {
        get
        {
            if (m_meanBasicShader == null)
                m_meanBasicShader = Shader.Find("Hidden/MeanMips");

            return m_meanBasicShader;
        }
    }

    private Material m_meanBasicMaterial;
    public Material meanBasicMaterial
    {
        get
        {
            if (m_meanBasicMaterial == null)
            {
                if (meanBasicShader == null || meanBasicShader.isSupported == false)
                    return null;

                m_meanBasicMaterial = new Material(meanBasicShader);
            }

            return m_meanBasicMaterial;
        }
    }


    private Shader m_passShader;
    public Shader passShader
    {
        get
        {
            if (m_passShader == null)
                m_passShader = Shader.Find("Hidden/Passthrough");

            return m_passShader;
        }
    }

    private Material m_passMaterial;
    public Material passMaterial
    {
        get
        {
            if (m_passMaterial == null)
            {
                if (passShader == null || passShader.isSupported == false)
                    return null;

                m_passMaterial = new Material(passShader);
            }

            return m_passMaterial;
        }
    }

    private Shader m_passShaderTransform;
    public Shader passShaderTransform
    {
        get
        {
            if (m_passShaderTransform == null)
                m_passShaderTransform = Shader.Find("Hidden/PassTransform");

            return m_passShaderTransform;
        }
    }

    private Material m_passMaterialTransform;
    public Material passMaterialTransform
    {
        get
        {
            if (m_passMaterialTransform == null)
            {
                if (passShaderTransform == null || passShaderTransform.isSupported == false)
                    return null;

                m_passMaterialTransform = new Material(passShaderTransform);
            }

            return m_passMaterialTransform;
        }
    }

    private Shader m_convShader;
    public Shader convolutionShader
    {
        get
        {
            if (m_convShader == null)
                m_convShader = Shader.Find("Hidden/ConvolutionTransform");

            return m_convShader;
        }
    }


    private Material m_convolutionMaterial;
    public Material convolutionMaterial
    {
        get
        {
            if (m_convolutionMaterial == null)
            {
                if (convolutionShader == null || convolutionShader.isSupported == false)
                    return null;

                m_convolutionMaterial = new Material(convolutionShader);
            }

            return m_convolutionMaterial;
        }
    }


    private Shader m_copyFoveaShader;
    public Shader copyFoveaShader
    {
        get
        {
            if (m_copyFoveaShader == null)
                m_copyFoveaShader = Shader.Find("Hidden/CopyFoveaSimple");

            return m_copyFoveaShader;
        }
    }


    private Material m_copyFoveaMaterial;
    public Material copyFoveaMaterial
    {
        get
        {
            if (m_copyFoveaMaterial == null)
            {
                if (copyFoveaShader == null || copyFoveaShader.isSupported == false)
                    return null;

                m_copyFoveaMaterial = new Material(copyFoveaShader);
            }

            return m_copyFoveaMaterial;
        }
    }


    private Shader m_shaderMatchNoise;
    public Shader shaderMatchNoise
    {
        get
        {
            if (m_shaderMatchNoise == null)
                m_shaderMatchNoise = Shader.Find("Hidden/MatchNoise");


            return m_shaderMatchNoise;
        }
    }

    private Material m_MaterialMatchNoise;
    public Material materialMatchNoise
    {
        get
        {
            if (m_MaterialMatchNoise == null)
            {
                if (shaderMatchNoise == null || shaderMatchNoise.isSupported == false)
                    return null;

                m_MaterialMatchNoise = new Material(shaderMatchNoise);
            }

            return m_MaterialMatchNoise;
        }
    }

    private Shader m_shaderAdd;
    public Shader shaderAdd
    {
        get
        {
            if (m_shaderAdd == null)
                m_shaderAdd = Shader.Find("Hidden/AddNTransform");


            return m_shaderAdd;
        }
    }

    private Material m_MaterialAdd;
    public Material materialAdd
    {
        get
        {
            if (m_MaterialAdd == null)
            {
                if (shaderAdd == null || shaderAdd.isSupported == false)
                    return null;

                m_MaterialAdd = new Material(shaderAdd);
            }

            return m_MaterialAdd;
        }
    }


    private Shader m_colorShader;
    public Shader colorShader
    {
        get
        {
            if (m_colorShader == null)
                m_colorShader = Shader.Find("Hidden/ColorProcess");

            return m_colorShader;
        }
    }


    private Material m_colorMaterial;
    public Material colorMaterial
    {
        get
        {
            if (m_colorMaterial == null)
            {
                if (colorShader == null || colorShader.isSupported == false)
                    return null;

                m_colorMaterial = new Material(colorShader);
            }

            return m_colorMaterial;
        }
    }


    private Shader m_DebugShader;
    public Shader debugShader
    {
        get
        {
            if (m_DebugShader == null)
                m_DebugShader = Shader.Find("Hidden/Viewer");


            return m_DebugShader;
        }
    }

    private Material m_DebugMaterial;
    public Material debugMaterial
    {
        get
        {
            if (m_DebugMaterial == null)
            {
                if (debugShader == null || debugShader.isSupported == false)
                    return null;

                m_DebugMaterial = new Material(debugShader);
            }

            return m_DebugMaterial;
        }
    }


    #endregion

    private void Update()
    {
        foveationLUT.Alpha = LUTAlpha;
        foveationLUT.Updating = usingHMDFoveation;
        foveationLUT.DownsampleFactor = LUTDownsample;
        foveationLUT.Mode = LUTMode;

    }
    private Texture2D LoadNoise(int size)
    {
        Texture2D fullText = Resources.Load("NoiseTile") as Texture2D;
        Color[] c = fullText.GetPixels(0, 0, size, size);
        Texture2D res = new Texture2D(size, size, TextureFormat.RGBAHalf, true);
        res.SetPixels(c);
        return res;
    }


    void InitTextures()
    {
        OnDisable();

        print("Initializing textures");

        m_camera = GetComponent<Camera>();
        m_pyramidDepth = pyramidDepth;


        //num textures = 1 for H0, N for bands
        m_band_means = new RenderTexture[nbands + 1];

        for (int i = 0; i < nbands + 1; i++)
        {
            m_band_means[i] = new RenderTexture(m_size, m_size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            m_band_means[i].filterMode = FilterMode.Trilinear;
            m_band_means[i].useMipMap = true;
            m_band_means[i].autoGenerateMips = false;
            m_band_means[i].Create();
            m_band_means[i].hideFlags = HideFlags.HideAndDontSave;
        }

        m_band_sdevs = new RenderTexture[nbands + 1];

        for (int i = 0; i < nbands + 1; i++)
        {
            m_band_sdevs[i] = new RenderTexture(m_size, m_size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            m_band_sdevs[i].filterMode = FilterMode.Trilinear;
            m_band_sdevs[i].useMipMap = true;
            m_band_sdevs[i].autoGenerateMips = false;
            m_band_sdevs[i].Create();
            m_band_sdevs[i].hideFlags = HideFlags.HideAndDontSave;
        }


        m_input = new RenderTexture(m_size, m_size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        m_input.filterMode = FilterMode.Trilinear;
        m_input.useMipMap = true;
        m_input.autoGenerateMips = true;
        m_input.Create();
        m_input.hideFlags = HideFlags.HideAndDontSave;

        m_loOutput = new RenderTexture(m_size, m_size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        m_loOutput.filterMode = FilterMode.Trilinear;
        m_loOutput.useMipMap = true;
        m_loOutput.autoGenerateMips = true;
        m_loOutput.Create();
        m_loOutput.hideFlags = HideFlags.HideAndDontSave;

        //Synthesis Textures

        m_hfiltered = new RenderTexture(m_size, m_size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        m_hfiltered.filterMode = FilterMode.Bilinear;
        m_hfiltered.useMipMap = false;
        m_hfiltered.Create();
        m_hfiltered.hideFlags = HideFlags.HideAndDontSave;



        band_tex = new List<RenderTexture>[nbands];

        int size = m_size;
        for (int i = 0; i < nbands; i++)
        {
            band_tex[i] = new List<RenderTexture>();

            size = m_size;
            for (int j = 0; j < m_pyramidDepth; j++)
            {
                ////Combine with noise////
                RenderTexture a = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                a.filterMode = FilterMode.Bilinear;
                a.useMipMap = false;
                a.Create();
                a.hideFlags = HideFlags.HideAndDontSave;
                size >>= 1;
                band_tex[i].Add(a);

            }
        }
        m_loPass = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        m_loPass.filterMode = FilterMode.Bilinear;
        m_loPass.useMipMap = false;
        m_loPass.Create();
        m_loPass.hideFlags = HideFlags.HideAndDontSave;

        size = m_size;

        m_aux_genMips = new List<RenderTexture>();
        for (int j = 0; j < m_pyramidDepth; j++)
        {
            RenderTexture b = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            b.filterMode = FilterMode.Bilinear;
            b.useMipMap = true;
            b.autoGenerateMips = true;
            b.Create();
            b.hideFlags = HideFlags.HideAndDontSave;
            size >>= 1;
            m_aux_genMips.Add(b);
        }

        size = m_size;
        m_aux_genMipsb = new List<RenderTexture>();
        for (int j = 0; j < m_pyramidDepth; j++)
        {
            RenderTexture b = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            b.filterMode = FilterMode.Bilinear;
            b.useMipMap = true;
            b.autoGenerateMips = true;
            b.Create();
            b.hideFlags = HideFlags.HideAndDontSave;
            size >>= 1;
            m_aux_genMipsb.Add(b);
        }

        size = m_size;
        m_aux_noMips = new List<RenderTexture>();
        for (int j = 0; j < m_pyramidDepth; j++)
        {
            RenderTexture b = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            b.filterMode = FilterMode.Bilinear;
            b.useMipMap = false;
            b.autoGenerateMips = false;
            b.Create();
            b.hideFlags = HideFlags.HideAndDontSave;
            size >>= 1;
            m_aux_noMips.Add(b);
        }


        m_aux_noGenmips = new RenderTexture(m_size, m_size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        m_aux_noGenmips.filterMode = FilterMode.Bilinear;
        m_aux_noGenmips.useMipMap = true;
        m_aux_noGenmips.autoGenerateMips = false;
        m_aux_noGenmips.Create();
        m_aux_noGenmips.hideFlags = HideFlags.HideAndDontSave;

        print("Textures initialized");

    }

    void InitNoiseTile()
    {
        print("Initializing noise Tile....");

        if (m_noiseTile != null)
        {
            foreach (RenderTexture t in m_noiseTile)
            {
                t.Release();
            }
            m_noiseTile = null;
        }

        Texture2D noiseBase;
        if (preLoadNoise)
        {
            noiseBase = LoadNoise(m_size);
        }
        else
        {

            noiseBase = new Texture2D(m_size, m_size, TextureFormat.RGBAFloat, true);
            for (int i = 0; i < m_size; i++)
            {
                for (int j = 0; j < m_size; j++)
                {
                    noiseBase.SetPixel(i, j, new Color(Random.value, Random.value, Random.value, 1.0f));
                }
            }
            //byte[] bytes = noiseBase.EncodeToEXR();
            //print("writing noise at " + Application.dataPath + "/Resources/NoiseTile.exr " + m_size );
            //System.IO.File.WriteAllBytes(Application.dataPath + "/Resources/NoiseTile.exr", bytes);
        }
        noiseBase.Apply();
        //Get pyramid of white noise: 
        //if (differentNoisePerLevel)
        //{
        //    m_noiseTile = PyramidUtils.SteerablePyramidNewNoise(m_size, m_pyramidDepth, -1, 10, m_hiq, m_preFilter);
        //}
        //else
        //{
        m_noiseTile = PyramidUtils.SteerablePyramid(noiseBase, pyramidDepth, m_nBands, -1, 10, m_hiq, m_preFilter);
        //}

        print("Noise tile initialized");

    }

    public void MatchNoise(RenderTexture output, int lod, int idx, int size, int filter, float k)
    {
        materialMatchNoise.SetFloat("_LOD", (float)lod);
        materialMatchNoise.SetTexture("_StdevTex", m_band_sdevs[idx]);
        materialMatchNoise.SetTexture("_MeanTex", m_band_means[idx]);
        materialMatchNoise.SetTexture("_NoiseTex", m_noiseTile[idx]);


        if (!m_preFilter)
        {
            Graphics.SetRenderTarget(m_aux_noGenmips, lod);
            Graphics.Blit(null, materialMatchNoise);
            convolutionMaterial.SetFloat("_LOD", (float)lod);
            convolutionMaterial.SetVector("_TexelSize", Vector2.one / (float)(size - 1));
            convolutionMaterial.SetFloat("_K", k);
            convolutionMaterial.SetFloatArray("_Kernel", PyramidUtils.getFilter(filter, m_hiq, false, m_nBands));
            convolutionMaterial.SetInt("_KernelWidth", PyramidUtils.getFilterWidth(filter, m_hiq, false, m_nBands));
            Graphics.Blit(m_aux_noGenmips, output, convolutionMaterial);
        }
        else
        {
            Graphics.SetRenderTarget(output, 0);
            Graphics.Blit(null, materialMatchNoise);
        }

    }


    public void SteerableFilter(RenderTexture from, RenderTexture to, int filter, float lod, float k, int size, bool flipSignal, bool prefilter, bool combinedMatrix, bool hiq)
    {

        ///
        if (FullL0Approach)
        {
            convolutionMaterial.SetInt("_L0Approach", 1);

        }
        else
        {
            convolutionMaterial.SetInt("_L0Approach", 0);
        }

        convolutionMaterial.SetInt("_CurrentLevel", (int)lod);

        convolutionMaterial.SetVector("_TexelSize", Vector2.one / (float)(size - 1));

        convolutionMaterial.SetFloat("_LOD", (float)lod);
        convolutionMaterial.SetFloat("_K", k);
        convolutionMaterial.SetFloatArray("_Kernel", PyramidUtils.getFilter(filter, hiq, combinedMatrix, nbands));
        convolutionMaterial.SetInt("_KernelWidth", PyramidUtils.getFilterWidth(filter, hiq, combinedMatrix, nbands));

        if (preFilter && !combinedMatrix)
        {
            Graphics.SetRenderTarget(m_aux_noGenmips, (int)lod);
            Graphics.Blit(from, convolutionMaterial);

            int sfOut2 = Shader.PropertyToID("_SteerableFilterOutput2");
            if (flipSignal)
                convolutionMaterial.SetFloat("_K", -k);
            Graphics.SetRenderTarget(to, (int)lod);
            Graphics.Blit(m_aux_noGenmips, convolutionMaterial);
        }
        else
        {
            Graphics.SetRenderTarget(to, (int)lod);
            Graphics.Blit(from, convolutionMaterial);

        }
    }

    private void calculateStats(RenderTexture input, RenderTexture outputMean, RenderTexture outputSdev, int mip, int size, float meanDepth, float foveaSize, float foveaX, float foveaY)
    {

        ////////MEAN/////////
        ///
        if (FullL0Approach)
        {
            meanMaterial.SetInt("_L0Approach", 1);
            sdevMaterial.SetInt("_L0Approach", 1);
        }
        else
        {

            meanMaterial.SetInt("_L0Approach", 0);
            sdevMaterial.SetInt("_L0Approach", 0);
        }


        if (usingHMDFoveation)
        {
            meanMaterial.SetTexture("_FoveationLUT", foveationLUT.EccentricityTex);
            meanMaterial.SetInt("_useLUT", 1);
            sdevMaterial.SetTexture("_FoveationLUT", foveationLUT.EccentricityTex);
            sdevMaterial.SetInt("_useLUT", 1);
        }
        else
        {
            meanMaterial.SetInt("_useLUT", 0);
            sdevMaterial.SetInt("_useLUT", 0);

        }


        meanMaterial.SetInt("_CurrentLevel", mip);
        sdevMaterial.SetInt("_CurrentLevel", mip);

        //Lets get mipmaps for I at LOD i so we can calculate the mean.
        passMaterial.SetFloat("_LOD", (float)mip);
        Graphics.SetRenderTarget(m_aux_genMips[mip], 0);
        Graphics.Blit(input, passMaterial);


        ////STANDARD DEVIATION////
        //Square of the input image, result already has mips because we have autoGenerateMips.true
        squareMaterial.SetFloat("_LOD", (float)mip);
        Graphics.SetRenderTarget(m_aux_genMipsb[mip], 0);
        Graphics.Blit(input, squareMaterial);

        sdevMaterial.SetVector("_TexelSize", Vector2.one / (float)(size - 1));
        sdevMaterial.SetFloat("_MeanDepth", (float)meanDepth);
        sdevMaterial.SetFloat("_FoveaSize", (float)foveaSize);
        sdevMaterial.SetFloat("_FoveaX", (float)foveaX);
        sdevMaterial.SetFloat("_FoveaY", (float)foveaY);

        sdevMaterial.SetTexture("_SecondaryTex", m_aux_genMipsb[mip]);
        Graphics.SetRenderTarget(outputSdev, mip);
        Graphics.Blit(m_aux_genMips[mip], sdevMaterial);

        meanMaterial.SetVector("_TexelSize", Vector2.one / (float)(size - 1));
        meanMaterial.SetFloat("_MeanDepth", (float)meanDepth);
        meanMaterial.SetFloat("_FoveaSize", (float)foveaSize);
        meanMaterial.SetFloat("_FoveaX", (float)foveaX);
        meanMaterial.SetFloat("_FoveaY", (float)foveaY);

        //Calculate the mean (by looking up a lower mip level). Result doesn't need mips.
        Graphics.SetRenderTarget(outputMean, mip);
        Graphics.Blit(m_aux_genMips[mip], meanMaterial);

    }

    public void Analysis(RenderTexture source, RenderTexture destination)
    {
        colorMaterial.SetInt("_direction", 1);
        colorMaterial.SetInt("_isLinearColorSpace", isLinearColorspace ? 1 : 0);

        colorMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        colorMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        colorMaterial.SetFloat("_texSize", m_size);

        if (displayMode == DisplayMode.INPUT_YCBCR)
        {
            Graphics.Blit(source, destination, colorMaterial);
            return;
        }

        convolutionMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        convolutionMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        convolutionMaterial.SetFloat("_texSize", m_size);

        meanMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        meanMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        meanMaterial.SetFloat("_texSize", m_size);

        sdevMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        sdevMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        sdevMaterial.SetFloat("_texSize", m_size);

        Graphics.SetRenderTarget(m_input);
        Graphics.Blit(source, colorMaterial);

        //to allow bigger filters
        convolutionMaterial.SetFloatArray("_Kernel", PyramidUtils.getFilter(0, true, false, 4));

        convolutionMaterial.SetFloat("_MeanDepth", (float)meanDepth);
        convolutionMaterial.SetFloat("_FoveaSize", (float)foveaSize);
        convolutionMaterial.SetFloat("_FoveaX", (float)foveaX);
        convolutionMaterial.SetFloat("_FoveaY", (float)foveaY);

        //We need a L0 pass to work with the pyramid, and this needs mips.
        //Did not run this on the SteerableFilter method as its a special case

        convolutionMaterial.SetFloat("_LOD", 0);
        convolutionMaterial.SetVector("_TexelSize", Vector2.one / (float)(m_size - 1));
        convolutionMaterial.SetFloat("_K", 1);
        convolutionMaterial.SetFloatArray("_Kernel", PyramidUtils.getFilter(1, m_hiq, combinedMatrix, m_nBands));
        convolutionMaterial.SetInt("_KernelWidth", PyramidUtils.getFilterWidth(1, m_hiq, combinedMatrix, m_nBands));
        Graphics.SetRenderTarget(m_loOutput, 0);
        Graphics.Blit(m_input, convolutionMaterial);


        //First level is a Hipass
        SteerableFilter(m_input, m_band_means[0], 0, 0, 1, m_size, false, m_preFilter, combinedMatrix, m_hiq);
        if(displayMode == DisplayMode.NORMAL || displayMode == DisplayMode.SHOW_STATS)
            calculateStats(m_band_means[0], m_band_means[0], m_band_sdevs[0], 0, m_size, meanDepth, foveaSize, foveaX, foveaY);


        int s = m_size >> m_pyramidDepth;

        int inc = 1;
        int start = 0;
        if (UseWhichFilters == WhichFilters.ODD)
        {
            inc = 2;
        }
        if (UseWhichFilters == WhichFilters.EVEN)
        {
            inc = 2;
            start = 1;
        }

        //then we do all the bands.
        for (int i = m_pyramidDepth - 1; i >= 0; i--)
        {
            s <<= 1;
            for (int j = start; j < nbands; j += inc)
            {
                SteerableFilter(m_loOutput, m_band_means[j+1], 2 + j, i, 1, s, true, preFilter, combinedMatrix, hiq);
                if(displayMode == DisplayMode.NORMAL || displayMode == DisplayMode.SHOW_STATS)
                    calculateStats(m_band_means[j+1], m_band_means[j+1], m_band_sdevs[j+1], i, s, meanDepth, foveaSize, foveaX, foveaY);
            }
        }

        if (displayMode != DisplayMode.NORMAL)
        {
            debugMaterial.SetInt("_DisplayMode", showDisplayMode);
            debugMaterial.SetFloat("_LOD", showMip);
            RenderTexture debug = null;
            if (showStep == 0) debug = m_band_means[showBand];
            else debug = m_band_sdevs[showBand];

            if (showTransformRect)
            {
                Graphics.SetRenderTarget(m_aux_noMips[0], 0);
                Graphics.Blit(debug, debugMaterial);

                passMaterialTransform.SetFloat("_screenWidth", m_camera.pixelWidth);
                passMaterialTransform.SetFloat("_screenHeight", m_camera.pixelHeight);
                passMaterialTransform.SetFloat("_texSize", m_size);
                passMaterialTransform.SetInt("_direction", -1);
                Graphics.SetRenderTarget(destination, 0);
                Graphics.Blit(m_aux_noMips[0], passMaterialTransform);

            }
            else
            {
                Graphics.SetRenderTarget(destination, 0);
                Graphics.Blit(debug, debugMaterial);

            }
        }
    }


    public void Synthesis(RenderTexture destination)
    {

        materialMatchNoise.SetFloat("_screenWidth", m_camera.pixelWidth);
        materialMatchNoise.SetFloat("_screenHeight", m_camera.pixelHeight);
        materialMatchNoise.SetFloat("_texSize", m_size);

        convolutionMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        convolutionMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        convolutionMaterial.SetFloat("_texSize", m_size);

        materialAdd.SetFloat("_screenWidth", m_camera.pixelWidth);
        materialAdd.SetFloat("_screenHeight", m_camera.pixelHeight);
        materialAdd.SetFloat("_texSize", m_size);

        copyFoveaMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        copyFoveaMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        copyFoveaMaterial.SetFloat("_texSize", m_size);

        //Go through each level, combine with noise, and Filter
        List<RenderTexture> levelResults = new List<RenderTexture>();

        int size = m_size;

        ////Do it to H0 first////

        // to allow bigger size
        convolutionMaterial.SetFloat("_K", 1);
        convolutionMaterial.SetFloatArray("_Kernel", PyramidUtils.getFilter(0, true, false, 4));

        MatchNoise(m_hfiltered, 0, 0, size, 0, 1);

        int inc = 1;
        int start = 0;
        if (UseWhichFilters == WhichFilters.ODD)
        {
            inc = 2;
        }
        if (UseWhichFilters == WhichFilters.EVEN)
        {
            inc = 2;
            start = 1;
        }

        for (int i = 0; i < m_pyramidDepth; i++)
        {
            ////Combine with noise////
            for (int j = start; j < m_nBands; j += inc)
            {
                MatchNoise(band_tex[j][i], i, j + 1, size, j + 2, -1);
            }
            int ntex = 0;
            for (int j = start; j < m_nBands; j += inc)
            {
                string name = "_Tex" + (ntex + 1);
                materialAdd.SetTexture(name, band_tex[j][i]);
                ntex++;
            }
            materialAdd.SetInt("_NTextures", ntex);
            Graphics.SetRenderTarget(m_aux_noMips[i], 0);
            Graphics.Blit(null, materialAdd);
            levelResults.Add(m_aux_noMips[i]);

            size >>= 1;
        }

        //Lowpass Residual
        if (FullL0Approach) //Here we need to blurperiphery basically
        {
            meanMaterial.SetInt("_CurrentLevel", 0);
            meanMaterial.SetInt("_L0Approach", 0);

            meanMaterial.SetVector("_TexelSize", Vector2.one / (float)(size - 1));
            meanMaterial.SetFloat("_FoveaX", (float)foveaX);
            meanMaterial.SetFloat("_FoveaY", (float)foveaY);
            if (usingHMDFoveation)
            {
                meanMaterial.SetTexture("_FoveationLUT", foveationLUT.EccentricityTex);
                meanMaterial.SetInt("_useLUT", 1);
            }
            else
            {
                meanMaterial.SetInt("_useLUT", 0);
                meanMaterial.SetFloat("_MeanDepth", (float)meanDepth);
                meanMaterial.SetFloat("_FoveaSize", (float)foveaSize);
            }

            //Calculate the mean (by looking up a lower mip level). Result doesn't need mips.
            Graphics.SetRenderTarget(m_loOutput, 0);
            Graphics.Blit(m_input, meanMaterial);
            materialAdd.SetTexture("_Tex1", m_loOutput);

            //Combine pyramid first bands, add blur in the end highres
            materialAdd.SetInt("_NTextures", 2);
            materialAdd.SetTexture("_Tex1", levelResults[levelResults.Count - 1]);
            for (int i = levelResults.Count - 2; i >= 0; i--)
            {
                materialAdd.SetTexture("_Tex2", levelResults[i]);
                Graphics.SetRenderTarget(band_tex[0][i]);
                Graphics.Blit(null, materialAdd);
                materialAdd.SetTexture("_Tex1", band_tex[0][i]);
            }
            materialAdd.SetTexture("_Tex1", band_tex[0][0]);
            materialAdd.SetTexture("_Tex2", m_loOutput);
            Graphics.SetRenderTarget(m_aux_noGenmips);
            Graphics.Blit(null, materialAdd);

        }
        else
        {   //Just grab lowpass from mips
            colorMaterial.SetFloat("_LOD", (float)m_pyramidDepth);
            colorMaterial.SetInt("_direction", 0);
            Graphics.SetRenderTarget(m_loPass, 0);
            Graphics.Blit(m_loOutput, colorMaterial); 

            materialAdd.SetTexture("_Tex1", m_loPass);
            materialAdd.SetInt("_NTextures", 2);
            materialAdd.SetTexture("_Tex2", levelResults[levelResults.Count - 1]);
            Graphics.SetRenderTarget(band_tex[0][levelResults.Count - 1]);
            Graphics.Blit(null, materialAdd);
            for (int i = levelResults.Count - 2; i > 0; i--)
            {
                materialAdd.SetTexture("_Tex1", band_tex[0][i + 1]);
                materialAdd.SetTexture("_Tex2", levelResults[i]);
                Graphics.SetRenderTarget(band_tex[0][i]);
                Graphics.Blit(null, materialAdd);
            }
            materialAdd.SetTexture("_Tex1", band_tex[0][1]);
            materialAdd.SetTexture("_Tex2", levelResults[0]);
            Graphics.SetRenderTarget(m_aux_noGenmips);
            Graphics.Blit(null, materialAdd);
        }

        ////WebGL doesnt like this
        //materialAdd.SetInt("_NTextures", levelResults.Count + 1);
        //int k = 2;
        //foreach (RenderTexture t in levelResults)
        //{
        //    string name = "_Tex" + k++;
        //    materialAdd.SetTexture(name, t);
        //}
        //Graphics.SetRenderTarget(m_aux_noGenmips, 0);
        //Graphics.Blit(null, materialAdd);

        convolutionMaterial.SetFloat("_LOD", (float)0);
        convolutionMaterial.SetVector("_TexelSize", Vector2.one / (float)(m_size - 1));
        convolutionMaterial.SetFloat("_K", 1);
        convolutionMaterial.SetFloatArray("_Kernel", PyramidUtils.getFilter(1, m_hiq, false, m_nBands));
        convolutionMaterial.SetInt("_KernelWidth", PyramidUtils.getFilterWidth(1, m_hiq, false, m_nBands));
        Graphics.SetRenderTarget(m_aux_noMips[0], 0);
        Graphics.Blit(m_aux_noGenmips, convolutionMaterial);

        materialAdd.SetTexture("_Tex1", m_aux_noMips[0]);
        materialAdd.SetTexture("_Tex2", m_hfiltered);
        materialAdd.SetInt("_NTextures", 2);

        Graphics.SetRenderTarget(m_aux_noGenmips, 0);
        Graphics.Blit(null, materialAdd); ;

        if (usingHMDFoveation)
        {
            copyFoveaMaterial.SetTexture("_FoveationLUT", foveationLUT.EccentricityTex);
            copyFoveaMaterial.SetInt("_useLUT", 1);

        }
        else
        {
            copyFoveaMaterial.SetInt("_useLUT", 0);

        }

        copyFoveaMaterial.SetInt("_transform", 1);
        copyFoveaMaterial.SetInt("_Blend", 1);
        copyFoveaMaterial.SetFloat("_FoveaSize", foveaSize);
        copyFoveaMaterial.SetFloat("_FoveaX", foveaX);
        copyFoveaMaterial.SetFloat("_FoveaY", foveaY);
        copyFoveaMaterial.SetTexture("_SecondTex", m_aux_noGenmips);
        Graphics.SetRenderTarget(m_aux_noMips[0], 0);
        Graphics.Blit(m_input, copyFoveaMaterial);



        colorMaterial.SetFloat("_LOD", (float)0);

        colorMaterial.SetInt("_direction", -1);
        colorMaterial.SetInt("_isLinearColorSpace", isLinearColorspace ? 1 : 0);
        colorMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        colorMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        colorMaterial.SetFloat("_texSize", m_size);

        Graphics.Blit(m_aux_noMips[0], destination, colorMaterial);

        // ZH
        //RTUtil.saveRT(destination, "dest");

    }

    public void BlurPeriphery(RenderTexture source, RenderTexture destination)
    {
        colorMaterial.SetInt("_direction", 1);
        colorMaterial.SetInt("_isLinearColorSpace", isLinearColorspace ? 1 : 0);

        colorMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        colorMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        colorMaterial.SetFloat("_texSize", m_size);

        Graphics.SetRenderTarget(m_input);
        Graphics.Blit(source, colorMaterial);

        meanMaterial.SetInt("_L0Approach", 0);
        meanMaterial.SetInt("_CurrentLevel", 0);
        meanMaterial.SetVector("_TexelSize", Vector2.one / (float)(m_size - 1));
        meanMaterial.SetFloat("_FoveaX", (float)foveaX);
        meanMaterial.SetFloat("_FoveaY", (float)foveaY);
        if (usingHMDFoveation)
        {
            meanMaterial.SetTexture("_FoveationLUT", foveationLUT.EccentricityTex);
            meanMaterial.SetInt("_useLUT", 1);
        }
        else
        {
            meanMaterial.SetInt("_useLUT", 0);
            meanMaterial.SetFloat("_MeanDepth", (float)meanDepth);
            meanMaterial.SetFloat("_FoveaSize", (float)foveaSize);
        }

        //Calculate the mean (by looking up a lower mip level). Result doesn't need mips.
        Graphics.SetRenderTarget(m_loOutput, 0);
        Graphics.Blit(m_input, meanMaterial);

        colorMaterial.SetFloat("_LOD", (float)0);

        colorMaterial.SetInt("_direction", -1);
        colorMaterial.SetInt("_isLinearColorSpace", isLinearColorspace ? 1 : 0);
        colorMaterial.SetFloat("_screenWidth", m_camera.pixelWidth);
        colorMaterial.SetFloat("_screenHeight", m_camera.pixelHeight);
        colorMaterial.SetFloat("_texSize", m_size);

        Graphics.Blit(m_loOutput, destination, colorMaterial);



    }
    public void OnRenderImage(RenderTexture source, RenderTexture destination)
    {

        if (m_camera == null)
        {
            m_camera = GetComponent<Camera>();
        }
        int size = (int)Mathf.Max((float)m_camera.pixelWidth, (float)m_camera.pixelHeight);
        size = (int)Mathf.Min((float)Mathf.NextPowerOfTwo(size), (float)MAXIMUM_BUFFER_SIZE);
        if (m_noiseTile == null || m_pyramidDepth != pyramidDepth || m_preFilter != preFilter || m_nBands != nbands || m_size != size || m_hiq != hiq)
        {
            m_hiq = hiq;
            m_size = size;
            m_nBands = nbands;
            m_preFilter = preFilter;
            m_pyramidDepth = pyramidDepth;
            m_newNoise = newNoise;
            m_preLoadNoise = preLoadNoise;
            InitTextures();
            InitNoiseTile();

        }
        else if (m_newNoise != newNoise || m_preLoadNoise != preLoadNoise)
        {
            m_newNoise = newNoise;
            m_preLoadNoise = preLoadNoise;
            InitNoiseTile();

        }
        if (justBlur)
        {
            BlurPeriphery(source, destination);
        }
        else { 
            Analysis(source,destination);
            if(displayMode == DisplayMode.NORMAL) Synthesis(destination);
        }
    }

}


