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
    private IPhase phase = default;
    private IList<Application> apps = default;
    private IList<Action> actions = default;
    private InputReader reader = default;

    public IPhase Phase => phase;
    public IEnumerable<Application> Apps => Apps;
    public IEnumerable<Action> Actions => actions;
    public GamePlayer Player { get; private set; }
    public GameOpponent Opponent { get; private set; }

    public Game()
    {
        Player = new GamePlayer();
        Opponent = new GameOpponent();
        apps = new List<Application>();
        actions = new List<Action>();
        reader = new InputReader(new IPhase[]
        {
            new MovePhase(),
            new ReleasePhase(),
        });
    }

    public void Read()
    {
        reader.ReadPhase(ref phase);
        reader.ReadApplications(ref apps);
        reader.ReadPlayers(Player, Opponent);
        reader.ReadCards(Player, Opponent);
        reader.ReadActions(ref actions);
    }

    public void Execute()
    {
        phase.Run(this);
    }
}

public class MovePhase : PhaseBase
{
    public override EPhase Name => EPhase.MOVE;

    public override void Run(Game _game)
    {
        
        Random();
    }

    public void Move(EDesk _desk)
    {
        Terminal.Command($"MOVE {(int)_desk}");
    }
}

public class ReleasePhase : OptionalPhase
{
    public override EPhase Name => EPhase.RELEASE;

    public override void Run(Game _game)
    {
        Random();
    }

    public void Release(Application _app)
    {
        Terminal.Command($"RELEASE {_app.Id}");
    }
}

public abstract class OptionalPhase : PhaseBase
{
    public void Wait()
    {
        Terminal.Command("WAIT waiting...");
    }
}

public abstract class PhaseBase : IPhase
{
    public abstract EPhase Name { get; }

    public abstract void Run(Game _game);

    public void Random()
    {
        Terminal.Command("RANDOM idk what to do...");
    }
}

public enum EPhase
{
    MOVE,
    GIVE_CARD,
    THROW_CARD,
    PLAY_CARD,
    RELEASE,
}

public interface IPhase
{
    EPhase Name { get; }
    void Run(Game _game);
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

    public bool CanRelease(string[] _possibleActions)
    {
        return _possibleActions.Contains($"RELEASE {Id}");
    }
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

public class InputReader
{
    string[]? inputs = default;
    IEnumerable<IPhase> phases = default;

    public InputReader(IEnumerable<IPhase> _phases)
    {
        this.phases = _phases;
    }

    public void ReadPhase(ref IPhase _phase)
    {
        string raw = Terminal.Read();
        if (!Enum.TryParse(raw, out EPhase p))
        {
            throw new System.Exception($"Unable to parse phase {raw}");
        }

        var phase = phases.FirstOrDefault(x => x.Name == p);
        if (phase is null)
        {
            throw new System.Exception($"No available phases for {raw} was found");
        }

        _phase = phase;
    }

    public void ReadApplications(ref IList<Application> _apps)
    {
        _apps.Clear();
        int numOfApps = int.Parse(Terminal.Read());
        for (int i = 0; i < numOfApps; i++)
        {
            inputs = Terminal.Read().Split(' ');
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
            _apps.Add(app);
        }
    }

    public void ReadPlayers(params IPlayer[] _players)
    {
        for (int i = 0; i < _players.Length; i++)
        {
            inputs = Terminal.Read().Split(' ');
            _players[i].Desk = (EDesk)int.Parse(inputs[0]);
            _players[i].Score = int.Parse(inputs[1]);
            _players[i].PlayerPermanentDailyRoutineCards = int.Parse(inputs[2]);
            _players[i].PlayerPermanentArchitectureStudyCards = int.Parse(inputs[3]);
        }
    }

    public void ReadCards(GamePlayer _player, GameOpponent _opponent)
    {
        _player.Hand.Cards.Clear();
        _opponent.Cards.Clear();

        int cardLocationsCount = int.Parse(Terminal.Read());
        for (int i = 0; i < cardLocationsCount; i++)
        {
            inputs = Terminal.Read().Split(' ');
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
                    _player.Hand.Cards.AddRange(new Card[0]);
                    break;
                case "DRAW":
                    var draw = new Card();
                    _player.Hand.Cards.Add(draw);
                    _player.Hand.LastDraw = draw;
                    break;
                case "DISCARD":
                    _player.Discard = new Card[0];
                    break;
                case "OPPONENT_CARDS":
                    _opponent.Cards.AddRange(new Card[0]);
                    break;
                case "AUTOMATED":
                    _player.Automated = new Card[0];
                    break;
                case "OPPONENT_AUTOMATED":
                    _opponent.Automated = new Card[0];
                    break;
                default:
                    Terminal.Log("Received unknown card location");
                    break;
            }
        }
    }

    public void ReadActions(ref IList<Action> _actions)
    {
        _actions.Clear();
        int numOfPossibleActions = int.Parse(Terminal.Read());
        for (int i = 0; i < numOfPossibleActions; i++)
        {
            var action = new Action { Value = Terminal.Read() };
            _actions.Add(action);
            Terminal.Log($"POSSIBLE_MOVE: {action.Value}");
        }
    }
}

public struct Action
{

    // In the first league: RANDOM | MOVE <zoneId> | RELEASE <applicationId> | WAIT;
    // In later leagues: | GIVE <cardType> | THROW <cardType> | TRAINING | CODING | DAILY_ROUTINE | TASK_PRIORITIZATION <cardTypeToThrow> <cardTypeToTake> | ARCHITECTURE_STUDY | CONTINUOUS_DELIVERY <cardTypeToAutomate> | CODE_REVIEW | REFACTORING;
    public string Value { get; set; }
}

public class Terminal
{
    public static void Log(string _message)
    {
        Console.Error.WriteLine(_message);
    }

    public static void Command(string _message)
    {
        Console.WriteLine(_message);
    }

    public static string Read()
    {
        string? input = Console.ReadLine();
        if (input == null) { return string.Empty; }
        return input;
    }
}