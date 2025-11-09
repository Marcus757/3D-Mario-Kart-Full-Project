public class ShellItemBehaviour : IItemBehaviour
{
    private readonly int trailingIndex;
    private readonly bool isRedShell;

    public ShellItemBehaviour(int trailingIndex, bool isRedShell)
    {
        this.trailingIndex = trailingIndex;
        this.isRedShell = isRedShell;
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
        if (isRedShell)
        {
            context.Manager.HandleRedShellRelease(aimBackwardHeld);
        }
        else
        {
            context.Manager.HandleGreenShellRelease(aimBackwardHeld);
        }
    }
}

