public class BobombItemBehaviour : IItemBehaviour
{
    public bool SupportsTrailing => true;

    public void OnUse(ItemContext context, bool aimBackwardHeld, bool useHeld, bool usePressedThisFrame)
    {
        if (!aimBackwardHeld && usePressedThisFrame && !context.Manager.IsBobombTrailingActive)
        {
            context.Manager.HandleBobombForwardUse();
        }
        else if (aimBackwardHeld && useHeld && !context.Manager.IsBobombTrailingActive)
        {
            context.Manager.HandleBobombStartTrailing();
        }
    }

    public void OnRelease(ItemContext context, bool aimBackwardHeld)
    {
        if (context.Manager.IsBobombTrailingActive)
        {
            context.Manager.HandleBobombRelease(aimBackwardHeld);
        }
    }
}


