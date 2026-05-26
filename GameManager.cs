using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // �V�[���J�ڂȂǂŎg�p
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private List<PointData> list;
    AudioSource[] audioSource;
    // Singleton�p�^�[��
    public static GameManager Instance { get; private set; }
    public static bool Clear;
    [SerializeField ]private Text pickedItems;

    [Header("�Q�[���ݒ�")]
    [SerializeField] private int requiredItemsToWin = 6; // �N���A�ɕK�v�ȃA�C�e����
    private int collectedItems = 0; // ���W�����A�C�e���̌��ݐ�

    void Start()
    {
        TouchGameOver.Caught = false;
        GameManager.Clear = false;
        audioSource = GetComponents<AudioSource>();
        audioSource[1].loop = true;
        audioSource[1].Play();
        if (Instance == null)
        {
            Instance = this;
            // �V�[�����ׂ��ŕێ��������ꍇ�� DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

     void Update()
    {
        list = new List<PointData>();
        SensorReceiver.Instance.getPointList(ref list);
        //Debug.Log(list);
        for(int i = 0;i < list.Count;i++)
        {
            PointData p = list[i];
            float x = p.position.x;
            float y = p.position.y;
            Vector2 v = p.position;
            // Debug.Log("x:" + x + " y:" + y);
        }

    }

    // �A�C�e�����W���ɌĂяo����郁�\�b�h
    public void CollectItem()
    {
        collectedItems += 1;
        Debug.Log("�A�C�e�����E���܂����B���݂̎��W��: " + collectedItems + " / " + requiredItemsToWin);
        audioSource[0].Play();
        // �N���A�����̃`�F�b�N
        if (collectedItems >= requiredItemsToWin)
        {
            GameClear();
        }
        pickedItems.text = collectedItems.ToString() + "/6";
        pickedItems.gameObject.SetActive(true);
    }

    // �Q�[���N���A���̏���
    private void GameClear()
    {
        Debug.Log("�Q�[���N���A�I");
        Clear = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
        // ���̑��̃N���A�����i���Ԓ�~�A���͖������Ȃǁj
        // Time.timeScale = 0f; 
    }

    // ���W���̕\���i�f�o�b�O�p�܂���UI�X�V�p�j
    public int GetCollectedItemsCount()
    {
        return collectedItems;
    }
}