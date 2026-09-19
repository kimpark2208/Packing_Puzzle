using UnityEngine;
using UnityEngine.UI;

// 하얀 배경 Image 위에 이 컴포넌트를 얹으면, 스프라이트 없이 격자선을 벡터로 그려준다.
// GridManager와 같은 오브젝트(또는 그 자식)에 붙여서 rows/cols/cellWidth/cellHeight를 그대로 참조한다.
[RequireComponent(typeof(CanvasRenderer))]
public class GridLineRenderer : MaskableGraphic
{
    [Header("그리드 크기 (GridManager와 동일하게 맞출 것)")]
    public int rows = 5;
    public int cols = 5;

    [Header("선 스타일")]
    public float lineThickness = 2f;
    public Color lineColor = new Color(0.7f, 0.65f, 0.6f, 1f);

    [Header("테두리를 더 두깝게 하고 싶을 때")]
    public bool thickerBorder = true;
    public float borderThickness = 4f;
    public Color borderColor = new Color(0.55f, 0.5f, 0.45f, 1f);

    protected override void Awake()
    {
        base.Awake();
        // 순수 장식용 격자선이라 절대 클릭/터치를 가로채면 안 된다.
        // (모서리 칸 경계에 정확히 손가락이 닿으면 이 격자선이 대신 히트되어 드래그가 끊길 수 있었음)
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        float width = r.width;
        float height = r.height;
        float originX = r.xMin;
        float originY = r.yMin;

        float cellW = width / cols;
        float cellH = height / rows;

        // 내부 세로선 (cols - 1개)
        for (int c = 1; c < cols; c++)
        {
            float x = originX + c * cellW;
            AddVerticalLine(vh, x, originY, originY + height, lineThickness, lineColor);
        }

        // 내부 가로선 (rows - 1개)
        for (int rIdx = 1; rIdx < rows; rIdx++)
        {
            float y = originY + rIdx * cellH;
            AddHorizontalLine(vh, y, originX, originX + width, lineThickness, lineColor);
        }

        // 바깥 테두리 (더 두깝게)
        float bt = thickerBorder ? borderThickness : lineThickness;
        Color bc = thickerBorder ? borderColor : lineColor;

        AddHorizontalLine(vh, originY, originX, originX + width, bt, bc);              // 아래
        AddHorizontalLine(vh, originY + height, originX, originX + width, bt, bc);     // 위
        AddVerticalLine(vh, originX, originY, originY + height, bt, bc);               // 왼쪽
        AddVerticalLine(vh, originX + width, originY, originY + height, bt, bc);       // 오른쪽
    }

    private void AddVerticalLine(VertexHelper vh, float x, float yMin, float yMax, float thickness, Color color)
    {
        float half = thickness / 2f;
        AddQuad(vh,
            new Vector2(x - half, yMin - half),
            new Vector2(x + half, yMin - half),
            new Vector2(x + half, yMax + half),
            new Vector2(x - half, yMax + half),
            color);
    }

    private void AddHorizontalLine(VertexHelper vh, float y, float xMin, float xMax, float thickness, Color color)
    {
        float half = thickness / 2f;
        AddQuad(vh,
            new Vector2(xMin - half, y - half),
            new Vector2(xMax + half, y - half),
            new Vector2(xMax + half, y + half),
            new Vector2(xMin - half, y + half),
            color);
    }

    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int idx = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = a; vh.AddVert(v);
        v.position = b; vh.AddVert(v);
        v.position = c; vh.AddVert(v);
        v.position = d; vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
