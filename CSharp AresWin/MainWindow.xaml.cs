private void SetAccentColor(string hexColor, bool updateAccentSelector = false)
{
    // update the primary accent brushes
    SetBrushColor("TronCyan", hexColor);
    SetBrushColor("TronBlue", hexColor);

    // update any UI effects that use accent
    SetDropShadowColor("GlowCyan", hexColor);
    SetDropShadowColor("TextGlow", hexColor);

    // Also update border/controls so accent change is visible across windows.
    // Use the same hex for PanelBorderBrush and ComboBoxBorderBrush so borders follow accent.
    SetBrushColor("PanelBorderBrush", hexColor);
    SetBrushColor("ComboBoxBorderBrush", hexColor);

    if (updateAccentSelector)
    {
        SelectComboBoxItemByTag(AccentSelector, hexColor);
    }
}