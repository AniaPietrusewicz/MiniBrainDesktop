using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using MiniBrain.Core.Models;

namespace MiniBrain.Desktop;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MainWindow> _logger;
    private string _currentSessionId = Guid.NewGuid().ToString();
    private Agent? _selectedAgent;
    private List<Agent> _agents = new();
    private List<Goal> _goals = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize logger
        _logger = App.LoggerFactory?.CreateLogger<MainWindow>() ?? 
            throw new InvalidOperationException("Logger factory not available");
        _logger.LogInformation("Initializing MainWindow");
        
        // Configure HttpClient with timeout settings
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost:5000");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _logger.LogInformation("HTTP client initialized with base address: {BaseAddress} and timeout: {Timeout}s", 
            _httpClient.BaseAddress, _httpClient.Timeout.TotalSeconds);
        
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("MainWindow loaded, initializing application");
        
        _logger.LogInformation("Checking API connection...");
        await CheckApiConnectionAsync();
        
        _logger.LogInformation("Loading agents...");
        await LoadAgentsAsync();
        
        _logger.LogInformation("Loading goals...");
        await LoadGoalsAsync();
        
        _logger.LogInformation("Application initialization complete");
    }

    private async Task CheckApiConnectionAsync()
    {
        _logger.LogInformation("Checking API connection to {BaseAddress}", _httpClient.BaseAddress);
        
        try
        {
            // First, test the base connectivity
            _logger.LogDebug("Testing base connectivity...");
            var baseUrlParts = _httpClient.BaseAddress.ToString().Split('/');
            var hostWithPort = baseUrlParts[2]; // Should be "localhost:5089" format
            
            _logger.LogDebug("Attempting to ping host: {Host}", hostWithPort);
            
            // Now test the specific API endpoint
            var requestUri = "/api/agents";
            _logger.LogDebug("Sending request to {RequestUri}", requestUri);
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5 second timeout for diagnostic check
            var response = await _httpClient.GetAsync(requestUri, cts.Token);
            _logger.LogDebug("API response status code: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API response: {Content}", content.Length > 100 ? content.Substring(0, 100) + "..." : content);
                
                _logger.LogInformation("API connection successful");
                ApiStatusTextBlock.Text = "🟢 Claude API: Connected";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.LightGreen);
                DbStatusTextBlock.Text = "🟢 Database: Connected";
                DbStatusTextBlock.Foreground = new SolidColorBrush(Colors.LightGreen);
                StatusTextBlock.Text = "Connected to MiniBrain API";
            }
            else
            {
                _logger.LogWarning("API connection returned non-success status code: {StatusCode}", response.StatusCode);
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API response content: {Content}", content);
                
                ApiStatusTextBlock.Text = "🔴 API: Error";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusTextBlock.Text = $"API Error: {response.StatusCode}";
                
                // Display error message to help diagnose
                ShowErrorMessage($"API returned status code {response.StatusCode}. Response: {content}");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("API connection timed out");
            ApiStatusTextBlock.Text = "🔴 API: Timeout";
            ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            StatusTextBlock.Text = "API Connection Timeout";
            ShowErrorMessage($"API Connection Timeout: The API at {_httpClient.BaseAddress} did not respond within the timeout period. Please check if the API is running.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            ApiStatusTextBlock.Text = "🔴 API: Not Reachable";
            ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            StatusTextBlock.Text = $"API not reachable: {ex.Message}";
            ShowErrorMessage($"API Connection Failed: {ex.Message}\n\nThis usually means the API is not running or is running on a different port. Please check that the API is running on port 5089.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to API");
            ApiStatusTextBlock.Text = "🔴 API: Error";
            ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            StatusTextBlock.Text = $"API Error: {ex.Message}";
            ShowErrorMessage($"API Connection Failed: {ex.Message}");
        }
    }

    private async Task LoadAgentsAsync()
    {
        _logger.LogInformation("Loading agents from API");
        
        try
        {
            _logger.LogDebug("Sending GET request to /api/agents");
            var response = await _httpClient.GetAsync("/api/agents");
            _logger.LogDebug("Response status code: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Received JSON: {Json}", json);
                
                _agents = JsonSerializer.Deserialize<List<Agent>>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                }) ?? new List<Agent>();
                
                _logger.LogInformation("Loaded {AgentCount} agents", _agents.Count);
                _logger.LogDebug("Agents: {Agents}", string.Join(", ", _agents.Select(a => $"{a.Id}:{a.Name}")));
                
                AgentsListBox.ItemsSource = _agents.Select(a => $"🤖 {a.Name}").ToList();
                
                if (_agents.Any() && _selectedAgent == null)
                {
                    _selectedAgent = _agents.First();
                    _logger.LogInformation("Selected first agent: {AgentId} ({AgentName})", 
                        _selectedAgent.Id, _selectedAgent.Name);
                    
                    AgentsListBox.SelectedIndex = 0;
                    SendButton.IsEnabled = true;
                    await CreateConversationAsync();
                }
                else if (!_agents.Any())
                {
                    _logger.LogWarning("No agents found in the system");
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to load agents. Status code: {StatusCode}, Response: {Response}", 
                    response.StatusCode, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while loading agents");
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
        _logger.LogInformation("Creating new conversation");
        
        if (_selectedAgent == null)
        {
            _logger.LogWarning("No agent selected, cannot create conversation");
            return;
        }
        
        _logger.LogInformation("Creating conversation with agent: {AgentId} ({AgentName})", 
            _selectedAgent.Id, _selectedAgent.Name);
        _logger.LogDebug("Session ID: {SessionId}", _currentSessionId);
        
        try
        {
            var request = new
            {
                AgentId = _selectedAgent.Id,
                SessionId = _currentSessionId
            };
            
            var json = JsonSerializer.Serialize(request);
            _logger.LogDebug("Request payload: {Json}", json);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _logger.LogDebug("Sending POST request to /api/conversations");
            
            var response = await _httpClient.PostAsync("/api/conversations", content);
            _logger.LogDebug("Response status code: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Conversation created successfully");
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to create conversation. Status code: {StatusCode}, Response: {Response}", 
                    response.StatusCode, responseContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating conversation");
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
        _logger.LogInformation("Starting SendMessageAsync");
        
        if (_selectedAgent == null)
        {
            _logger.LogWarning("No agent selected, cannot send message");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(ChatInputTextBox.Text))
        {
            _logger.LogWarning("Message text is empty, cannot send message");
            return;
        }

        var userMessage = ChatInputTextBox.Text.Trim();
        ChatInputTextBox.Text = "";
        
        _logger.LogInformation("Sending message: {UserMessage} to agent: {AgentId} ({AgentName})", 
            userMessage, _selectedAgent.Id, _selectedAgent.Name);
        _logger.LogDebug("Current session ID: {SessionId}", _currentSessionId);
        
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
            _logger.LogDebug("Request payload: {Json}", json);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _logger.LogDebug("Sending POST request to /api/conversations/message");
            
            var startTime = DateTime.Now;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 second timeout
            _logger.LogDebug("Using timeout of {Timeout} seconds for Claude API call", 60);
            
            var response = await _httpClient.PostAsync("/api/conversations/message", content, cts.Token);
            var duration = DateTime.Now - startTime;
            
            _logger.LogDebug("Response received in {Duration}ms with status code {StatusCode}", 
                duration.TotalMilliseconds, response.StatusCode);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Raw response content: {ResponseContent}", responseContent);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Message sent successfully");
                
                try 
                {
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("response", out var responseElement))
                    {
                        string? botResponse = responseElement.GetString();
                        
                        _logger.LogInformation("Bot response received");
                        
                        if (string.IsNullOrEmpty(botResponse))
                        {
                            _logger.LogWarning("Bot response was empty or null");
                            botResponse = "No response received";
                        }
                        
                        AddMessageToChat($"🤖 {_selectedAgent.Name}", botResponse, Colors.MediumSeaGreen);
                        StatusTextBlock.Text = "Message sent successfully";
                    }
                    else
                    {
                        _logger.LogWarning("Response did not contain 'response' property");
                        AddMessageToChat("⚠️ System", "Failed to parse agent response", Colors.Orange);
                        StatusTextBlock.Text = "Failed to parse response";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing API response");
                    AddMessageToChat("⚠️ System", $"Error processing response: {ex.Message}", Colors.Orange);
                    StatusTextBlock.Text = "Error processing response";
                }
            }
            else
            {
                _logger.LogWarning("API returned error status code: {StatusCode}", response.StatusCode);
                _logger.LogWarning("Error response: {ResponseContent}", responseContent);
                
                AddMessageToChat("⚠️ System", "Failed to get response from agent", Colors.Orange);
                StatusTextBlock.Text = "Failed to send message";
                
                _logger.LogInformation("Attempting to get more error details...");
                try
                {
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var root = jsonDoc.RootElement;
                    
                    if (root.TryGetProperty("error", out var errorElement))
                    {
                        string? errorMessage = errorElement.GetString();
                        string errorText = errorMessage ?? "Unknown error";
                        
                        _logger.LogInformation("API error message: {ErrorMessage}", errorText);
                        AddMessageToChat("⚠️ Error Details", errorText, Colors.OrangeRed);
                    }
                }
                catch
                {
                    _logger.LogWarning("Could not extract error details from response");
                }
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("API request timed out");
            AddMessageToChat("❌ Timeout Error", "The request to Claude API timed out. This could indicate that Claude is taking too long to process your question about the weather. Try asking a simpler question.", Colors.Red);
            StatusTextBlock.Text = "Request timed out";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            AddMessageToChat("❌ Connection Error", $"Error: {ex.Message}\n\nPlease check if the API is running and accessible.", Colors.Red);
            StatusTextBlock.Text = $"Connection error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending message");
            AddMessageToChat("❌ Error", $"Error: {ex.Message}", Colors.Red);
            StatusTextBlock.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _logger.LogInformation("SendMessageAsync completed");
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
        _logger.LogError("ERROR: {ErrorMessage}", message);
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpClient?.Dispose();
        base.OnClosed(e);
    }
}