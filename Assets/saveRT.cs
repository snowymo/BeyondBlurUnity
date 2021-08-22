using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class saveRT : MonoBehaviour
{

    public RenderTexture rt;

    public string pngOutPath;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var oldRT = RenderTexture.active;
            RenderTexture.active = rt;
            RTUtil.saveRT(rt, pngOutPath);
            RenderTexture.active = oldRT;
        }
    }
}
