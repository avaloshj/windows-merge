using TabDock.Core.Abstractions;

namespace TabDock.Core.Tests.Fakes;

internal sealed class FakeHostFrame : IHostFrame
{
    public event EventHandler? SlotBoundsChanged;

    public event EventHandler? WindowStateChanged;

    public event EventHandler<FrameActivatedEventArgs>? Activated;

    public event EventHandler? ZOrderChanged;

    public const int FrameHandle = 9999;

    public WindowHandle Handle { get; } = new(FrameHandle);

    /// <summary>El hueco está abierto: se ve la ventana activa, que está debajo del marco.</summary>
    public bool HoleOpen { get; private set; }

    public void SetSlotHole(bool open) => HoleOpen = open;

    /// <summary>Barra propia del marco visible (tapa la barra nativa de la ventana activa).</summary>
    public bool OwnCaptionVisible { get; private set; }

    /// <summary>Como el marco real: mostrar su barra baja el hueco 40 px (la fila de la barra) y avisa del cambio.</summary>
    public void SetOwnCaptionVisible(bool visible)
    {
        if (OwnCaptionVisible == visible)
            return;

        OwnCaptionVisible = visible;
        var shift = visible ? CaptionRowHeight : -CaptionRowHeight;
        MoveSlot(new PixelRect(SlotBounds.Left, SlotBounds.Top + shift, SlotBounds.Right, SlotBounds.Bottom));
    }

    public const int CaptionRowHeight = 40;

    public PixelRect SlotBounds { get; set; } = new(300, 140, 1500, 900);

    public bool IsMinimized { get; set; }

    public bool IsMaximized { get; set; }

    public List<PixelRect> MovedTo { get; } = [];

    public List<PixelRect> FrameBounds { get; } = [];

    public void SetFrameBounds(PixelRect bounds) => FrameBounds.Add(bounds);

    public void MoveSlotTo(PixelRect slot)
    {
        MovedTo.Add(slot);
        SlotBounds = slot;
    }

    public void Maximize() => IsMaximized = true;

    public void Minimize()
    {
        IsMinimized = true;
        WindowStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Restore()
    {
        IsMinimized = false;
        IsMaximized = false;
        WindowStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MoveSlot(PixelRect slot)
    {
        SlotBounds = slot;
        SlotBoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Simula que Windows subió el marco (p. ej. un clic en su barra lateral).</summary>
    public void RaiseZOrderChanged() => ZOrderChanged?.Invoke(this, EventArgs.Empty);

    public void Activate(bool forwardFocus = false) => Activated?.Invoke(this, new FrameActivatedEventArgs(forwardFocus));
}
