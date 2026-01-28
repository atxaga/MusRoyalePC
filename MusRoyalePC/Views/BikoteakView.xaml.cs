using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusRoyalePC.Views
{
    public partial class BikoteakView : UserControl
    {
        private static readonly Regex NumericRegex = new("^[0-9]+([.,][0-9]{0,2})?$");

        public BikoteakView()
        {
            InitializeComponent();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !NumericRegex.IsMatch(((TextBox)sender).Text.Insert(((TextBox)sender).SelectionStart, e.Text));
        }
    }
}
