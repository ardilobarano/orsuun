using System.ComponentModel.DataAnnotations;
using Orsuun.Rules;

namespace Orsuun.Server.Data;

public sealed class Account
{
    public Guid Id { get; set; }
    /// <summary>Opaque device token for the guest login. Real auth (Apple, Google) replaces this later.</summary>
    [MaxLength(128)] public string DeviceToken { get; set; } = "";
    [MaxLength(64)] public string? SessionToken { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int WeaponsBroken { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }

    public long Sorn { get; set; }
    public int Potions { get; set; }
    public int Materials { get; set; }
    public int ScrollsOfMercy { get; set; }
    public int KhansAlloys { get; set; }
    public int AnvilWards { get; set; }
    public int Turnstones { get; set; }

    public Guid? EquippedWeaponId { get; set; }
    public Item? EquippedWeapon { get; set; }

    /// <summary>Mapped to PostgreSQL's xmin by IsRowVersion(); never set by hand.</summary>
    public uint Version { get; set; }
}

public sealed class Item
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public int ItemLevel { get; set; }
    public Rarity Rarity { get; set; }
    public int UpgradeLevel { get; set; }
    public int PatienceBp { get; set; }
    public int LockedEtchingIndex { get; set; } = -1;
    /// <summary>Etchings as "entryId:tier:value" triples, in slot order.</summary>
    [MaxLength(256)] public string Etchings { get; set; } = "";
    public bool Destroyed { get; set; }
    public DateTime CreatedUtc { get; set; }

    public ItemState ToState()
    {
        var state = new ItemState(ItemLevel, Rarity)
        {
            UpgradeLevel = UpgradeLevel,
            PatienceBp = PatienceBp,
            LockedEtchingIndex = LockedEtchingIndex,
            Destroyed = Destroyed,
        };
        foreach (string triple in Etchings.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = triple.Split(':');
            state.Etchings.Add(new Etching(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2])));
        }
        return state;
    }

    public void ApplyState(ItemState state)
    {
        UpgradeLevel = state.UpgradeLevel;
        PatienceBp = state.PatienceBp;
        LockedEtchingIndex = state.LockedEtchingIndex;
        Destroyed = state.Destroyed;
        Etchings = string.Join(';', state.Etchings.Select(e => $"{e.EntryId}:{e.Tier}:{e.Value}"));
    }
}

/// <summary>Append-only record of every roll and every currency change. Support and rate audits read this.</summary>
public sealed class LedgerEntry
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime Utc { get; set; }
    /// <summary>Idempotency key from the client, unique per account.</summary>
    [MaxLength(64)] public string RequestId { get; set; } = "";
    [MaxLength(32)] public string Kind { get; set; } = "";
    /// <summary>Human-readable inputs and result, for example "ScrollOfMercy +7->+8 chance=5000 roll=success".</summary>
    [MaxLength(512)] public string Detail { get; set; } = "";
    public long SornDelta { get; set; }
}
