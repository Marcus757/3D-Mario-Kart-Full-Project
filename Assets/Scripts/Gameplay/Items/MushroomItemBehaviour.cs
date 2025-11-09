public class MushroomItemBehaviour : IItemBehaviour
{
    public bool SupportsTrailing => false;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        if (!aimBackwardHeld)
        {
            context.Manager.HandleMushroomUse();
        }
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
    }
}

