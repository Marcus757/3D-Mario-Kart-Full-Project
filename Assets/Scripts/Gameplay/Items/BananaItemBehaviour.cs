public class BananaItemBehaviour : IItemBehaviour
{
    private readonly string trailingItemName;

    public BananaItemBehaviour(string trailingItemName)
    {
        this.trailingItemName = trailingItemName;
    }

    public bool SupportsTrailing => true;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        if (!aimBackwardHeld)
        {
            context.Manager.StartTrailingItemIfNeeded(trailingItemName);
        }
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
        context.Manager.HandleBananaRelease(aimBackwardHeld);
    }
}

