using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 새 꽃의 밤 진단 그림을 Play 없이 바로 확인하는 에디터 창. 꽃과 아픔을 고르고 [적용]을 누르면 02NightPuzzle 씬을 새로 불러와 그 꽃에 아픔을 그려 보여 준다.
/// 적용한 모습은 저장하지 않은 미리보기이므로 씬을 저장하면 안 된다. [초기화]는 씬을 디스크에서 다시 불러와 미리보기를 지운다(미리보기 중에 한 다른 편집도 함께 사라진다).
/// </summary>
public class NightFlowerPreviewWindow : EditorWindow
{
    private const string ScenePath = "Assets/01Scenes/02Night/02NightPuzzle.unity";

    [SerializeField] private int flowerIndex;
    [SerializeField] private bool[] ailments = new bool[System.Enum.GetValues(typeof(FlowerData.Ailment)).Length];
    [SerializeField] private bool previewing;

    private List<FlowerData> flowers = new();

    [MenuItem("Tools/Night/Flower Preview")]
    private static void Open() => GetWindow<NightFlowerPreviewWindow>("Night Flower Preview");

    private void OnEnable() => LoadFlowers();

    private void LoadFlowers()
    {
        flowers = AssetDatabase.FindAssets("t:FlowerData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<FlowerData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(flower => flower != null).OrderBy(flower => flower.flowerID).ToList();
        flowerIndex = Mathf.Clamp(flowerIndex, 0, Mathf.Max(0, flowers.Count - 1));
    }

    private void OnGUI()
    {
        if (flowers.Count == 0)
        {
            EditorGUILayout.HelpBox("FlowerData가 없습니다.", MessageType.Warning);
            if (GUILayout.Button("다시 찾기")) LoadFlowers();
            return;
        }

        flowerIndex = EditorGUILayout.Popup("꽃", flowerIndex, flowers.Select(Label).ToArray());
        FlowerData flower = flowers[flowerIndex];
        string problem = flower.petalPartSprites == null || flower.petalPartSprites.Length != FlowerData.PetalPartCount ? "꽃잎 그림 배열이 6칸이 아니라서 미리보기를 할 수 없습니다." : null;
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        else if (!flower.HasDiagnosisArt) EditorGUILayout.HelpBox("진단 그림이 일부 비어 있습니다(비어 있는 부위는 숨겨서 보여 줍니다). 게임에서는 그림이 다 있어야 이 꽃이 진단 꽃으로 나옵니다.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("아픔");
        for (int i = 0; i < ailments.Length; i++)
        {
            var ailment = (FlowerData.Ailment)i;
            bool noLossArt = ailment == FlowerData.Ailment.PetalLoss && (flower.lossPetals == null || flower.lossPetals.Length == 0);
            using (new EditorGUI.DisabledScope(noLossArt))
                ailments[i] = EditorGUILayout.ToggleLeft(NightDiagnosisUI.AilmentName(ailment) + (noLossArt ? " (결손 그림 없음)" : ""), ailments[i] && !noLossArt);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(problem != null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("적용", GUILayout.Height(28))) Apply(flower);
        }
        using (new EditorGUI.DisabledScope(!previewing || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("초기화(씬 다시 불러오기)")) ClearPreview();
        }

        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Play 중에는 쓸 수 없습니다.", MessageType.Info);
        else if (previewing) EditorGUILayout.HelpBox("미리보기 중입니다. 이 상태로 씬을 저장하지 마세요(저장했다면 초기화 전에 되돌려야 합니다).", MessageType.Warning);
    }

    private static string Label(FlowerData flower) => $"{flower.flowerID:00} {flower.DisplayName}{(flower.HasDiagnosisArt ? "" : " (그림 일부 없음)")}";

    private void Apply(FlowerData flower)
    {
        if (!OpenCleanScene()) return;

        var ui = Object.FindFirstObjectByType<NightDiagnosisUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Night Flower Preview", "씬에서 NightDiagnosisUI를 찾지 못했습니다.", "확인");
            return;
        }

        var previewCase = new NightDiagnosisCase { flower = flower, art = flower };
        previewCase.ailments = Enumerable.Range(0, ailments.Length).Where(i => ailments[i]).Select(i => (FlowerData.Ailment)i).ToList();
        if (previewCase.ailments.Contains(FlowerData.Ailment.PetalScatter))
        {
            previewCase.keptPetals = Enumerable.Range(0, FlowerData.PetalPartCount).OrderBy(_ => Random.value)
                .Take(Random.Range(NightDiagnosisCase.MinKeptPetals, NightDiagnosisCase.MaxKeptPetals + 1)).ToList();
        }

        ui.EditorPreview(previewCase);
        previewing = true;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    // 이름을 Reset으로 하면 유니티가 창을 만들 때 저절로 불러 버린다(씬이 다시 열리며 에디터가 멈춘다)
    private void ClearPreview()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        previewing = false;
    }

    /// <summary>미리보기를 그릴 깨끗한 02NightPuzzle 씬을 연다. 사용자의 저장 안 한 편집을 잃게 되면 하지 않는다.</summary>
    private bool OpenCleanScene()
    {
        // 미리보기는 씬을 dirty로 표시하지 않으므로 dirty 여부만으로는 이전 미리보기가 남았는지 알 수 없다. 항상 디스크에서 다시 불러온다.
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool onlyPreviewChanges = active.path == ScenePath && previewing;
        if (active.isDirty && !onlyPreviewChanges && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return true;
    }

}
