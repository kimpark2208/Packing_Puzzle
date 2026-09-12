using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GachaManager : MonoBehaviour
{
    [SerializeField] Button PurseBTN;
    [SerializeField] List<BlockData> blockData; //TODO: 나중엔 블럭 선택화면에서 받아오게 할거임.
    [SerializeField] Transform tray;
    [SerializeField] int drawCount = 3;
    [SerializeField] int maxRerolls = 3;

    int rerollCount = 0;
    List<GameObject> curSpawned = new List<GameObject>();

    private void Start()
    {
        PurseBTN.onClick.AddListener(Gacha);
    }

    public void Gacha()
    {
        Debug.Log("gacha");
        if ((rerollCount >= maxRerolls) && (curSpawned.Count > 0))
        {
            Debug.Log("다뿑았어임마");
            return;
        }

        ClearBlocks();

        for (int i = 0; i < drawCount; i++)
        {
            BlockData picked = blockData[Random.Range(0, blockData.Count)];
            GameObject block = Instantiate(picked.blockPrefab, tray, false);

            BlockDrag draggable = block.GetComponent<BlockDrag>();
            if (draggable != null)
            {
                draggable.blockData = picked;
                // 배치 완료 시 curSpawned에서 스스로 제거하도록 콜백 등록
                draggable.OnPlaced += HandleBlockPlaced;
            }
            else
            {
                Debug.LogWarning($"{picked.name}의 blockPrefab에 BlockDrag 컴포넌트가 없습니다.");
            }

            curSpawned.Add(block);
        }

        Debug.Log(++rerollCount + "/" + maxRerolls);
    }

    // 그리드에 배치가 완료된 블록은 더 이상 "트레이의 리롤 대상"이 아니므로 목록에서 제거
    private void HandleBlockPlaced(GameObject block)
    {
        curSpawned.Remove(block);
    }

    private void ClearBlocks()
    {
        foreach (var block in curSpawned)
        {
            if (block != null) Destroy(block);
        }
        curSpawned.Clear();
    }

    public void ResetRerollCount()
    {
        rerollCount = 0;
    }
}
