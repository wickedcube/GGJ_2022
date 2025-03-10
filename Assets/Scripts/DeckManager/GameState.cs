using System.Collections;
using UnityEngine;
using Flippards;
using System.Collections.Generic;
using Flippards.Helpers;
using Sirenix.OdinInspector;
using Random = UnityEngine.Random;

public struct Result
{
    public int playerHealth;
    public int enemyHealth;
    public int turnCount;
}

public partial class GameState : MonoBehaviour
{
    private DeckManager deckManager;
    [SerializeField] public List<FullCard> playerHand;
    [SerializeField] public List<FullCard> enemyHand;

    [SerializeField] private int playerHealth;
    [SerializeField] private int playerMaxHealth;
    [SerializeField] private int enemyHealth;
    [SerializeField] private int enemyMaxHealth;

    public float PlayerHealthRatio => (float)playerHealth / playerMaxHealth;
    public float EnemyHealthRatio => (float)enemyHealth / enemyMaxHealth;

    private List<Result> resultsList = new List<Result>();

    [ShowInInspector, ReadOnly] private bool isPlayersTurn = true;
    private CardAttributes lastPlayedCard;

    private List<string> enemyList = new List<string> { "Liam", "Dualette", "Natasha", "Cyko", "Boss" };
    private List<int> enemyHealthList = new List<int> { 20, 30, 40, 50, 70 };
    private List<int> playerHealthList = new List<int> { 20, 25, 30, 35, 50 };

    [ShowInInspector, ReadOnly] private int CurrentSimulationIndex;

    private void Update()
    {
        CurrentSimulationIndex = resultsList?.Count ?? 0;
    }

    public int startingHandCount;
    public int simulationCount;
    int playerWinCount = 0;
    int enemyWinCount = 0;
    int drawCount = 0;
    int turnCount = 0;
    int newDeckCount = 0;
    public bool autoTurn = true;
    private int currentLevel;
    private WaitForSeconds waitTimeBetweenTurns = new WaitForSeconds(0.01f);

    private void Start()
    {
        currentLevel = PlayerPrefs.GetInt("CurrentLevel", 0);
        if (currentLevel >= 4)
        {
            GameMenuController.Instance.transform.Find("Menus/Win/Next").gameObject.SetActive(false);
        }
        Debug.Log("Current enemy level " + currentLevel);
        GameMenuController.Instance.OnNextClicked.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Prod");
        });
        GameMenuController.Instance.OnRestartClicked.AddListener(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Prod");
        });
        CutsceneManager.Instance.LoadCutsceneFor(enemyList[currentLevel]);
        //CutsceneManager.Instance.LoadCutsceneFor("Test");
        CutsceneManager.Instance.OnCutsceneComplete.AddListener(() =>
        {
            InitGame();
        });
    }

    [Button("Start Simulation")]
    private void StartSim()
    {
        resultsList.Clear();
        playerWinCount = 0;
        enemyWinCount = 0;
        drawCount = 0;
        InitGame();
    }

    [Button("Perform Player turn")]
    public void DebugPlayerMove()
    {
        StartCoroutine(DoPlayerTurn());
    }

    [Button("Perform Enemy turn")]
    public void DebugEnemyMove()
    {
        StartCoroutine(DoEnemyTurn());
    }

    [Button("Generate Deck and distribute initial!")]
    private void InitGame()
    {
        BattleVisualManager.Instance.InitVisuals(enemyList[currentLevel]);
        turnCount = 0;
        newDeckCount = 0;
        // isPlayersTurn = Random.Range(0, 100) % 2 == 0;
        isPlayersTurn = true;
        lastPlayedCard = null;
        deckManager = GetComponent<DeckManager>();
        deckManager.GenerateDeck();

        playerHand = new List<FullCard>();
        enemyHand = new List<FullCard>();

        playerMaxHealth = playerHealthList[currentLevel];
        enemyMaxHealth = enemyHealthList[currentLevel];
        playerHealth = playerMaxHealth;
        enemyHealth = enemyMaxHealth;

        for (int i = 0; i < 2 * startingHandCount; i++)
        {
            FullCard cardDrawn = deckManager.DrawCardFromMasterDeck();
            if (cardDrawn != null)
            {
                if (i % 2 == 0)
                {
                    playerHand.Add(cardDrawn);
                    PlayerCardHolder.Instance.AddCard(cardDrawn);
                }
                else
                {
                    enemyHand.Add(cardDrawn);
                    EnemyCardHolder.Instance.AddCard(cardDrawn);
                }
            }
            else
            {
                Debug.Log("Game ended and ran out of deck".GetRichText("red"));
                InitGame();
            }
        }

        StartNextTurn();
        if (autoTurn)
        {
            StartCoroutine(isPlayersTurn ? DoPlayerTurn() : DoEnemyTurn());
        }
        BattleVisualManager.Instance.onTurnAnimationsCompleted += StartNextTurn;
        BattleVisualManager.Instance.onFlipTargetChosen += FlipAndProceed;
    }

    public void FlipAndProceed(EntityType flippantEntity)
    {
        FlipCards(flippantEntity);
        StartNextTurn();
    }

    private void StartNextTurn()
    {
        FullCard cardDrawn = deckManager.DrawCardFromMasterDeck();

        if (cardDrawn != null)
        {
            if (isPlayersTurn)
            {
                Debug.Log($"Player took card from deck".GetRichText("orange"));
                playerHand.Add(cardDrawn);
                PlayerCardHolder.Instance.AddCard(cardDrawn);
            }
            else
            {
                Debug.Log($"Enemy took card from deck".GetRichText("orange"));
                enemyHand.Add(cardDrawn);
                EnemyCardHolder.Instance.AddCard(cardDrawn);
            }
        }
    }

    public void CheckAndTakeTurn(int index)
    {
        if (isPlayersTurn)
            StartCoroutine(DoPlayerTurn(index, false));
    }

    private IEnumerator DoPlayerTurn(int index = 0, bool random = true)
    {
        turnCount++;
        if (enemyHand.Count == 0 || playerHand.Count == 0)
        {
            CheckWinCondition();
            AddResult();
            yield break;
        }

        var cardToPlay = random ? playerHand[Random.Range(0, playerHand.Count)] : playerHand[index];

        playerHand.Remove(cardToPlay);
        DoTurn(cardToPlay, EntityType.PLAYER);

        yield return null;
        StartCoroutine(DoEnemyTurn());
    }

    private void CheckWinCondition()
    {
        if (playerHealth > enemyHealth)
        {
            currentLevel++;
            currentLevel = Mathf.Clamp(currentLevel, 0, 5);
            PlayerPrefs.SetInt("CurrentLevel", currentLevel);
            PlayerPrefs.Save();
            BattleVisualManager.Instance.ShowVictory();
        }
        else
        {
            PlayerPrefs.SetInt("CurrentLevel", 0);
            PlayerPrefs.Save();
            BattleVisualManager.Instance.ShowDefeat();
        }
    }

    private IEnumerator DoEnemyTurn()
    {
        turnCount++;
        if (enemyHand.Count == 0 || playerHand.Count == 0)
        {
            CheckWinCondition();
            AddResult();
            yield break;
        }
        yield return new WaitForSeconds(2f);

        var cardToPlay = GetAIMove();
        EnemyCardHolder.Instance.PlayCard(enemyHand.IndexOf(cardToPlay));
        enemyHand.Remove(cardToPlay);
        DoTurn(cardToPlay, EntityType.ENEMY);

        yield return null;
    }

    private void DoTurn(FullCard cardPlayed, EntityType entityType = EntityType.PLAYER)
    {
        CardAttributes cardToEval = cardPlayed.GetTopCardAttributes();
        isPlayersTurn = !isPlayersTurn;

        if (cardToEval.cardType == CardType.Flip)
        {
            if (entityType == EntityType.PLAYER)
                BattleVisuals.Instance.ShowSelectorTriggers();
            else
                FlipAndProceed(EntityType.PLAYER);

        }
        else if (entityType == EntityType.PLAYER)
        {
            if (cardToEval.cardType == CardType.Hit)
            {
                var t = GetModifiedStatValue(cardToEval);
                enemyHealth -= t;
                enemyHealth = Mathf.Clamp(enemyHealth, 0, enemyMaxHealth);

                if (enemyHealth == 0)
                {
                    AddResult();
                    currentLevel++;
                    currentLevel = Mathf.Clamp(currentLevel, 0, 5);
                    PlayerPrefs.SetInt("CurrentLevel", currentLevel);
                    PlayerPrefs.Save();
                    BattleVisualManager.Instance.ShowVictory();
                }

                //Battle visuals send health and percentage
                BattleVisualManager.Instance.DealDamage(EntityType.ENEMY, cardToEval, t);
            }
            else if (cardToEval.cardType == CardType.Heal)
            {
                var t = GetModifiedStatValue(cardToEval);
                playerHealth += t;
                playerHealth = Mathf.Clamp(playerHealth, 0, playerMaxHealth);
                BattleVisualManager.Instance.GainHealth(EntityType.PLAYER, cardToEval, t);
            }
        }
        else if (entityType == EntityType.ENEMY)
        {
            if (cardToEval.cardType == CardType.Hit)
            {
                var t = GetModifiedStatValue(cardToEval);
                playerHealth -= t;
                playerHealth = Mathf.Clamp(playerHealth, 0, playerMaxHealth);
                BattleVisualManager.Instance.DealDamage(EntityType.PLAYER, cardToEval, t);

                if (playerHealth == 0)
                {
                    AddResult();
                    PlayerPrefs.SetInt("CurrentLevel", 0);
                    PlayerPrefs.Save();
                    BattleVisualManager.Instance.ShowDefeat();
                }
            }
            else if (cardToEval.cardType == CardType.Heal)
            {
                var t = GetModifiedStatValue(cardToEval);
                enemyHealth += t;
                enemyHealth = Mathf.Clamp(enemyHealth, 0, enemyMaxHealth);
                BattleVisualManager.Instance.GainHealth(EntityType.ENEMY, cardToEval, t);
            }
        }

        lastPlayedCard = cardToEval;
        //StartNextTurn();
    }

    private void AddResult()
    {
        Result result = new Result();
        result.playerHealth = playerHealth;
        result.enemyHealth = enemyHealth;
        result.turnCount = turnCount;
        resultsList.Add(result);

        if (resultsList.Count >= simulationCount)
        {
            int turnCountAvg = 0;
            foreach (var e in resultsList)
            {
                if (e.playerHealth > e.enemyHealth)
                {
                    playerWinCount++;
                }
                else if (e.enemyHealth > e.playerHealth)
                {
                    enemyWinCount++;
                }
                else
                {
                    drawCount++;
                }

                turnCountAvg += e.turnCount;
            }

            string s = "PlayerWin " + (float)playerWinCount * 100 / simulationCount + "% " + "EnemyWin " +
                       (float)enemyWinCount * 100 / simulationCount + "% " + "Draw " +
                       (float)drawCount * 100 / simulationCount + "% + turnCount " + turnCount;
            Debug.Log(s.GetRichText("white"));
        }
        else
        {
            InitGame();
        }
    }

    private void FlipCards(EntityType entity)
    {
        BattleVisualManager.Instance.FlipCardsVisually(entity);
        List<FullCard> cardsToFlip = entity == EntityType.ENEMY ? enemyHand : playerHand;
        foreach (var t in cardsToFlip)
        {
            t.isCardFlipped = !t.isCardFlipped;
        }
        if (entity == EntityType.PLAYER)
            PlayerCardHolder.Instance.FlipCards();
        else
            EnemyCardHolder.Instance.FlipCards();
    }

    public int GetModifiedStatValue(CardAttributes playedCard)
    {
        if (lastPlayedCard == null || playedCard.cardClass == lastPlayedCard.cardClass)
        {
            return playedCard.value;
        }
        else
        {
            if (playedCard.cardClass == CardClass.Liquid && lastPlayedCard.cardClass == CardClass.Weight ||
                playedCard.cardClass == CardClass.Weight && lastPlayedCard.cardClass == CardClass.Paper ||
                playedCard.cardClass == CardClass.Paper && lastPlayedCard.cardClass == CardClass.Liquid)
            {
                if (playedCard.cardType == CardType.Hit)
                {
                    return playedCard.value * 2;
                }
                else if (playedCard.cardType == CardType.Heal)
                {
                    return playedCard.value + 1;
                }
            }
            else if (playedCard.cardClass == CardClass.Weight && lastPlayedCard.cardClass == CardClass.Liquid ||
                     playedCard.cardClass == CardClass.Paper && lastPlayedCard.cardClass == CardClass.Weight ||
                     playedCard.cardClass == CardClass.Liquid && lastPlayedCard.cardClass == CardClass.Paper)
            {
                if (playedCard.cardType == CardType.Hit)
                {
                    return playedCard.value / 2;
                }
                else if (playedCard.cardType == CardType.Heal)
                {
                    return playedCard.value - 1;
                }
            }
        }

        return 0;
    }
}