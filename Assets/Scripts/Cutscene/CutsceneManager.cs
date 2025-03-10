using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CutsceneManager : MonoBehaviour
{

    private static CutsceneManager instance;
    public static CutsceneManager Instance
    {
        get {
            if (instance == null)
            {
                instance = FindObjectOfType<CutsceneManager>();
            }
            return instance; 
        }
    }

    [SerializeField] DialogueWindow dialogueWindow;
    [SerializeField] Image cutSceneBG;
    [SerializeField] bool isIntro = false;
    [SerializeField] Animator animator;
    
    public BattleAnimEventComplete OnCutsceneComplete;
    bool open = false;

    string currCharName;

    public bool CutSceneActive = false;

    public void SetImage(Sprite img)
    {
        cutSceneBG.gameObject.SetActive(false);
        if (img == null)
            return;

        cutSceneBG.gameObject.SetActive(true);
        cutSceneBG.sprite = img;
    }

    public void LoadCutsceneFor(string charName)
    {
        CutSceneActive = true;
        this.currCharName = charName;
        open = true;
        SwipeCut(0.5f);
    }

    public void Skip()
    {
        dialogueWindow.CloseDialogue();
    }

    public void OnSwipeCut()
    {
        if (open)
        {
            dialogueWindow.StartDialogue(currCharName, () =>
            {
                open = false;
                SwipeCut(0);
            }, 0.65f);
        }
        else
        {
            CloseCutscene();
        }
    }

    void CloseCutscene()
    {
        CutSceneActive = false;
        cutSceneBG.gameObject.SetActive(false);
        OnCutsceneComplete?.Invoke();
    }

    private void Start()
    {
        if (isIntro)
        {
            LoadCutsceneFor("Game_Intro");
            OnCutsceneComplete.AddListener(() =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            });
        }
    }

    void SwipeCut(float jumpTo = 0)
    {
        AudioManager.Instance.PlayWhooshSFX();
        animator.Play("SwipeCut",0,jumpTo);
    }
}
