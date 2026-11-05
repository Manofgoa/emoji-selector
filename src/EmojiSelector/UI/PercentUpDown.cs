namespace EmojiSelector.UI;

/// <summary>
/// A <see cref="NumericUpDown"/> showing its value as a percentage, <c>120%</c>: a typed value is read from its digits,
/// the sign optional, then clamped to the range.
/// </summary>
internal sealed class PercentUpDown : NumericUpDown
{
    protected override void UpdateEditText() => this.Text = $"{this.Value:0}%";

    protected override void ValidateEditText()
    {
        string digits = new([.. this.Text.Where(char.IsAsciiDigit)]);
        if (decimal.TryParse(digits, out decimal value))
        {
            this.Value = Math.Clamp(value, this.Minimum, this.Maximum);
        }

        this.UserEdit = false;
        this.UpdateEditText();
    }
}
