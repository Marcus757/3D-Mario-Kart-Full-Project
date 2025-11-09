public class StarItemBehaviour : IItemBehaviour
{
    public bool SupportsTrailing => false;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        if (!aimBackwardHeld)
        {
            context.Manager.HandleStarUse();
        }
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
    }
}

