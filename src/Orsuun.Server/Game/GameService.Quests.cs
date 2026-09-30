using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Map quests (owner, 30 Sep 2026; Rules.MapQuests): each map's chain, counted where the deeds happen (Settle, the Commander
// fight) and claimed on the quest card.
public sealed partial class GameService
{
    private static QuestProgress QuestsOf(Account account) => QuestProgress.Parse(account.MapQuests);

    /// <summary>Counts a deed on a map toward the map's current quest step (nothing when it asks for something else).</summary>
    private static void CountQuest(Account account, int map, QuestKind kind, long amount)
    {
        if (map <= 0 || amount <= 0) return;
        QuestProgress p = QuestsOf(account);
        if (p.Add(map, kind, amount)) account.MapQuests = p.Serialize();
    }

    /// <summary>A map's chain as the hero stands in it (null: no map, e.g. hunting a zone).</summary>
    private static MapQuestDto? QuestDtoOf(Account account, int map, QuestProgress? progress = null)
    {
        MapQuestDef? quest = MapQuests.For(map);
        if (quest == null) return null;
        QuestProgress p = progress ?? QuestsOf(account);
        int step = Math.Min(p.Step(map), quest.Steps.Length);
        QuestStepDef? current = p.Current(map);
        bool last = step == quest.Steps.Length - 1;
        string camp = (current ?? quest.Steps[^1]).Camp;
        return new MapQuestDto(map, Content.Maps[map - 1].Name, quest.Giver, quest.Title, step, quest.Steps.Length,
            current?.Ask ?? quest.Ending, current == null ? "" : MapQuests.Task(quest, current), current == null ? 0 : p.Done(map),
            current?.Target ?? 0, camp, p.Ready(map), MapQuests.Open(map, account.HighestStageCleared),
            MapQuests.Sorn(map, last), MapQuests.Xp(map, last), MapQuests.Materials(last), last, quest.Ending,
            (map - 1) * MapDef.StagesPerMap);
    }

    /// <summary>Every map's chain, and the map the hero hunts (0: a zone).</summary>
    public QuestsDto Quests(Account account)
    {
        QuestProgress p = QuestsOf(account);
        return new QuestsDto(MapQuests.All.Select(q => QuestDtoOf(account, q.Map, p)!).ToArray(), MapQuests.MapOfPlace(account.ParkedStage));
    }

    /// <summary>Development: counts toward a map's current step as if its deed were done (the smoke test).</summary>
    public async Task<QuestsDto> DevQuestAsync(Account account, int map, long amount, CancellationToken ct)
    {
        if (QuestsOf(account).Current(map) is { } step) CountQuest(account, map, step.Kind, amount);
        await SaveAsync(ct);
        return Quests(account);
    }

    /// <summary>Claims a map's finished step: its sorn, XP and materials, and the chain's piece at its end (by letter when the
    /// bag is full); the chain moves on.</summary>
    public async Task<StateDto> ClaimQuestAsync(Account account, QuestClaimRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        MapQuestDef quest = MapQuests.For(request.Map) ?? throw new GameException("no_quest", "That map has no quest.");
        QuestProgress p = QuestsOf(account);
        if (p.Finished(quest.Map)) throw new GameException("quest_done", "That quest is finished.");
        if (!p.Ready(quest.Map)) throw new GameException("quest_open", "Not done yet: " + MapQuests.Task(quest, p.Current(quest.Map)!) + ".");
        int step = p.Step(quest.Map);
        bool last = step == quest.Steps.Length - 1;

        Inventory inventory = Snapshot(account);
        long sorn = MapQuests.Sorn(quest.Map, last);
        inventory.Sorn += sorn;
        inventory.Xp += MapQuests.Xp(quest.Map, last);
        inventory.Materials += MapQuests.Materials(last);
        string pieceName = "";
        if (last)
        {
            ItemState piece = MapQuests.Piece(quest.Map, inventory, _rng);
            pieceName = piece.DisplayName;
            int held = account.Items.Count(x => !x.Equipped && !x.Destroyed && !x.OutOfBag);
            if (held >= Bag.Size)
            {
                // The bag is full: the piece waits in the mailbox (Item.InMail, out of the bag) rather than being left behind.
                inventory.Loot.Remove(piece);
                Item item = Item.From(piece, account.Id, equipped: false);
                item.InMail = true;
                account.Items.Add(item);
                SendLetter(account.Id, "quest", quest.Giver, quest.Title, quest.Ending + "\nYour bag was full, so it came by letter.", itemId: item.Id);
                pieceName += " (by letter)";
            }
        }
        Apply(account, inventory);
        p.Advance(quest.Map);
        account.MapQuests = p.Serialize();
        Mark(account, "quest");
        _db.Ledger.Add(Entry(account.Id, null, "quest", $"map={quest.Map} step={step + 1}/{quest.Steps.Length} materials={MapQuests.Materials(last)} piece={pieceName}", sorn, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
    }
}
