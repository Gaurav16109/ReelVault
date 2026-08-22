namespace ReelVault.App;

// Phase 6.4 (B2): shared tactile press feedback so buttons, Home tiles, and browse cards all feel
// equally alive. Durations/intensity come from AnimationTuning.
public static class PressFeedback
{
    // Buttons get real Pressed/Released events - a native button press-state that fires reliably
    // cross-platform - so this is a true press-then-release, not just a tap-completion bounce.
    public static void AttachTo(Button button)
    {
        button.Pressed += async (_, _) => await button.ScaleToAsync(AnimationTuning.PressScaleDown, AnimationTuning.PressDownMs, Easing.CubicOut);
        button.Released += async (_, _) => await button.ScaleToAsync(1.0, AnimationTuning.PressUpMs, Easing.CubicOut);
    }

    // Borders/cards (CollectionView cells, Home tiles) have no native press state - only a
    // TapGestureRecognizer's single completed "Tapped" event - so this plays as one quick bounce
    // immediately before whatever action the tap triggers, rather than true press-then-release.
    public static async Task PunchAsync(VisualElement element, BoxView? glow = null)
    {
        await Task.WhenAll(
            element.ScaleToAsync(AnimationTuning.PressScaleDown, AnimationTuning.PressDownMs, Easing.CubicOut),
            glow is null ? Task.CompletedTask : glow.FadeToAsync(AnimationTuning.PressGlowOpacity, AnimationTuning.PressDownMs));

        await Task.WhenAll(
            element.ScaleToAsync(1.0, AnimationTuning.PressUpMs, Easing.CubicOut),
            glow is null ? Task.CompletedTask : glow.FadeToAsync(0, AnimationTuning.PressUpMs + 20));
    }
}
