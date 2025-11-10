public class ShellItemBehaviour : IItemBehaviour
{
    private readonly string trailingItemName;
    private readonly bool isRedShell;

    public ShellItemBehaviour(string trailingItemName, bool isRedShell)
    {
        this.trailingItemName = trailingItemName;
        this.isRedShell = isRedShell;
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

