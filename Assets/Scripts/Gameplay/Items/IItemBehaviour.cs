public class ItemContext
{
    public ItemManager Manager { get; }
    public Player Player { get; }
    public PlayerSounds Sounds { get; }

    public ItemContext(ItemManager manager, Player player, PlayerSounds sounds)
    {
        Manager = manager;
        Player = player;
        Sounds = sounds;
    }
}

public interface IItemBehaviour
{
    bool SupportsTrailing { get; }
    void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame);
    void OnRelease(ItemContext context, bool aimBackwardHeld);
}

