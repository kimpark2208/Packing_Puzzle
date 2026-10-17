using UnityEngine;
using UnityEditor;

// BlockData 인스펙터 창을 바둑판 토글 형태로 강제 재정의합니다.
[CustomEditor(typeof(BlockData))]
public class BlockDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. 기존의 기본 항목들(ID, Score, Color, 프리팹 등)을 먼저 정상적으로 그립니다.
        DrawDefaultInspector();

        BlockData data = (BlockData)target;
        if (data.shapeGrid == null || data.shapeGrid.Length == 0) return;

        GUILayout.Space(25);
        GUILayout.Label("🎨 블록 모양 직관적 편집기 (바둑판)", EditorStyles.boldLabel);

        // 2. shapeGrid 배열을 순회하며 진짜 바둑판 모양 체크박스를 그립니다.
        for (int r = 0; r < data.shapeGrid.Length; r++)
        {
            if (data.shapeGrid[r].cols == null) continue;

            // 가로 한 줄 정렬 시작
            GUILayout.BeginHorizontal();
            
            for (int c = 0; c < data.shapeGrid[r].cols.Length; c++)
            {
                // 가로로 한 칸씩 토글(체크박스)을 나열합니다. (크기 25x25)
                data.shapeGrid[r].cols[c] = EditorGUILayout.Toggle(data.shapeGrid[r].cols[c], GUILayout.Width(25));
            }
            
            // 가로 한 줄 정렬 끝
            GUILayout.EndHorizontal(); 
        }

        // 3. 인스펙터 창에서 마우스로 클릭해서 바꾼 값이 SO 파일에 실시간 세이브되도록 강제 설정
        if (GUI.changed)
        {
            EditorUtility.SetDirty(data);
        }
    }
}
