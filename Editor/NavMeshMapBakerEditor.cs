#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavMeshMapBaker))]
public class NavMeshMapBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NavMeshMapBaker baker = (NavMeshMapBaker)target;

        EditorGUILayout.Space(10);

        // 烘焙并保存
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("烘焙地图纹理并保存", GUILayout.Height(35)))
        {
            baker.BakeAndSave();
        }
        GUI.backgroundColor = Color.white;

        // 显示已有数据
        if (baker.floors != null && baker.floors.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"已烘焙: {baker.floors.Count} 层", EditorStyles.boldLabel);
            EditorGUILayout.Vector3Field("地图原点", baker.mapOrigin);

            foreach (var floor in baker.floors)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  第{floor.index}层  Y={floor.baseY:F1}m");
                if (floor.texture != null)
                {
                    GUILayout.Label(floor.texture, GUILayout.Width(64), GUILayout.Height(64));
                }
                else
                {
                    EditorGUILayout.LabelField("  [纹理丢失]");
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // 清除
        if (baker.floors != null && baker.floors.Count > 0)
        {
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.9f, 0.5f, 0.5f);
            if (GUILayout.Button("清除已烘焙数据"))
            {
                baker.floors.Clear();
                baker.mapOrigin = Vector3.zero;
                EditorUtility.SetDirty(baker);
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
#endif