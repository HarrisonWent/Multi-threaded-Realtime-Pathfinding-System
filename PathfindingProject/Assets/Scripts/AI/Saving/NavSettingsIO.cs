using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class NavSettingsIO : MonoBehaviour
{
    public static void SaveSurfaceSettings()
    {
        CheckDirectory();

        string path = Application.dataPath + "/Resources/" + "SceneData\\" + "SurfaceData.txt";

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        StreamWriter f = File.CreateText(path);

        CustomNavigationSurface[] customNavigationSurfaces = Navigation.SurfaceTypes.ToArray();

        f.WriteLine(customNavigationSurfaces.Length);

        foreach(CustomNavigationSurface customNavigationSurface in customNavigationSurfaces)
        {
            f.WriteLine(customNavigationSurface.SurfaceName);
            f.WriteLine(customNavigationSurface.HiddenID);
            f.WriteLine(customNavigationSurface.Breakable);
            f.WriteLine(customNavigationSurface.BreakCost);
            f.WriteLine(customNavigationSurface.DebugDisplay);

            f.WriteLine(customNavigationSurface.DebugColour.r + "," + customNavigationSurface.DebugColour.g + "," + customNavigationSurface.DebugColour.b);
        }

        f.Close();
    }

    public static void LoadSurfaces()
    {
        CheckDirectory();

        string path = Application.dataPath + "/Resources/" + "SceneData\\" + "SurfaceData.txt";

        if (File.Exists(path))
        {
            //Debug.Log("Found saved surfaces");

            StreamReader sr = File.OpenText(path);

            int SurfaceCount = int.Parse(sr.ReadLine());

            List<CustomNavigationSurface> surfaces = new List<CustomNavigationSurface>();

            for(int i = 0; i< SurfaceCount; i++)
            {
                CustomNavigationSurface customNavigationSurface = new CustomNavigationSurface();
                customNavigationSurface.SurfaceName = sr.ReadLine();
                //Debug.Log("Loaded: " + customNavigationSurface.SurfaceName);
                customNavigationSurface.HiddenID = byte.Parse(sr.ReadLine());
                customNavigationSurface.Breakable = bool.Parse(sr.ReadLine());
                customNavigationSurface.BreakCost = int.Parse(sr.ReadLine());
                customNavigationSurface.DebugDisplay = bool.Parse(sr.ReadLine());

                string col = sr.ReadLine();
                string[] cols = col.Split(',');
                customNavigationSurface.DebugColour = new Color(float.Parse(cols[0]), float.Parse(cols[1]), float.Parse(cols[2]));

                surfaces.Add(customNavigationSurface);
            }

            sr.Close();

            Navigation.SetSurfaces(surfaces);
        }

        Debug.Log("Loaded your project surfaces");
    }

    private static void CheckDirectory()
    {
        if (!Directory.Exists(Application.dataPath + "/Resources/"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources/");
        }

        if (!Directory.Exists(Application.dataPath + "/Resources/" + "SceneData"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Resources/" + "SceneData");
        }
    }
}
