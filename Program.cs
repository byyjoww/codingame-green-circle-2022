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
        LogPhaseInfo();
        phase.Run(this);
    }

    private void LogPhaseInfo()
    {
        Terminal.Log($"Phase: {Phase.Name}");
        Terminal.LogArray($"Available Actions:", Actions.Select(x => x.Value));
        Terminal.Log($"Player Info (Score: {Player.Score})");
        Terminal.LogArray($"- Hand:", Player.Hand.Select(x => x.Name.ToString()));
        Terminal.LogArray($"- Deck:", Player.Deck.Select(x => x.Name.ToString()));
        Terminal.LogArray($"- Discard:", Player.Discard.Select(x => x.Name.ToString()));
        Terminal.Log($"Opponent Info (Score: {Opponent.Score})");
        Terminal.LogArray($"- Cards:", Opponent.Cards.Select(x => x.Name.ToString()));
        Terminal.Log($"Applications:");
        foreach (var app in apps)
        {
            Terminal.LogArray($"- {app.Type} {app.Id}", app.Costs.Select(x => $"x{x.Value} {x.Desk}"));
        }
    }
}

public class MovePhase : PhaseBase
{
    public override EPhase Name => EPhase.MOVE;

    public override void Run(Game _game)
    {
        if (!TryFilterForPursuableApps(_game, out IEnumerable<Application> _pursuable))
        {
            // no available desks with any needed resources
            Random();
            return;
        }

        var bestAppToPursue = GetBestApplicationToPursue(_pursuable);
        var bestDesk = GetBestDeskToMove(bestAppToPursue);
        Move(bestDesk);
    }

    private bool TryFilterForPursuableApps(Game _game, out IEnumerable<Application> _pursuable)
    {
        _pursuable = _game.Apps.Where(x => !x.CanRelease(_game.Actions)); // if we can already release that app, we dont need to pursue it (TODO: check if we can improve the release by using good skills instead of shoddy skills)
        return _pursuable.Count() > 0;
    }

    private Application GetBestApplicationToPursue(IEnumerable<Application> _pursuable)
    {
        return _pursuable.First();
    }

    private EDesk GetBestDeskToMove(Application _bestApplicationToPursue)
    {
        return _bestApplicationToPursue.Costs.First().Desk;
    }

    protected void Move(EDesk _desk)
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

    protected void Release(Application _app)
    {
        Terminal.Command($"RELEASE {_app.Id}");
    }
}

public abstract class OptionalPhase : PhaseBase
{
    protected void Wait()
    {
        Terminal.Command("WAIT waiting...");
    }
}

public abstract class PhaseBase : IPhase
{
    public abstract EPhase Name { get; }

    public abstract void Run(Game _game);

    protected void Random()
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
    IEnumerable<Card> Cards { get; }
    IEnumerable<Card> Automated { get; }
    int PlayerPermanentDailyRoutineCards { get; set; }
    int PlayerPermanentArchitectureStudyCards { get; set; }
    EDesk Desk { get; set; }
    int Score { get; set; }
}

public class GamePlayer : IPlayer
{
    public IEnumerable<Card> Hand { get; set; }
    public IEnumerable<Card> Deck { get; set; }
    public IEnumerable<Card> Discard { get; set; }    
    public IEnumerable<Card> Automated { get; set; }
    public int PlayerPermanentDailyRoutineCards { get; set; }
    public int PlayerPermanentArchitectureStudyCards { get; set; }
    public EDesk Desk { get; set; }
    public int Score { get; set; }

    public IEnumerable<Card> Cards => Hand.Concat(Discard).Concat(Deck).ToList();

    public GamePlayer()
    {
        this.Hand = new Card[0];
        this.Deck = new Card[0];
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
    public IEnumerable<Card> Cards { get; set; }
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

public struct Card
{
    public ECard Name { get; set; }

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
            cards.Add(new Card { Name = _desk });
        }
        return cards;
    }
}

public struct Application
{
    public string Type { get; set; }
    public int Id { get; set; }
    public IEnumerable<Cost> Costs { get; set; }

    public bool CanRelease(IEnumerable<Action> _possibleActions)
    {
        return _possibleActions.Select(x => x.Value).Contains($"RELEASE {Id}");
    }
}

public struct Cost
{
    public EDesk Desk { get; set; }
    public int Value { get; set; }

    public static IEnumerable<Cost> Parse(EDesk _desk, string _count)
    {
        int costNum = int.Parse(_count);
        if (costNum <= 0) { return new Cost[0]; }
        return new Cost[] { new Cost { Desk = _desk, Value = costNum } };
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
            List<Cost> costs = new List<Cost>();
            costs.AddRange(Cost.Parse(EDesk.TRAINING, inputs[2]));
            costs.AddRange(Cost.Parse(EDesk.CODING, inputs[3]));
            costs.AddRange(Cost.Parse(EDesk.DAILY_ROUTINE, inputs[4]));
            costs.AddRange(Cost.Parse(EDesk.TASK_PRIORITIZATION, inputs[5]));
            costs.AddRange(Cost.Parse(EDesk.ARCHITECTURE_STUDY, inputs[6]));
            costs.AddRange(Cost.Parse(EDesk.CONTINUOUS_DELIVERY, inputs[7]));
            costs.AddRange(Cost.Parse(EDesk.CODE_REVIEW, inputs[8]));
            costs.AddRange(Cost.Parse(EDesk.REFACTORING, inputs[9]));

            Application app = new Application
            {
                Type = inputs[0],
                Id = int.Parse(inputs[1]),
                Costs = costs,
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
                    _player.Hand = cards;
                    break;
                case "DRAW":
                    _player.Deck = cards;
                    break;
                case "DISCARD":
                    _player.Discard = cards;
                    break;
                case "OPPONENT_CARDS":
                    _opponent.Cards = cards;
                    break;
                case "AUTOMATED":
                    _player.Automated = cards;
                    break;
                case "OPPONENT_AUTOMATED":
                    _opponent.Automated = cards;
                    break;
                default:
                    throw new System.Exception($"Received unknown card location: {cardsLocation}");
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
            // Terminal.Log($"POSSIBLE_MOVE: {action.Value}");
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

    public static void LogArray(string _message, IEnumerable<object> _array)
    {
        Log($"{_message}: [{string.Join(", ", _array)}]");
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