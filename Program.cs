using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

class Player
{
    static void Main(string[] args)
    {
        Game game = new Game();
        while (true)
        {
            game.Read();
            game.Execute();
        }
    }
}

public class Game 
{
    private EPhase phase = default;
    private IList<Application> apps = default;

    public EPhase Phase => phase;
    public IEnumerable<Application> Apps => Apps;
    public GamePlayer Player { get; private set; }
    public GameOpponent Opponent { get; private set; }

    public enum EPhase 
    {
        MOVE,
        GIVE_CARD,
        THROW_CARD,
        PLAY_CARD,
        RELEASE,
    }

    public Game()
    {
        Player = new GamePlayer();
        Opponent = new GameOpponent();
        apps = new List<Application>();
    }

    public void Read()
    {
        ReadPhase();
        ReadApplications();
        ReadPlayers();
        ReadCards();
    }

    public void Execute()
    {
        int possibleMovesCount = int.Parse(Console.ReadLine());
        for (int i = 0; i < possibleMovesCount; i++)
        {
            string possibleMove = Console.ReadLine();
            Console.Error.WriteLine("POSSIBLE_MOVE: ", possibleMove);
        }

        // Write an action using Console.WriteLine()
        // To debug: Console.Error.WriteLine("Debug messages...");
        // In the first league: RANDOM | MOVE <zoneId> | RELEASE <applicationId> | WAIT; In later leagues: | GIVE <cardType> | THROW <cardType> | TRAINING | CODING | DAILY_ROUTINE | TASK_PRIORITIZATION <cardTypeToThrow> <cardTypeToTake> | ARCHITECTURE_STUDY | CONTINUOUS_DELIVERY <cardTypeToAutomate> | CODE_REVIEW | REFACTORING;
        Console.WriteLine("RANDOM");
    }

    private void ReadPhase()
    {
        string _raw = Console.ReadLine();
        this.phase = Enum.Parse<EPhase>(_raw);
    }

    private void ReadApplications()
    {
        apps.Clear();
        int numOfApps = int.Parse(Console.ReadLine());
        for (int i = 0; i < numOfApps; i++)
        {
            string[] inputs = Console.ReadLine().Split(' ');
            Application app = new Application 
            {
                Type = inputs[0],
                Id = int.Parse(inputs[1]),
                Costs = new Cost[]
                {
                    new Cost(Card.ECard.TRAINING, inputs[2]),
                    new Cost(Card.ECard.CODING, inputs[3]),
                    new Cost(Card.ECard.DAILY_ROUTINE, inputs[4]),
                    new Cost(Card.ECard.TASK_PRIORITIZATION, inputs[5]),
                    new Cost(Card.ECard.ARCHITECTURE_STUDY, inputs[6]),
                    new Cost(Card.ECard.CONTINUOUS_DELIVERY, inputs[7]),
                    new Cost(Card.ECard.CODE_REVIEW, inputs[8]),
                    new Cost(Card.ECard.REFACTORING, inputs[9]),
                }
            };
            apps.Add(app);
        }
    }

    private void ReadPlayers()
    {
        IPlayer[] players = new IPlayer[] { Player, Opponent };
        for (int i = 0; i < players.Length; i++)
        {
            var inputs = Console.ReadLine().Split(' ');
            players[i].Desk = (EDesk)int.Parse(inputs[0]);
            players[i].Score = int.Parse(inputs[1]);
            players[i].PlayerPermanentDailyRoutineCards = int.Parse(inputs[2]);
            players[i].PlayerPermanentArchitectureStudyCards = int.Parse(inputs[3]);
        }
    }

    private void ReadCards()
    {
        Player.Hand.Cards.Clear();
        Opponent.Cards.Clear();

        int cardLocationsCount = int.Parse(Console.ReadLine());
        for (int i = 0; i < cardLocationsCount; i++)
        {
            var inputs = Console.ReadLine().Split(' ');
            string cardsLocation = inputs[0];

            List<Card> cards = new List<Card>();
            cards.AddRange(Card.Parse(Card.ECard.TRAINING, inputs[1]));
            cards.AddRange(Card.Parse(Card.ECard.CODING, inputs[2]));
            cards.AddRange(Card.Parse(Card.ECard.DAILY_ROUTINE, inputs[3]));
            cards.AddRange(Card.Parse(Card.ECard.TASK_PRIORITIZATION, inputs[4]));
            cards.AddRange(Card.Parse(Card.ECard.ARCHITECTURE_STUDY, inputs[5]));
            cards.AddRange(Card.Parse(Card.ECard.CONTINUOUS_DELIVERY, inputs[6]));
            cards.AddRange(Card.Parse(Card.ECard.CODE_REVIEW, inputs[7]));
            cards.AddRange(Card.Parse(Card.ECard.REFACTORING, inputs[8]));
            cards.AddRange(Card.Parse(Card.ECard.BONUS, inputs[9]));
            cards.AddRange(Card.Parse(Card.ECard.TECHNICAL_DEBT, inputs[10]));

            switch (cardsLocation)
            {
                case "HAND":
                    Player.Hand.Cards.AddRange(new Card[0]);
                    break;
                case "DRAW":
                    var draw = new Card();
                    Player.Hand.Cards.Add(draw);
                    Player.Hand.LastDraw = draw;
                    break;
                case "DISCARD":
                    Player.Discard = new Card[0];
                    break;
                case "OPPONENT_CARDS":
                    Opponent.Cards.AddRange(new Card[0]);
                    break;
                case "AUTOMATED":
                    Player.Automated = new Card[0];
                    break;
                case "OPPONENT_AUTOMATED":
                    Opponent.Automated = new Card[0];
                    break;
                default:
                    Console.Error.WriteLine("Received unknown card location");
                    break;
            }
        }
    }    
}

public interface IPlayer
{
    List<Card> Cards { get; }
    IEnumerable<Card> Automated { get; }
    int PlayerPermanentDailyRoutineCards { get; set; }
    int PlayerPermanentArchitectureStudyCards { get; set; }
    EDesk Desk { get; set; }
    int Score { get; set; }
}

public class GamePlayer : IPlayer
{
    public Hand Hand { get; set; }
    public IEnumerable<Card> Discard { get; set; }    
    public IEnumerable<Card> Automated { get; set; }
    public int PlayerPermanentDailyRoutineCards { get; set; }
    public int PlayerPermanentArchitectureStudyCards { get; set; }
    public EDesk Desk { get; set; }
    public int Score { get; set; }

    public List<Card> Cards => Hand.Cards.Concat(Discard).ToList();

    public GamePlayer()
    {
        this.Hand = new Hand();
        this.Discard = new Card[0];
        this.Automated = new Card[0];
        this.Desk = (EDesk)(-1);
        this.Score = 0;
        this.PlayerPermanentDailyRoutineCards = 0;
        this.PlayerPermanentArchitectureStudyCards = 0;
    }
}

public class GameOpponent : IPlayer
{
    public List<Card> Cards { get; set; }
    public IEnumerable<Card> Automated { get; set; }
    public int PlayerPermanentDailyRoutineCards { get; set; }
    public int PlayerPermanentArchitectureStudyCards { get; set; }
    public EDesk Desk { get; set; }
    public int Score { get; set; }

    public GameOpponent()
    {
        this.Cards = new List<Card>();
        this.Automated = new Card[0];
        this.Desk = (EDesk)(-1);
        this.Score = 0;
        this.PlayerPermanentDailyRoutineCards = 0;
        this.PlayerPermanentArchitectureStudyCards = 0;
    }
}

public class Hand
{
    public List<Card> Cards { get; set; } = new List<Card>();
    public Card? LastDraw { get; set; } = null;
}

public struct Card
{
    public ECard Desk { get; set; }

    public enum ECard
    {
        TRAINING = 0,
        CODING = 1,
        DAILY_ROUTINE = 2,
        TASK_PRIORITIZATION = 3,
        ARCHITECTURE_STUDY = 4,
        CONTINUOUS_DELIVERY = 5,
        CODE_REVIEW = 6,
        REFACTORING = 7,
        BONUS = 8,
        TECHNICAL_DEBT = 9,
    }

    public static IEnumerable<Card> Parse(ECard _desk, string _count)
    {
        var cards = new List<Card>();
        for (int i = 0; i < int.Parse(_count); i++)
        {
            cards.Add(new Card { Desk = _desk });
        }
        return cards;
    }
}

public struct Application
{
    public string Type { get; set; }
    public int Id { get; set; }
    public Cost[] Costs { get; set; }
}

public struct Cost
{
    public Card.ECard Desk { get; set; }
    public int Value { get; set; }

    public Cost(Card.ECard _desk, string _raw)
    {
        this.Desk = _desk;
        this.Value = int.Parse(_raw);
    }
}

public enum EDesk
{
    NONE = -1,
    TRAINING = 0,
    CODING = 1,
    DAILY_ROUTINE = 2,
    TASK_PRIORITIZATION = 3,
    ARCHITECTURE_STUDY = 4,
    CONTINUOUS_DELIVERY = 5,
    CODE_REVIEW = 6,
    REFACTORING = 7,
}