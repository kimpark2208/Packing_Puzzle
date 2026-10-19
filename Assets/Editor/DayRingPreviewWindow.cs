using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 낮 퍼즐 포장지 보드를 Play 없이 확인하는 에디터 창. 포장지와 프리셋을 고르고 링별(중앙, 1링, 2링, 3링) 칸 그림을 바꿔 가며 [적용]을 누르면 03DayPuzzle 씬을 새로 불러와 그 모습을 보여 준다.
/// 적용한 모습은 저장하지 않은 미리보기이므로 씬을 저장하면 안 된다. [초기화]는 씬을 디스크에서 다시 불러와 미리보기를 지운다(미리보기 중에 한 다른 편집도 함께 사라진다).
/// 칸 그림을 바꿔 본 결과를 씬에 남기려면 미리보기를 지운 뒤 씬의 칸 이미지(SlotArea 아래)를 직접 바꾼다.
/// </summary>
public class DayRingPreviewWindow : EditorWindow
{
    private const string ScenePath = "Assets/01Scenes/01Day/03DayPuzzle.unity";
    private static readonly string[] RingNames = { "중앙", "1링", "2링", "3링" };

    [SerializeField] private int wrapperIndex;
    [SerializeField] private int presetIndex;
    [SerializeField] private bool showFlowers = true;
    [SerializeField] private Sprite[] ringSprites = new Sprite[4];
    [SerializeField] private bool previewing;

    private WrapperData[] wrappers = new WrapperData[0];

    [MenuItem("Tools/Day/Ring Preview")]
    private static void Open() => GetWindow<DayRingPreviewWindow>("Day Ring Preview");

    private void OnEnable() => LoadWrappers();

    private void LoadWrappers()
    {
        wrappers = AssetDatabase.FindAssets("t:WrapperData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<WrapperData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(w => w != null).OrderBy(w => w.wrapperId).ToArray();
        wrapperIndex = Mathf.Clamp(wrapperIndex, 0, Mathf.Max(0, wrappers.Length - 1));
        if (ringSprites == null || ringSprites.Length != RingNames.Length) ringSprites = new Sprite[RingNames.Length];
    }

    private void OnGUI()
    {
        if (wrappers.Length == 0)
        {
            EditorGUILayout.HelpBox("WrapperData가 없습니다.", MessageType.Warning);
            if (GUILayout.Button("다시 찾기")) LoadWrappers();
            return;
        }

        wrapperIndex = EditorGUILayout.Popup("포장지", wrapperIndex, wrappers.Select(w => $"{w.wrapperId}번 {w.ColorName}색 ({w.ringCount}링, 프리셋 {w.presets.Count}개)").ToArray());
        WrapperData wrapper = wrappers[wrapperIndex];
        string problem = null;
        if (wrapper.presets.Count == 0) problem = "이 포장지에는 프리셋이 없습니다.";
        else
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, wrapper.presets.Count - 1);
            presetIndex = EditorGUILayout.Popup("프리셋", presetIndex, wrapper.presets.Select((p, i) => p == null ? $"{i} (비어 있음)" : $"{i} {p.name} ({p.MaxRing}링)").ToArray());
            if (wrapper.presets[presetIndex] == null) problem = "선택한 프리셋이 비어 있습니다.";
            else if (!wrapper.presets[presetIndex].IsWellFormed) problem = "프리셋의 칸 수가 1 + 4 × 링 수 모양이 아닙니다.";
            else if (wrapper.presets[presetIndex].MaxRing > DayPuzzleController.SupportedRings) problem = $"보드가 {DayPuzzleController.SupportedRings}링까지만 지원해서 미리볼 수 없습니다.";
        }
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("링별 칸 그림 (비워 두면 씬의 그림 그대로)");
        for (int i = 0; i < RingNames.Length; i++)
            ringSprites[i] = (Sprite)EditorGUILayout.ObjectField(RingNames[i], ringSprites[i], typeof(Sprite), false);
        if (GUILayout.Button("그림 모두 비우기")) ringSprites = new Sprite[RingNames.Length];
        showFlowers = EditorGUILayout.ToggleLeft("꽃 이미지도 보기(태그별 꽃 그림)", showFlowers);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(problem != null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("적용", GUILayout.Height(28))) Apply(wrapper, wrapper.presets[presetIndex]);
        }
        using (new EditorGUI.DisabledScope(!previewing || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("초기화(씬 다시 불러오기)")) ClearPreview();
        }

        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Play 중에는 쓸 수 없습니다.", MessageType.Info);
        else if (previewing) EditorGUILayout.HelpBox("미리보기 중입니다. 이 상태로 씬을 저장하지 마세요(저장했다면 초기화 전에 되돌려야 합니다).", MessageType.Warning);
    }

    private void Apply(WrapperData wrapper, WrapperPresetData preset)
    {
        if (!OpenCleanScene()) return;

        var controller = Object.FindFirstObjectByType<DayPuzzleController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Day Ring Preview", "씬에서 DayPuzzleController를 찾지 못했습니다.", "확인");
            return;
        }

        controller.EditorPreview(wrapper, preset, ringSprites, showFlowers);
        previewing = true;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    // 이름을 Reset으로 하면 유니티가 창을 만들 때 저절로 불러 버린다(씬이 다시 열리며 에디터가 멈춘다)
    private void ClearPreview()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        previewing = false;
    }

    /// <summary>미리보기를 그릴 깨끗한 03DayPuzzle 씬을 연다. 사용자의 저장 안 한 편집을 잃게 되면 하지 않는다.</summary>
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
