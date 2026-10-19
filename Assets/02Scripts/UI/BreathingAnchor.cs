using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 숨쉬기 셰이더(UI/Breathing)를 쓰는 Image에 붙인다. 앵커(RectTransform의 Pivot)에서 정점까지의 거리를 정점마다 TEXCOORD1에 넣어 준다.
/// UI 정점은 캔버스 공간으로 한데 모여서 셰이더가 오브젝트의 Pivot을 알 수 없기 때문이다. 오브젝트가 움직여도 거리는 그대로라서 다시 만들 필요가 없다(크기가 바뀌어 메시가 다시 만들어질 때 새로 넣는다).
/// 캔버스가 TEXCOORD1을 셰이더로 넘기도록 Additional Shader Channels에 TexCoord1을 켜는 것도 실행할 때 여기서 한다(에디터의 캔버스 값은 바꾸지 않는다).
/// </summary>
[RequireComponent(typeof(Graphic))]
public class BreathingAnchor : BaseMeshEffect
{
    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Application.isPlaying || graphic == null || graphic.canvas == null) return;

        graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        graphic.canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        Transform canvasSpace = graphic.canvas != null ? graphic.canvas.transform : null; // 셰이더의 정점은 캔버스 안쪽 좌표라서, 거리도 그 단위로 넣는다
        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            Vector3 offset = transform.TransformVector(vertex.position); // 메시는 Pivot이 원점이라 정점 위치가 곧 앵커에서의 거리다
            if (canvasSpace != null) offset = canvasSpace.InverseTransformVector(offset);
            vertex.uv1 = new Vector4(offset.x, offset.y, 0f, 0f);
            vh.SetUIVertex(vertex, i);
        }
    }
}
