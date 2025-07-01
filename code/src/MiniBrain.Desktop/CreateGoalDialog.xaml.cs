using System.Windows;

namespace MiniBrain.Desktop;

public partial class CreateGoalDialog : Window
{
    public string GoalTitle => TitleTextBox.Text.Trim();
    public string GoalDescription => DescriptionTextBox.Text.Trim();
    public int GoalPriority => (int)PrioritySlider.Value;

    public CreateGoalDialog()
    {
        InitializeComponent();
        PrioritySlider.ValueChanged += PrioritySlider_ValueChanged;
    }

    private void PrioritySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PriorityTextBlock != null)
        {
            PriorityTextBlock.Text = $"Priority: {(int)e.NewValue}";
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GoalTitle))
        {
            MessageBox.Show("Please enter a goal title.", "Validation Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
