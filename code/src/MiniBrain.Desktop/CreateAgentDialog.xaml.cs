using System.Windows;

namespace MiniBrain.Desktop;

public partial class CreateAgentDialog : Window
{
    public string AgentName => NameTextBox.Text.Trim();
    public string AgentDescription => DescriptionTextBox.Text.Trim();
    public string AgentInstructions => InstructionsTextBox.Text.Trim();

    public CreateAgentDialog()
    {
        InitializeComponent();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AgentName))
        {
            MessageBox.Show("Please enter an agent name.", "Validation Error", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(AgentInstructions))
        {
            MessageBox.Show("Please enter system instructions for the agent.", "Validation Error", 
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
