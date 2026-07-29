using System;

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
        if (!isRedShell && string.Equals(trailingItemName, "GreenShell", StringComparison.OrdinalIgnoreCase))
        {
            context.Manager.HandleGreenShellUsePressed(aimBackwardHeld);
            return;
        }

        if (isRedShell && string.Equals(trailingItemName, "RedShell", StringComparison.OrdinalIgnoreCase))
        {
            context.Manager.HandleRedShellUsePressed(aimBackwardHeld);
            return;
        }

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

