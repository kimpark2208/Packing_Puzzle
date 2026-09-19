using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

public class CreateRedCube
{
    [MenuItem("Tools/Create Red Cube at (0,2,0)")]
    public static void CreateCube()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "RedCube";
        cube.transform.position = new Vector3(0, 2, 0);

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.red;
            renderer.material = mat;
        }

        Debug.Log("[CreateRedCube] 빨간색 큐브 생성: (0, 2, 0)");
    }
}
#endif
