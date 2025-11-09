public class TripleShellBehaviour : IItemBehaviour
{
    private readonly bool isRedShell;

    public TripleShellBehaviour(bool isRedShell)
    {
        this.isRedShell = isRedShell;
    }

    public bool SupportsTrailing => false;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        context.Manager.HandleTripleShellUse(isRedShell, aimBackwardHeld);
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
        // Triple shells do not react to release events
    }
}



