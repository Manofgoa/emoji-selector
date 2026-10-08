namespace EmojiSelector.UI;

/// <summary>
/// Asks for a <b>custom group</b>'s name — a new group's, or a new name for one. The name is trimmed, and OK stays
/// greyed while it is blank; two groups may have the same name.
/// </summary>
internal sealed class GroupNameDialog : Form
{
    public const string NameLabel = "Name:";

    // In logical pixels (96 DPI): the dialog scales with the form's DPI.
    private const int LogicalNameWidth = 280;
    private const int LogicalPadding = 12;

    private readonly TextBox nameBox;
    private readonly Button okButton;

    public GroupNameDialog(string title, string name)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        this.Text = title;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MinimizeBox = false;
        this.MaximizeBox = false;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        this.nameBox = new TextBox { Text = name, Width = LogicalNameWidth, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        this.okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, LogicalPadding / 2, 0, 0),
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(this.okButton);

        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(LogicalPadding),
        };
        panel.Controls.Add(new Label { Text = NameLabel, AutoSize = true });
        panel.Controls.Add(this.nameBox);
        panel.Controls.Add(buttons);
        this.Controls.Add(panel);

        this.AcceptButton = this.okButton;
        this.CancelButton = cancelButton;
        this.nameBox.TextChanged += (_, _) => this.UpdateOkButton();
        this.UpdateOkButton();
        ResumeLayout(performLayout: true);
    }

    /// <summary>The name typed, trimmed.</summary>
    public string GroupName => this.nameBox.Text.Trim();

    // The current name selected: typing replaces it.
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        this.nameBox.SelectAll();
        this.nameBox.Focus();
    }

    private void UpdateOkButton() => this.okButton.Enabled = this.GroupName.Length > 0;
}
