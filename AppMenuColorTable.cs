namespace SimpleSync;

internal sealed class AppMenuRenderer(AppSkin skin) : ToolStripProfessionalRenderer(new AppMenuColorTable(skin))
{
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == false ? skin.MutedText : skin.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var rect = e.ImageRectangle;
        using var pen = new Pen(e.Item.Enabled ? skin.Text : skin.MutedText, 2);
        e.Graphics.DrawLines(pen, [new Point(rect.Left + 2, rect.Top + rect.Height / 2),
            new Point(rect.Left + rect.Width / 2 - 1, rect.Bottom - 3), new Point(rect.Right - 2, rect.Top + 3)]);
    }
}

internal sealed class AppMenuColorTable(AppSkin skin) : ProfessionalColorTable
{
    public override Color MenuStripGradientBegin => skin.Surface;
    public override Color MenuStripGradientEnd => skin.Surface;
    public override Color ToolStripDropDownBackground => skin.Surface;
    public override Color ImageMarginGradientBegin => skin.Surface;
    public override Color ImageMarginGradientMiddle => skin.Surface;
    public override Color ImageMarginGradientEnd => skin.Surface;
    public override Color MenuItemSelected => skin.AccentSoft;
    public override Color MenuItemSelectedGradientBegin => skin.AccentSoft;
    public override Color MenuItemSelectedGradientEnd => skin.AccentSoft;
    public override Color MenuItemPressedGradientBegin => skin.SurfaceAlt;
    public override Color MenuItemPressedGradientMiddle => skin.SurfaceAlt;
    public override Color MenuItemPressedGradientEnd => skin.SurfaceAlt;
    public override Color MenuItemBorder => skin.Border;
    public override Color MenuBorder => skin.Border;
    public override Color SeparatorDark => skin.Border;
    public override Color SeparatorLight => skin.Surface;
    public override Color CheckBackground => skin.AccentSoft;
    public override Color CheckSelectedBackground => skin.AccentSoft;
    public override Color CheckPressedBackground => skin.AccentSoft;
}
