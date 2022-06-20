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
            new PlayCardPhase(),
            new GiveCardPhase(),
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
        Terminal.LogArray($"Available Actions", Actions.Select(x => x.Value));
        Terminal.Log($"Player Info (Score: {Player.Score})");
        Terminal.LogArray($"- Hand", Player.Hand.Select(x => x.Name.ToString()));
        Terminal.LogArray($"- Deck", Player.Deck.Select(x => x.Name.ToString()));
        Terminal.LogArray($"- Discard", Player.Discard.Select(x => x.Name.ToString()));
        Terminal.Log($"Opponent Info (Score: {Opponent.Score})");
        Terminal.LogArray($"- Cards", Opponent.Cards.Select(x => x.Name.ToString()));
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
        if (!TryGetPursuableApps(_game, out Dictionary<Application, IEnumerable<Cost>> _pursuable))
        {
            Terminal.Log($"No pursuable applications");
            Random(_game);
            return;
        }

        var bestAppToPursue = GetBestApplicationToPursue(_game, _pursuable);
        EDesk bestDesk = GetBestDeskToMove(_game, bestAppToPursue);
        LogDecision(_pursuable, bestAppToPursue, bestDesk);
        Move(_game, bestDesk);
    }

    private bool TryGetPursuableApps(Game _game, out Dictionary<Application, IEnumerable<Cost>> _pursuable)
    {
        var mana = _game.Player.AvailableMana();
        mana.Remove(EMana.WILDCARD);
        mana.Remove(EMana.SHODDY);

        _pursuable = _game.Apps
            .Where(x => !x.CanReleaseBasedOnMana(mana))
            .ToDictionary(x => x, y => y.CostExcludingAvailableMana(mana))
            .Where(x => x.Value.Count() > 0)
            .Where(x => x.Value.Any(x => IsAvailable(_game, x.Desk)))
            .ToDictionary(x => x.Key, y => y.Value);

        // LogPursuableCalculation(_game, _pursuable);
        return _pursuable.Count() > 0;
    }    

    private KeyValuePair<Application, IEnumerable<Cost>> GetBestApplicationToPursue(Game _game, Dictionary<Application, IEnumerable<Cost>> _pursuable)
    {
        var best = _pursuable
            .OrderBy(x => x.Key.RequiredShoddyMana(_game.Player.AvailableMana()))
            .ThenByDescending(x => x.Key.Costs.Where(x => IsSafeSpot(_game, x)).Count());

        LogBestApplicationCalculation(_game, best);
        return best.First();
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
        IPlayer[] players = new IPlayer[] { _game.Player };
        foreach (var player in players)
        {
            if (player.Desk == _desk) { return false; }
        }
        return true;
    }

    private bool IsSafeSpot(Game _game, Cost _cost)
    {
        IPlayer[] players = new IPlayer[] { _game.Player };
        foreach (var player in players)
        {
            bool isSameDesk = player.Desk == _cost.Desk;
            bool isNextDesk = (int)player.Desk == (int)_cost.Desk + 1;
            bool isPrevDesk = (int)player.Desk == (int)_cost.Desk - 1;
            if (isSameDesk || isNextDesk || isPrevDesk) { return false; }
        }
        return true;
    }

    protected void Move(Game _game, EDesk _desk)
    {
        if (!_game.Actions.Any(x => x.Value == $"MOVE {(int)_desk}")) 
        {
            Terminal.LogArray($"Available Actions", _game.Actions.Select(x => x.Value));
            throw new System.Exception($"can't move to desk {_desk}"); 
        }
        Terminal.Log($"Moving to desk {_desk}");
        Terminal.Command($"MOVE {(int)_desk}");
    }

    private void LogDecision(Dictionary<Application, IEnumerable<Cost>> _pursuable, KeyValuePair<Application, IEnumerable<Cost>> _bestApplicationToPursue, EDesk _bestDesk)
    {
        Terminal.LogArray($"Pursuable Applications", _pursuable.Select(x => x.Key.Id.ToString()));
        Terminal.Log($"Best Application: {_bestApplicationToPursue.Key.Id.ToString()}");
        Terminal.Log($"Best Desk: {_bestDesk.ToString()}");
    }

    private void LogPursuableCalculation(Game _game, Dictionary<Application, IEnumerable<Cost>> _apps)
    {
        Terminal.LogArray("Hand", _game.Player.Hand.Select(x => x.Name.ToString()));
        Terminal.Log($"Total Needed After Deductions:");
        foreach (var app in _apps)
        {
            Terminal.LogArray($"- {app.Key.Type} {app.Key.Id}", app.Value.Select(x => $"x{x.Value} {x.Desk}"));
        }
    }

    private void LogBestApplicationCalculation(Game _game, IOrderedEnumerable<KeyValuePair<Application, IEnumerable<Cost>>> _best)
    {
        Terminal.Log($"Best App:");
        foreach (var app in _best)
        {
            Terminal.LogArray($"- {app.Key.Type} {app.Key.Id} (Shoddy: {app.Key.RequiredShoddyMana(_game.Player.AvailableMana())})", app.Value.Select(x => $"{x.Desk}: {IsSafeSpot(_game, x)}"));
        }
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
        var availableMana = _game.Player.AvailableMana();

        IEnumerable<Application> releasable = _game.Apps
            .Where(x => x.CanRelease(_game.Actions))
            .OrderBy(x => x.RequiredShoddyMana(availableMana));
        
        _app = releasable.FirstOrDefault();
        LogOrder(releasable, availableMana);
        return _app.HasValue;
    }

    protected void Release(Game _game, Application _app)
    {
        if (!_app.CanRelease(_game.Actions)) 
        {
            Terminal.LogArray($"Available Actions", _game.Actions.Select(x => x.Value));
            throw new System.Exception($"can't release app {_app.Id}"); 
        }
        Terminal.Log($"Releasing app {_app.Id}");
        Terminal.Command($"RELEASE {_app.Id}");
    }

    private void LogOrder(IEnumerable<Application> _releasable, Dictionary<EMana, int> _availableMana)
    {
        Terminal.LogArray($"Required Shoddy", _releasable.Select(x => $"{x.Type} {x.Id}: {x.RequiredShoddyMana(_availableMana)}"));
    }
}

public class PlayCardPhase : OptionalPhase
{
    private const int RELEASE_TECH_DEBT_SAFETY_THRESHOLD = 2;

    public override EPhase Name => EPhase.PLAY_CARD;

    public override void Run(Game _game)
    {
        if (CanSafelyReleaseApplication(_game))
        {
            Wait(_game);
            return;
        }

        PlayBestCard(_game);
    }

    private bool CanSafelyReleaseApplication(Game _game)
    {
        var availableMana = _game.Player.AvailableMana();

        IEnumerable<Application?> releasable = _game.Apps
            .Where(x => x.CanRelease(_game.Actions))
            .OrderBy(x => x.RequiredShoddyMana(availableMana))
            .Cast<Application?>();

        Application? best = releasable.FirstOrDefault();
        return best.HasValue && best.Value.RequiredShoddyMana(availableMana) <= RELEASE_TECH_DEBT_SAFETY_THRESHOLD;
    }

    private void PlayBestCard(Game _game)
    {
        List<ECard> order = new List<ECard>()
        {
            ECard.TRAINING,
            ECard.ARCHITECTURE_STUDY,
            ECard.CODE_REVIEW,
            ECard.REFACTORING,
        };

        ICard? card = _game.Player.Hand
            .Where(x => order.Contains(x.Name))
            .OrderBy(x => order.IndexOf(x.Name))
            .FirstOrDefault();

        if (card is null) 
        { 
            Random(_game);
            return;
        }

        Play(_game, card);
    }

    protected void Play(Game _game, ICard _card)
    {
        if (!_game.Actions.Any(x => x.Value == _card.Name.ToString()))
        {
            Terminal.LogArray($"Available Actions", _game.Actions.Select(x => x.Value));
            throw new System.Exception($"can't play card {_card.Name}");
        }
        Terminal.Command($"{_card.Name} playing...");
    }
}

public class GiveCardPhase : PhaseBase
{
    public override EPhase Name => EPhase.GIVE_CARD;

    public override void Run(Game _game)
    {
        List<ECard> order = new List<ECard>()
        {
            ECard.REFACTORING,
            ECard.CODE_REVIEW,
            ECard.ARCHITECTURE_STUDY,
            ECard.TRAINING,
        };

        ICard? card = _game.Player.Hand
            .Where(x => order.Contains(x.Name))
            .OrderBy(x => order.IndexOf(x.Name))
            .FirstOrDefault();

        if (card is null)
        {
            Random(_game);
            return;
        }

        Give(_game, card);
    }

    protected void Give(Game _game, ICard _card)
    {
        if (!_game.Actions.Any(x => x.Value == $"GIVE {_card.Id}"))
        {
            Terminal.LogArray($"Available Actions", _game.Actions.Select(x => x.Value));
            throw new System.Exception($"can't give card {_card.Name}");
        }
        Terminal.Command($"GIVE {_card.Id} giving...");
    }
}

public abstract class OptionalPhase : PhaseBase
{
    protected void Wait(Game _game)
    {
        if (!_game.Actions.Any(x => x.Value == "WAIT")) 
        {
            Terminal.LogArray($"Available Actions", _game.Actions.Select(x => x.Value));
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
            Terminal.LogArray($"Available Actions", _game.Actions.Select(x => x.Value));
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
    public IEnumerable<ICard> InPlay { get; set; }
    public IEnumerable<ICard> Automated { get; set; }
    public int PlayerPermanentDailyRoutineCards { get; set; }
    public int PlayerPermanentArchitectureStudyCards { get; set; }
    public EDesk Desk { get; set; }
    public int Score { get; set; }
    
    public IEnumerable<ICard> Cards => Hand.Concat(Discard).Concat(Deck).Concat(InPlay).ToList();

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

    public Dictionary<EMana, int> AvailableMana()
    {
        IEnumerable<IMana> totalMana = Hand.SelectMany(x => x.GetProvidedMana());
        Dictionary<EMana, int> sortedMana = new Dictionary<EMana, int>();
        totalMana.ToList().ForEach(m => AddToDictionary(ref sortedMana, m));
        return sortedMana;
    }

    private void AddToDictionary(ref Dictionary<EMana, int> sortedMana, IMana _mana)
    {
        if (_mana.Value > 0)
        {
            if (sortedMana.ContainsKey(_mana.Name)) { sortedMana[_mana.Name] += _mana.Value; }
            else { sortedMana.Add(_mana.Name, _mana.Value); }
        }
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
    int Id { get; }
    int Priority { get; }
    IEnumerable<IMana> GetProvidedMana();
}

public struct GenericCard : ICard
{
    public ECard Name { get; set; }
    public int Id => (int)Name;
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
    public int Id => (int)Name;
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
    public int Id => (int)Name;
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

    public bool CanReleaseBasedOnMana(Dictionary<EMana, int> _availableMana)
    {
        var mana = new Dictionary<EMana, int>(_availableMana);
        return CostExcludingAvailableMana(mana).Count() <= 0;
    }

    public int RequiredShoddyMana(Dictionary<EMana, int> _availableMana)
    {
        var filteredMana = new Dictionary<EMana, int>(_availableMana);
        filteredMana.Remove(EMana.SHODDY);
        var costs = CostExcludingAvailableMana(filteredMana);
        return costs.Sum(x => x.Value);
    }

    public IEnumerable<Cost> CostExcludingAvailableMana(Dictionary<EMana, int> _availableMana)
    {
        Dictionary<EMana, int> available = new Dictionary<EMana, int>(_availableMana);

        // Deduct specific mana first
        List<Cost> newCostsMinusSpecific = DeductSpecificMana(available);

        // Deduct wildcard mana next
        List<Cost> newCostsMinusSpecificMinusWildcard = DeductMana(EMana.WILDCARD, available, newCostsMinusSpecific);

        // Deduct shoddy mana next
        List<Cost> newCostsMinusSpecificMinusWildcardMinusShoddy = DeductMana(EMana.SHODDY, available, newCostsMinusSpecificMinusWildcard);

        // LogManaCalculation(_availableMana, newCostsMinusSpecific, newCostsMinusSpecificMinusWildcard, newCostsMinusSpecificMinusWildcardMinusShoddy);
        return newCostsMinusSpecificMinusWildcardMinusShoddy;
    }    

    private List<Cost> DeductSpecificMana(Dictionary<EMana, int> _availableMana)
    {
        var mana = new List<Cost>();
        var costCopy = new List<Cost>(Costs);
        foreach (var cost in costCopy)
        {
            EMana key = Enum.Parse<EMana>(cost.Desk.ToString());
            DeductManaByKey(_availableMana, mana, cost, key);
        }
        return mana;
    }    

    private List<Cost> DeductMana(EMana _key, Dictionary<EMana, int> _availableMana, List<Cost> _costs)
    {
        var mana = new List<Cost>();
        var costCopy = new List<Cost>(_costs);
        foreach (var cost in costCopy)
        {
            DeductManaByKey(_availableMana, mana, cost, _key);
        }
        return mana;
    }

    private void DeductManaByKey(Dictionary<EMana, int> _availableMana, List<Cost> _mana, Cost _cost, EMana _key)
    {
        if (_availableMana.ContainsKey(_key))
        {
            int toRemove = Math.Min(_availableMana[_key], _cost.Value);
            int newCost = _cost.Value - toRemove;
            if (newCost > 0)
            {
                _mana.Add(new Cost
                {
                    Desk = _cost.Desk,
                    Value = newCost,
                });
            }

            _availableMana[_key] -= toRemove;
            if (_availableMana[_key] <= 0)
            {
                _availableMana.Remove(_key);
            }
        }
        else
        {
            _mana.Add(new Cost
            {
                Desk = _cost.Desk,
                Value = _cost.Value,
            });
        }
    }

    private void LogManaCalculation(Dictionary<EMana, int> _availableMana, List<Cost> _newCostsMinusSpecific, List<Cost> _newCostsMinusSpecificMinusWildcard, List<Cost> _newCostsMinusSpecificMinusWildcardMinusShoddy)
    {
        Terminal.Log($"{Type}: {Id}");
        Terminal.LogArray("Mana", _availableMana.Select(x => $"x{x.Value} {x.Key}"));
        Terminal.LogArray("Cost Total", Costs.Select(x => $"x{x.Value} {x.Desk}"));
        Terminal.LogArray("Cost Minus Specific", _newCostsMinusSpecific.Select(x => $"x{x.Value} {x.Desk}"));
        Terminal.LogArray("Cost Minus Wildcard", _newCostsMinusSpecificMinusWildcard.Select(x => $"x{x.Value} {x.Desk}"));
        Terminal.LogArray("Cost Minus Shoddy", _newCostsMinusSpecificMinusWildcardMinusShoddy.Select(x => $"x{x.Value} {x.Desk}"));
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
                case "PLAYED_CARDS":
                    _player.InPlay = cards;
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