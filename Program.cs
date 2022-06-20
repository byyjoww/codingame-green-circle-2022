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
    public IEnumerable<Application> Apps => apps;
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
        // LogPhaseInfo();
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
        var totalMana = _game.Player.Hand.SelectMany(x => x.GetProvidedMana());
        if (!TryGetPursuableApps(_game, totalMana, out Dictionary <Application, IEnumerable<Cost>> _pursuable))
        {
            Terminal.Log($"No pursuable applications");
            Random(_game);
            return;
        }

        var bestAppToPursue = GetBestApplicationToPursue(_pursuable);
        EDesk bestDesk = GetBestDeskToMove(_game, bestAppToPursue);
        LogDecision(_pursuable, bestAppToPursue, bestDesk);
        Move(_game, bestDesk);
    }

    private bool TryGetPursuableApps(Game _game, IEnumerable<IMana> _mana, out Dictionary<Application, IEnumerable<Cost>> _pursuable)
    {
        // If we can already release that app, we dont need to pursue it
        // TODO: check if we can improve the release by using good skills instead of shoddy skills
        var apps = _game.Apps
            .Where(x => !x.CanRelease(_game.Actions))
            .Where(x => x.Costs.Any(x => IsAvailable(_game, x.Desk)))
            .ToDictionary(x => x, y => y.Costs);

        _pursuable = apps
            .ToDictionary(x => x.Key, y => ExcludeAvailableMana(y, _mana))
            .Where(x => x.Value.Count() > 0)
            .Where(x => x.Value.Any(x => IsAvailable(_game, x.Desk)))
            .ToDictionary(x => x.Key, y => y.Value);

        LogPursuableCalculation(_game, apps, _pursuable);
        return _pursuable.Count() > 0;
    }

    private IEnumerable<Cost> ExcludeAvailableMana(KeyValuePair<Application, IEnumerable<Cost>> _total, IEnumerable<IMana> _mana)
    {
        Dictionary<EMana, int> specificMana = new Dictionary<EMana, int>();
        void AddToDictionary(IMana _mana)
        {
            if (_mana.Value > 0)
            {
                if (specificMana.ContainsKey(_mana.Name)) { specificMana[_mana.Name] += _mana.Value; }
                else { specificMana.Add(_mana.Name, _mana.Value); }
            }
        }
        _mana.Where(x => x is Mana).ToList().ForEach(x => AddToDictionary(x));
        int wildcardMana = _mana.Where(x => x is WildcardMana).Sum(x => x.Value);        
        int shoddyMana = _mana.Where(x => x is ShoddyMana).Sum(x => x.Value);

        var loggableSpecificMana = new Dictionary<EMana, int>(specificMana);
        int loggableWildcardMana = wildcardMana;
        int loggableShoddyMana = shoddyMana;

        // Deduct specific mana first
        var newCostsMinusSpecific = new List<Cost>();
        foreach (var cost in _total.Value)
        {
            var m = Enum.Parse<EMana>(cost.Desk.ToString());
            if (specificMana.ContainsKey(m))
            {
                int toRemove = Math.Min(specificMana[m], cost.Value);
                int newCost = cost.Value - toRemove;
                if (newCost > 0) 
                {
                    newCostsMinusSpecific.Add(new Cost 
                    {
                        Desk = cost.Desk,
                        Value = newCost,
                    });
                }

                specificMana[m] -= toRemove;
                if (specificMana[m] <= 0)
                {
                    specificMana.Remove(m);
                }
            }
            else
            {
                newCostsMinusSpecific.Add(new Cost
                {
                    Desk = cost.Desk,
                    Value = cost.Value,
                });
            }
        }

        // Deduct wildcard mana next
        var newCostsMinusSpecificMinusWildcard = new List<Cost>();  
        foreach (var cost in newCostsMinusSpecific)
        {
            int toRemove = Math.Min(wildcardMana, cost.Value);
            int newCost = cost.Value - toRemove;
            if (newCost > 0)
            {
                newCostsMinusSpecificMinusWildcard.Add(new Cost
                {
                    Desk = cost.Desk,
                    Value = newCost,
                });
            }
            wildcardMana -= toRemove;
        }

        // Deduct shoddy mana next
        var newCostsMinusSpecificMinusWildcardMinusShoddy = new List<Cost>();
        foreach (var cost in newCostsMinusSpecificMinusWildcard)
        {
            int toRemove = Math.Min(shoddyMana, cost.Value);
            int newCost = cost.Value - toRemove;
            if (newCost > 0)
            {
                newCostsMinusSpecificMinusWildcardMinusShoddy.Add(new Cost
                {
                    Desk = cost.Desk,
                    Value = newCost,
                });
            }
            shoddyMana -= toRemove;
        }

        // LogManaCalculation(_total, loggableSpecificMana, loggableWildcardMana, loggableShoddyMana, newCostsMinusSpecific, newCostsMinusSpecificMinusWildcard, newCostsMinusSpecificMinusWildcardMinusShoddy);
        return newCostsMinusSpecificMinusWildcardMinusShoddy;
    }    

    private KeyValuePair<Application, IEnumerable<Cost>> GetBestApplicationToPursue(Dictionary<Application, IEnumerable<Cost>> _pursuable)
    {
        return _pursuable.First();
    }

    private EDesk GetBestDeskToMove(Game _game, KeyValuePair<Application, IEnumerable<Cost>> _bestApplicationToPursue)
    {
        return _bestApplicationToPursue.Value
            .Select(x => x.Desk)
            .Where(x => IsAvailable(_game, x))
            .First();
    }

    private bool IsAvailable(Game _game, EDesk _desk)
    {
        IPlayer[] players = new IPlayer[] { _game.Player, _game.Opponent };
        foreach (var player in players)
        {
            if (player.Desk == _desk) { return false; }
        }
        return true;
    }

    protected void Move(Game _game, EDesk _desk)
    {
        if (!_game.Actions.Any(x => x.Value == $"MOVE {(int)_desk}")) 
        {
            Terminal.LogArray($"Available Actions:", _game.Actions.Select(x => x.Value));
            throw new System.Exception($"can't move to desk {_desk}"); 
        }
        Terminal.Log($"Moving to desk {_desk}");
        Terminal.Command($"MOVE {(int)_desk}");
    }

    private void LogDecision(Dictionary<Application, IEnumerable<Cost>> _pursuable, KeyValuePair<Application, IEnumerable<Cost>> _bestApplicationToPursue, EDesk _bestDesk)
    {
        Terminal.LogArray($"Pursuable Applications:", _pursuable.Select(x => x.Key.Id.ToString()));
        Terminal.Log($"Best Application: {_bestApplicationToPursue.Key.Id.ToString()}");
        Terminal.Log($"Best Desk: {_bestDesk.ToString()}");
    }

    private void LogPursuableCalculation(Game _game, params Dictionary<Application, IEnumerable<Cost>>[] _apps)
    {
        Terminal.LogArray("Hand:", _game.Player.Hand.Select(x => x.Name.ToString()));
        foreach (var dict in _apps)
        {
            Terminal.Log($"Total Needed:");
            foreach (var app in dict)
            {
                Terminal.LogArray($"- {app.Key.Type} {app.Key.Id}", app.Value.Select(x => $"x{x.Value} {x.Desk}"));
            }
        }
    }

    private void LogManaCalculation(KeyValuePair<Application, IEnumerable<Cost>> _total, Dictionary<EMana, int> _specificMana, int _wildcardMana, int _shoddyMana, List<Cost> _newCostsMinusSpecific, List<Cost> _newCostsMinusSpecificMinusWildcard, List<Cost> _newCostsMinusSpecificMinusWildcardMinusShoddy)
    {
        Terminal.Log($"{_total.Key.Type}: {_total.Key.Id}");
        Terminal.LogArray("Specific Mana:", _specificMana.Select(x => $"x{x.Value} {x.Key}"));
        Terminal.Log($"Wildcard Mana: {_wildcardMana}");
        Terminal.Log($"Shoddy Mana: {_shoddyMana}");
        Terminal.LogArray("Cost Total:", _total.Value.Select(x => $"x{x.Value} {x.Desk}"));
        Terminal.LogArray("Cost Minus Specific:", _newCostsMinusSpecific.Select(x => $"x{x.Value} {x.Desk}"));
        Terminal.LogArray("Cost Minus Wildcard:", _newCostsMinusSpecificMinusWildcard.Select(x => $"x{x.Value} {x.Desk}"));
        Terminal.LogArray("Cost Minus Shoddy:", _newCostsMinusSpecificMinusWildcardMinusShoddy.Select(x => $"x{x.Value} {x.Desk}"));
    }
}

public class ReleasePhase : OptionalPhase
{
    public override EPhase Name => EPhase.RELEASE;

    public override void Run(Game _game)
    {
        if (!TryGetBestApplicationToRelease(_game, out Application? _app))
        {
            Terminal.Log($"No releasable applications");
            Random(_game);
            return;
        }

        Release(_game, _app.Value);
    }

    private bool TryGetBestApplicationToRelease(Game _game, out Application? _app)
    {
        _app = _game.Apps.FirstOrDefault(x => x.CanRelease(_game.Actions));
        return _app.HasValue;
    }

    protected void Release(Game _game, Application _app)
    {
        if (!_app.CanRelease(_game.Actions)) 
        {
            Terminal.LogArray($"Available Actions:", _game.Actions.Select(x => x.Value));
            throw new System.Exception($"can't release app {_app.Id}"); 
        }
        Terminal.Log($"Releasing app {_app.Id}");
        Terminal.Command($"RELEASE {_app.Id}");
    }
}

public abstract class OptionalPhase : PhaseBase
{
    protected void Wait(Game _game)
    {
        if (!_game.Actions.Any(x => x.Value == "WAIT")) 
        {
            Terminal.LogArray($"Available Actions:", _game.Actions.Select(x => x.Value));
            throw new System.Exception("can't wait"); 
        }
        Terminal.Command("WAIT wait...");
    }
}

public abstract class PhaseBase : IPhase
{
    public abstract EPhase Name { get; }

    public abstract void Run(Game _game);

    protected void Random(Game _game)
    {
        if (!_game.Actions.Any(x => x.Value == "RANDOM")) 
        {
            Terminal.LogArray($"Available Actions:", _game.Actions.Select(x => x.Value));
            throw new System.Exception("can't random"); 
        }
        Terminal.Command("RANDOM idk...");
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
    IEnumerable<ICard> Cards { get; }
    IEnumerable<ICard> Automated { get; }
    int PlayerPermanentDailyRoutineCards { get; set; }
    int PlayerPermanentArchitectureStudyCards { get; set; }
    EDesk Desk { get; set; }
    int Score { get; set; }
}

public class GamePlayer : IPlayer
{
    public IEnumerable<ICard> Hand { get; set; }
    public IEnumerable<ICard> Deck { get; set; }
    public IEnumerable<ICard> Discard { get; set; }    
    public IEnumerable<ICard> Automated { get; set; }
    public int PlayerPermanentDailyRoutineCards { get; set; }
    public int PlayerPermanentArchitectureStudyCards { get; set; }
    public EDesk Desk { get; set; }
    public int Score { get; set; }

    public IEnumerable<ICard> Cards => Hand.Concat(Discard).Concat(Deck).ToList();

    public GamePlayer()
    {
        this.Hand = new ICard[0];
        this.Deck = new ICard[0];
        this.Discard = new ICard[0];
        this.Automated = new ICard[0];
        this.Desk = (EDesk)(-1);
        this.Score = 0;
        this.PlayerPermanentDailyRoutineCards = 0;
        this.PlayerPermanentArchitectureStudyCards = 0;
    }
}

public class GameOpponent : IPlayer
{
    public IEnumerable<ICard> Cards { get; set; }
    public IEnumerable<ICard> Automated { get; set; }
    public int PlayerPermanentDailyRoutineCards { get; set; }
    public int PlayerPermanentArchitectureStudyCards { get; set; }
    public EDesk Desk { get; set; }
    public int Score { get; set; }

    public GameOpponent()
    {
        this.Cards = new List<ICard>();
        this.Automated = new ICard[0];
        this.Desk = (EDesk)(-1);
        this.Score = 0;
        this.PlayerPermanentDailyRoutineCards = 0;
        this.PlayerPermanentArchitectureStudyCards = 0;
    }
}

public static class CardFactory
{
    public static IEnumerable<ICard> Parse(ECard _card, string _count)
    {
        var cards = new List<ICard>();
        for (int i = 0; i < int.Parse(_count); i++)
        {
            ICard card;
            switch (_card)
            {
                case ECard.BONUS: 
                    card = new BonusCard(); 
                    break;
                case ECard.TECHNICAL_DEBT: 
                    card = new TechDebtCard(); 
                    break;
                default: 
                    card = new GenericCard { Name = _card }; 
                    break;
            }
            cards.Add(card);
        }
        return cards;
    }
}

public interface ICard
{
    ECard Name { get; }
    int Priority { get; }
    IEnumerable<IMana> GetProvidedMana();
}

public struct GenericCard : ICard
{
    public ECard Name { get; set; }
    public int Priority => 0;

    public IEnumerable<IMana> GetProvidedMana()
    {
        return new IMana[]
        {
            new Mana 
            {
                Name = Enum.Parse<EMana>(Name.ToString()),
                Value = 2,
            },
            new ShoddyMana 
            {
                Value = 2,
            },
        };
    }
}

public struct BonusCard : ICard
{
    public ECard Name => ECard.BONUS;
    public int Priority => 1;

    public IEnumerable<IMana> GetProvidedMana()
    {
        return new IMana[] 
        {
            new WildcardMana 
            {
                Value = 1,
            },
            new ShoddyMana 
            { 
                Value = 1,
            },
        };
    }
}

public struct TechDebtCard : ICard
{
    public ECard Name => ECard.TECHNICAL_DEBT;
    public int Priority => 99;

    public IEnumerable<IMana> GetProvidedMana()
    {
        return new IMana[0];
    }
}

public interface IMana
{
    EMana Name { get; }
    int Value { get; }
}

public struct Mana : IMana
{
    public EMana Name { get; set; }
    public int Value { get; set; }
}

public struct WildcardMana : IMana
{
    public EMana Name => EMana.WILDCARD;
    public int Value { get; set; }
}

public struct ShoddyMana : IMana
{
    public EMana Name => EMana.SHODDY;
    public int Value { get; set; }
}

public enum EMana
{
    TRAINING = 0,
    CODING = 1,
    DAILY_ROUTINE = 2,
    TASK_PRIORITIZATION = 3,
    ARCHITECTURE_STUDY = 4,
    CONTINUOUS_DELIVERY = 5,
    CODE_REVIEW = 6,
    REFACTORING = 7,
    WILDCARD = 8,
    SHODDY = 9,
}

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
}

public static class CostFactory
{
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
            costs.AddRange(CostFactory.Parse(EDesk.TRAINING, inputs[2]));
            costs.AddRange(CostFactory.Parse(EDesk.CODING, inputs[3]));
            costs.AddRange(CostFactory.Parse(EDesk.DAILY_ROUTINE, inputs[4]));
            costs.AddRange(CostFactory.Parse(EDesk.TASK_PRIORITIZATION, inputs[5]));
            costs.AddRange(CostFactory.Parse(EDesk.ARCHITECTURE_STUDY, inputs[6]));
            costs.AddRange(CostFactory.Parse(EDesk.CONTINUOUS_DELIVERY, inputs[7]));
            costs.AddRange(CostFactory.Parse(EDesk.CODE_REVIEW, inputs[8]));
            costs.AddRange(CostFactory.Parse(EDesk.REFACTORING, inputs[9]));

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

            List<ICard> cards = new List<ICard>();
            cards.AddRange(CardFactory.Parse(ECard.TRAINING, inputs[1]));
            cards.AddRange(CardFactory.Parse(ECard.CODING, inputs[2]));
            cards.AddRange(CardFactory.Parse(ECard.DAILY_ROUTINE, inputs[3]));
            cards.AddRange(CardFactory.Parse(ECard.TASK_PRIORITIZATION, inputs[4]));
            cards.AddRange(CardFactory.Parse(ECard.ARCHITECTURE_STUDY, inputs[5]));
            cards.AddRange(CardFactory.Parse(ECard.CONTINUOUS_DELIVERY, inputs[6]));
            cards.AddRange(CardFactory.Parse(ECard.CODE_REVIEW, inputs[7]));
            cards.AddRange(CardFactory.Parse(ECard.REFACTORING, inputs[8]));
            cards.AddRange(CardFactory.Parse(ECard.BONUS, inputs[9]));
            cards.AddRange(CardFactory.Parse(ECard.TECHNICAL_DEBT, inputs[10]));

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