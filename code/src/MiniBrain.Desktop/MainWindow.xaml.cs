using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MiniBrain.Core.Models;

namespace MiniBrain.Desktop;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private string _currentSessionId = Guid.NewGuid().ToString();
    private Agent? _selectedAgent;
    private List<Agent> _agents = new();
    private List<Goal> _goals = new();

    public MainWindow()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost:5089");
        
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckApiConnectionAsync();
        await LoadAgentsAsync();
        await LoadGoalsAsync();
    }

    private async Task CheckApiConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/agents");
            if (response.IsSuccessStatusCode)
            {
                ApiStatusTextBlock.Text = "🟢 Claude API: Connected";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.LightGreen);
                DbStatusTextBlock.Text = "🟢 Database: Connected";
                DbStatusTextBlock.Foreground = new SolidColorBrush(Colors.LightGreen);
                StatusTextBlock.Text = "Connected to MiniBrain API";
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to connect to API: {ex.Message}";
            ShowErrorMessage($"API Connection Failed: {ex.Message}");
        }
    }

    private async Task LoadAgentsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/agents");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _agents = JsonSerializer.Deserialize<List<Agent>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                }) ?? new List<Agent>();
                
                AgentsListBox.ItemsSource = _agents.Select(a => $"🤖 {a.Name}").ToList();
                
                if (_agents.Any() && _selectedAgent == null)
                {
                    _selectedAgent = _agents.First();
                    AgentsListBox.SelectedIndex = 0;
                    SendButton.IsEnabled = true;
                    await CreateConversationAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Failed to load agents: {ex.Message}");
        }
    }

    private async Task LoadGoalsAsync()
    {
        try
        {
            if (_selectedAgent != null)
            {
                var response = await _httpClient.GetAsync($"/api/goals/agent/{_selectedAgent.Id}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _goals = JsonSerializer.Deserialize<List<Goal>>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<Goal>();
                    
                    GoalsListBox.ItemsSource = _goals.Select(g => $"🎯 {g.Title} ({g.Status})").ToList();
                }
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Failed to load goals: {ex.Message}");
        }
    }

    private async Task CreateConversationAsync()
    {
        if (_selectedAgent == null) return;
        
        try
        {
            var request = new
            {
                AgentId = _selectedAgent.Id,
                SessionId = _currentSessionId
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync("/api/conversations", content);
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Failed to create conversation: {ex.Message}");
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessageAsync();
    }

    private async void ChatInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ChatInputTextBox.Text))
        {
            await SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        if (_selectedAgent == null || string.IsNullOrWhiteSpace(ChatInputTextBox.Text))
            return;

        var userMessage = ChatInputTextBox.Text.Trim();
        ChatInputTextBox.Text = "";
        
        AddMessageToChat("👤 You", userMessage, Colors.DodgerBlue);
        SendButton.IsEnabled = false;
        StatusTextBlock.Text = "Processing message...";

        try
        {
            var request = new
            {
                SessionId = _currentSessionId,
                Message = userMessage,
                AgentId = _selectedAgent.Id
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/conversations/message", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<dynamic>(responseJson);
                
                var botResponse = result?.GetProperty("response").GetString() ?? "No response received";
                AddMessageToChat($"🤖 {_selectedAgent.Name}", botResponse, Colors.MediumSeaGreen);
                StatusTextBlock.Text = "Message sent successfully";
            }
            else
            {
                AddMessageToChat("⚠️ System", "Failed to get response from agent", Colors.Orange);
                StatusTextBlock.Text = "Failed to send message";
            }
        }
        catch (Exception ex)
        {
            AddMessageToChat("❌ Error", $"Error: {ex.Message}", Colors.Red);
            StatusTextBlock.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void AddMessageToChat(string sender, string message, Color color)
    {
        var messagePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        
        var senderBlock = new TextBlock
        {
            Text = sender,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 0, 5)
        };
        
        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Colors.White),
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        };
        
        var border = new Border
        {
            Child = messageBlock,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        };
        
        messagePanel.Children.Add(senderBlock);
        messagePanel.Children.Add(border);
        
        ChatMessagesPanel.Children.Add(messagePanel);
        
        ChatScrollViewer.ScrollToEnd();
    }

    private async void AgentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AgentsListBox.SelectedIndex >= 0 && AgentsListBox.SelectedIndex < _agents.Count)
        {
            _selectedAgent = _agents[AgentsListBox.SelectedIndex];
            _currentSessionId = Guid.NewGuid().ToString();
            SendButton.IsEnabled = true;
            
            ChatMessagesPanel.Children.Clear();
            AddMessageToChat("🤖 System", $"Connected to {_selectedAgent.Name}. How can I help you?", Colors.MediumSeaGreen);
            
            await CreateConversationAsync();
            await LoadGoalsAsync();
        }
    }

    private async void CreateAgent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CreateAgentDialog();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var request = new
                {
                    Name = dialog.AgentName,
                    Description = dialog.AgentDescription,
                    Instructions = dialog.AgentInstructions
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/agents", content);
                
                if (response.IsSuccessStatusCode)
                {
                    await LoadAgentsAsync();
                    StatusTextBlock.Text = "Agent created successfully";
                }
                else
                {
                    ShowErrorMessage("Failed to create agent");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error creating agent: {ex.Message}");
            }
        }
    }

    private async void CreateGoal_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAgent == null)
        {
            ShowErrorMessage("Please select an agent first");
            return;
        }

        var dialog = new CreateGoalDialog();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var request = new
                {
                    AgentId = _selectedAgent.Id,
                    Title = dialog.GoalTitle,
                    Description = dialog.GoalDescription,
                    Priority = dialog.GoalPriority
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/goals", content);
                
                if (response.IsSuccessStatusCode)
                {
                    await LoadGoalsAsync();
                    StatusTextBlock.Text = "Goal created successfully";
                }
                else
                {
                    ShowErrorMessage("Failed to create goal");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error creating goal: {ex.Message}");
            }
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Settings dialog would be implemented here", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("MiniBrain Desktop v1.0\n\nAgentic AI Assistant with Claude integration\nBuilt with C#, WPF, and ASP.NET Core", 
                       "About MiniBrain", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowErrorMessage(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpClient?.Dispose();
        base.OnClosed(e);
    }
}