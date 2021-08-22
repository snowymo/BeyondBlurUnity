using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public static class RTUtil
{
    public static void saveRT(RenderTexture destination, string pngOutPath)
    {
        var tex = new Texture2D(destination.width, destination.height);
        tex.ReadPixels(new Rect(0, 0, destination.width, destination.height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(Application.dataPath + "/" + pngOutPath + ".png", tex.EncodeToPNG());
        Debug.Log(Application.dataPath + "/" + pngOutPath + ".png");
    }
}
