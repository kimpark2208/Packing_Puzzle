using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 이미지의 그림만 Pivot을 축으로 돌린다. 오브젝트를 돌리는 것과 달리, 자식 오브젝트(말풍선, 점멸 표시 …)와 터치 영역은 제자리에 그대로 있다.
/// 전화기가 울릴 때 전화기 그림만 흔들리게 하려고 쓴다(ReservationCall). Angle(도)을 바꾸면 그림이 다시 그려진다.
/// </summary>
public class ImageTilt : BaseMeshEffect
{
    private float angle;

    /// <summary>그림을 돌린 각도(도, 반시계 방향이 +).</summary>
    public float Angle
    {
        get => angle;
        set
        {
            if (Mathf.Approximately(angle, value)) return;

            angle = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || angle == 0f) return;

        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            vertex.position = rotation * vertex.position; // 메시는 Pivot이 원점이라 이 돌림이 곧 Pivot을 축으로 한 돌림이다
            vh.SetUIVertex(vertex, i);
        }
    }
}
