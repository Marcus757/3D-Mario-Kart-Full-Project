public class TripleBananaBehaviour : IItemBehaviour
{
    public bool SupportsTrailing => false;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        context.Manager.HandleTripleBananaUse(aimBackwardHeld);
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
        // Triple bananas do not rely on release events
    }
}



