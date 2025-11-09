public class BananaItemBehaviour : IItemBehaviour
{
    private readonly int trailingIndex;

    public BananaItemBehaviour(int trailingIndex)
    {
        this.trailingIndex = trailingIndex;
    }

    public bool SupportsTrailing => true;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        if (!aimBackwardHeld)
        {
            context.Manager.StartTrailingItemIfNeeded(trailingIndex);
        }
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
        context.Manager.HandleBananaRelease(aimBackwardHeld);
    }
}

