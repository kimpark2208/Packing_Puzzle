using UnityEngine;

[System.Serializable]
public struct BlockRow
{
    public bool[] cols;
}

[CreateAssetMenu(fileName = "BlockData", menuName = "Puzzle/BlockData", order = 1)]
public class BlockData : ScriptableObject
{
    public enum Color
    {
        Red,
        Blue,
        Yellow,
        Black,
        Green,
        white,
        Purple 
    }

    [Header("블록 정보")]
    public int blockID;
    public int blockScore;
    public Color color;

    [Header("블록 모양")]
    public BlockRow[] shapeGrid;

    [Header("회전 기준점")]
    public Vector2Int anchorCoord;
    
    public GameObject blockPrefab;

    //TODO: 꽃 많아지면 모양정보랑 색상및프리펩 정보의SO분리
}
