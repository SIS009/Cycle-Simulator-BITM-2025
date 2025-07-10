using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ERPackageUpgrade : Editor
{
	[InitializeOnLoadMethod]
	static void ERPackageUpgradeInit()
	{

#if UNITY_2022

		if (EditorPrefs.GetString("unityVersion") != "Unity2022_b9")
		{
			//EditorPrefs.SetString("unityVersion", "Unity2022_b9"); 
			//AssetDatabase.ImportPackage("Assets/EasyRoads3D/EasyRoads3Dv3.3b9-Unity2022.unitypackage", false);
			//Debug.Log("EasyRoads3D Demo packages upgraded to Unity 2022");
		}

#elif UNITY_2021_2_OR_NEWER

		if (EditorPrefs.GetString("unityVersion") != "Unity2021_2_b9")
		{
			EditorPrefs.SetString("unityVersion", "Unity2021_2_b9");
			AssetDatabase.ImportPackage("Assets/EasyRoads3D/EasyRoads3Dv3.3b9-Unity2021.2+.unitypackage", false);
			Debug.Log("EasyRoads3D Demo packages upgraded to Unity 2021.2+");
		}

#endif

	}

}
