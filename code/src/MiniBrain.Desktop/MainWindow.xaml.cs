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
        try
        {
            InitializeComponent();
            
            // Initialize logger
            _logger = App.LoggerFactory?.CreateLogger<MainWindow>() ?? 
                throw new InvalidOperationException("Logger factory not available");
            _logger.LogInformation("Initializing MainWindow");
            Console.WriteLine("INFO: Initializing MainWindow");
            
            // Configure HttpClient with timeout settings
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://localhost:5089");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            _logger.LogInformation("HTTP client initialized with base address: {BaseAddress} and timeout: {Timeout}s", 
                _httpClient.BaseAddress, _httpClient.Timeout.TotalSeconds);
            Console.WriteLine($"INFO: HTTP client initialized with base address: {_httpClient.BaseAddress} and timeout: {_httpClient.Timeout.TotalSeconds}s");
            
            // Add keyboard shortcuts for copy/paste
            var copyCommand = new RoutedCommand();
            var pasteCommand = new RoutedCommand();
            
            copyCommand.InputGestures.Add(new KeyGesture(Key.C, ModifierKeys.Control));
            pasteCommand.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control));
            
            CommandBindings.Add(new CommandBinding(copyCommand, CopyExecuted));
            CommandBindings.Add(new CommandBinding(pasteCommand, PasteExecuted));
            
            Loaded += MainWindow_Loaded;
            
            Console.WriteLine("INFO: MainWindow constructor completed successfully");
        }
        catch (Exception ex)
        {
            var errorMsg = $"CRITICAL ERROR in MainWindow constructor: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            if (_logger != null)
            {
                _logger.LogCritical(ex, "Failed to initialize MainWindow");
            }
            
            MessageBox.Show($"Failed to initialize application: {ex.Message}", "Critical Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("MainWindow loaded, initializing application");
            Console.WriteLine("INFO: MainWindow loaded, initializing application");
            
            _logger.LogInformation("Checking API connection...");
            Console.WriteLine("INFO: Checking API connection...");
            await CheckApiConnectionAsync();
            
            // NOTE: Agent loading currently disabled - using direct Claude API calls
            // Agent and goal features are not fully implemented yet
            // _logger.LogInformation("Loading agents...");
            // Console.WriteLine("INFO: Loading agents...");
            // await LoadAgentsAsync();
            
            // _logger.LogInformation("Loading goals...");
            // Console.WriteLine("INFO: Loading goals...");
            // await LoadGoalsAsync();
            
            _logger.LogInformation("Application initialization complete - Direct Claude API mode");
            Console.WriteLine("INFO: Application initialization complete - Direct Claude API mode");
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in MainWindow_Loaded: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Failed to initialize application components");
            
            //ShowErrorMessage($"Failed to initialize application: {ex.Message}");
            
            try
            {
                StatusTextBlock.Text = "Application initialization failed";
                ApiStatusTextBlock.Text = "🔴 Initialization Error";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
            catch (Exception uiEx)
            {
                Console.WriteLine($"ERROR: Could not update UI after initialization failure: {uiEx.Message}");
                _logger.LogError(uiEx, "Could not update UI after initialization failure");
            }
        }
    }

    private async Task CheckApiConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Checking API connection to {BaseAddress}", _httpClient.BaseAddress);
            Console.WriteLine($"INFO: Checking API connection to {_httpClient.BaseAddress}");
            
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
                    Console.WriteLine("INFO: API connection successful");
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
                    Console.WriteLine($"WARNING: API returned status code {response.StatusCode}. Response: {content}");
                    
                    ApiStatusTextBlock.Text = "🔴 API: Error";
                    ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    StatusTextBlock.Text = $"API Error: {response.StatusCode}";
                    
                    // Display error message to help diagnose
                    //ShowErrorMessage($"API returned status code {response.StatusCode}. Response: {content}");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("API connection timed out");
                Console.WriteLine("ERROR: API connection timed out");
                ApiStatusTextBlock.Text = "🔴 API: Timeout";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusTextBlock.Text = "API Connection Timeout";
                //ShowErrorMessage($"API Connection Timeout: The API at {_httpClient.BaseAddress} did not respond within the timeout period. Please check if the API is running.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed");
                Console.WriteLine($"ERROR: HTTP request failed: {ex.Message}");
                ApiStatusTextBlock.Text = "🔴 API: Not Reachable";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusTextBlock.Text = $"API not reachable: {ex.Message}";
                //ShowErrorMessage($"API Connection Failed: {ex.Message}\n\nThis usually means the API is not running or is running on a different port. Please check that the API is running on port 5089.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to API");
                Console.WriteLine($"ERROR: Failed to connect to API: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                ApiStatusTextBlock.Text = "🔴 API: Error";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusTextBlock.Text = $"API Error: {ex.Message}";
                //ShowErrorMessage($"API Connection Failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"CRITICAL ERROR in CheckApiConnectionAsync: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogCritical(ex, "Critical error in API connection check");
            
            try
            {
                ApiStatusTextBlock.Text = "🔴 Critical Error";
                ApiStatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusTextBlock.Text = "Critical error checking API";
            }
            catch (Exception uiEx)
            {
                Console.WriteLine($"ERROR: Could not update UI after critical error: {uiEx.Message}");
            }
        }
    }

    private async Task LoadAgentsAsync()
    {
        try
        {
            _logger.LogInformation("Loading agents from API");
            Console.WriteLine("INFO: Loading agents from API");
            
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
                    Console.WriteLine($"INFO: Loaded {_agents.Count} agents");
                    _logger.LogDebug("Agents: {Agents}", string.Join(", ", _agents.Select(a => $"{a.Id}:{a.Name}")));
                    
                    AgentsListBox.ItemsSource = _agents.Select(a => $"🤖 {a.Name}").ToList();
                    
                    if (_agents.Any() && _selectedAgent == null)
                    {
                        _selectedAgent = _agents.First();
                        _logger.LogInformation("Selected first agent: {AgentId} ({AgentName})", 
                            _selectedAgent.Id, _selectedAgent.Name);
                        Console.WriteLine($"INFO: Selected first agent: {_selectedAgent.Id} ({_selectedAgent.Name})");
                        
                        AgentsListBox.SelectedIndex = 0;
                        SendButton.IsEnabled = true;
                        await CreateConversationAsync();
                    }
                    else if (!_agents.Any())
                    {
                        _logger.LogWarning("No agents found in the system");
                        Console.WriteLine("WARNING: No agents found in the system");
                    }
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to load agents. Status code: {StatusCode}, Response: {Response}", 
                        response.StatusCode, content);
                    Console.WriteLine($"WARNING: Failed to load agents. Status code: {response.StatusCode}, Response: {content}");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize agents JSON response");
                Console.WriteLine($"ERROR: Failed to deserialize agents JSON: {ex.Message}");
                //ShowErrorMessage($"Failed to parse agents data: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed while loading agents");
                Console.WriteLine($"ERROR: HTTP request failed while loading agents: {ex.Message}");
                //ShowErrorMessage($"Failed to load agents - connection error: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timed out while loading agents");
                Console.WriteLine($"ERROR: Request timed out while loading agents: {ex.Message}");
                //ShowErrorMessage("Failed to load agents - request timed out");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"CRITICAL ERROR in LoadAgentsAsync: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogCritical(ex, "Critical error while loading agents");
            //ShowErrorMessage($"Critical error loading agents: {ex.Message}");
        }
    }

    private async Task LoadGoalsAsync()
    {
        try
        {
            if (_selectedAgent != null)
            {
                Console.WriteLine($"INFO: Loading goals for agent {_selectedAgent.Id} ({_selectedAgent.Name})");
                var response = await _httpClient.GetAsync($"/api/goals/agent/{_selectedAgent.Id}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _goals = JsonSerializer.Deserialize<List<Goal>>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<Goal>();
                    
                    GoalsListBox.ItemsSource = _goals.Select(g => $"🎯 {g.Title} ({g.Status})").ToList();
                    Console.WriteLine($"INFO: Loaded {_goals.Count} goals for agent {_selectedAgent.Name}");
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"WARNING: Failed to load goals. Status: {response.StatusCode}, Response: {content}");
                    _logger.LogWarning("Failed to load goals. Status code: {StatusCode}, Response: {Response}", 
                        response.StatusCode, content);
                }
            }
            else
            {
                Console.WriteLine("INFO: No agent selected, skipping goal loading");
            }
        }
        catch (JsonException ex)
        {
            var errorMsg = $"ERROR: Failed to deserialize goals JSON: {ex.Message}";
            Console.WriteLine(errorMsg);
            _logger.LogError(ex, "Failed to deserialize goals JSON response");
            //($"Failed to parse goals data: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"ERROR: HTTP request failed while loading goals: {ex.Message}";
            Console.WriteLine(errorMsg);
            _logger.LogError(ex, "HTTP request failed while loading goals");
            //ShowErrorMessage($"Failed to load goals - connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in LoadGoalsAsync: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Exception occurred while loading goals");
            //ShowErrorMessage($"Failed to load goals: {ex.Message}");
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
            //ShowErrorMessage($"Failed to create conversation: {ex.Message}");
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SendMessageAsync();
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in SendButton_Click: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in send button click handler");
            //ShowErrorMessage($"Error sending message: {ex.Message}");
            
            try
            {
                SendButton.IsEnabled = true;
                StatusTextBlock.Text = "Error occurred";
            }
            catch (Exception uiEx)
            {
                Console.WriteLine($"ERROR: Could not reset UI after send button error: {uiEx.Message}");
            }
        }
    }

    private async void ChatInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ChatInputTextBox.Text))
            {
                await SendMessageAsync();
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in ChatInputTextBox_KeyDown: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in chat input key down handler");
           // ShowErrorMessage($"Error processing key input: {ex.Message}");
        }
    }

    private async Task SendMessageAsync()
    {
        // NOTE: Currently bypassing agent system and making direct Claude API calls
        // Agent features are not implemented yet, so we send messages directly
        _logger.LogInformation("Starting SendMessageAsync - Direct Claude API mode");
        
        // Removed agent check - no longer required for direct API calls
        // if (_selectedAgent == null) { return; }
        
        if (string.IsNullOrWhiteSpace(ChatInputTextBox.Text))
        {
            _logger.LogWarning("Message text is empty, cannot send message");
            return;
        }

        var userMessage = ChatInputTextBox.Text.Trim();
        ChatInputTextBox.Text = "";
        
        // Direct API call without agent dependency
        _logger.LogInformation("Sending direct message to Claude API: {UserMessage}", userMessage);
        _logger.LogDebug("Current session ID: {SessionId}", _currentSessionId);
        
        AddMessageToChat("👤 You", userMessage, Colors.DodgerBlue);
        AddMessageToChat("🔍 System", "Sending direct request to Claude API (no agent)", Colors.Gray);
        
        SendButton.IsEnabled = false;
        StatusTextBlock.Text = "Processing message...";

        try
        {
            // Simplified request for direct Claude API call
            // Using a special UUID that indicates "direct Claude mode" 
            var directClaudeAgentId = new Guid("00000000-0000-0000-0000-000000000001");
            
            var request = new
            {
                SessionId = _currentSessionId,
                Message = userMessage,
                AgentId = directClaudeAgentId  // Direct Claude API agent ID
            };

            var json = JsonSerializer.Serialize(request);
            _logger.LogDebug("Request payload: {Json}", json);
            AddMessageToChat("📤 API Request", $"Payload: {json}", Colors.DarkGray);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _logger.LogDebug("Sending POST request to /api/conversations/message");
            
            var startTime = DateTime.Now;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            _logger.LogDebug("Using timeout of {Timeout} seconds for Claude API call", 60);
            
            AddMessageToChat("⏳ Processing", "Waiting for Claude API response...", Colors.Orange);
            
            var response = await _httpClient.PostAsync("/api/conversations/message", content, cts.Token);
            var duration = DateTime.Now - startTime;
            
            _logger.LogDebug("Response received in {Duration}ms with status code {StatusCode}", 
                duration.TotalMilliseconds, response.StatusCode);
            
            AddMessageToChat("📥 API Response", $"Status: {response.StatusCode}, Duration: {duration.TotalMilliseconds}ms", Colors.DarkGray);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Raw response content: {ResponseContent}", responseContent);
            
            AddMessageToChat("📋 Raw Response", responseContent, Colors.DarkSlateGray);
            
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
                        
                        // Direct Claude response without agent name
                        AddMessageToChat("🤖 Claude", botResponse, Colors.MediumSeaGreen);
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
        try
        {
            var messagePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            
            var contextMenu = new ContextMenu();
            var copyMenuItem = new MenuItem { Header = "Copy" };
            copyMenuItem.Click += (s, e) => 
            {
                try
                {
                    Clipboard.SetText(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Failed to copy to clipboard: {ex.Message}");
                    _logger.LogError(ex, "Failed to copy message to clipboard");
                }
            };
            contextMenu.Items.Add(copyMenuItem);
            
            var senderBlock = new TextBlock
            {
                Text = sender,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 0, 5),
                ContextMenu = contextMenu
            };
            
            var messageBlock = new TextBox
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                IsTabStop = false,
                ContextMenu = contextMenu
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
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in AddMessageToChat: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Failed to add message to chat UI");
            
            try
            {
                var fallbackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                var fallbackText = new TextBlock
                {
                    Text = $"[ERROR DISPLAYING MESSAGE] {sender}: {message}",
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap
                };
                fallbackPanel.Children.Add(fallbackText);
                ChatMessagesPanel.Children.Add(fallbackPanel);
                ChatScrollViewer.ScrollToEnd();
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"CRITICAL ERROR: Could not add fallback message to chat: {fallbackEx.Message}");
            }
        }
    }

    private async void AgentsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (AgentsListBox.SelectedIndex >= 0 && AgentsListBox.SelectedIndex < _agents.Count)
            {
                _selectedAgent = _agents[AgentsListBox.SelectedIndex];
                _currentSessionId = Guid.NewGuid().ToString();
                SendButton.IsEnabled = true;
                
                Console.WriteLine($"INFO: Agent selection changed to {_selectedAgent.Name} (ID: {_selectedAgent.Id})");
                _logger.LogInformation("Agent selection changed to {AgentName} (ID: {AgentId})", 
                    _selectedAgent.Name, _selectedAgent.Id);
                
                ChatMessagesPanel.Children.Clear();
                AddMessageToChat("🤖 System", $"Connected to {_selectedAgent.Name}. How can I help you?", Colors.MediumSeaGreen);
                
                await CreateConversationAsync();
                await LoadGoalsAsync();
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in AgentsListBox_SelectionChanged: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in agent selection changed handler");
            //ShowErrorMessage($"Error changing agent selection: {ex.Message}");
            
            try
            {
                SendButton.IsEnabled = false;
                StatusTextBlock.Text = "Error selecting agent";
            }
            catch (Exception uiEx)
            {
                Console.WriteLine($"ERROR: Could not reset UI after agent selection error: {uiEx.Message}");
            }
        }
    }

    private async void CreateAgent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine("INFO: Create agent dialog requested");
            var dialog = new CreateAgentDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Console.WriteLine($"INFO: Creating new agent: {dialog.AgentName}");
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
                        Console.WriteLine("INFO: Agent created successfully");
                        await LoadAgentsAsync();
                        StatusTextBlock.Text = "Agent created successfully";
                    }
                    else
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"ERROR: Failed to create agent. Status: {response.StatusCode}, Response: {responseContent}");
                        _logger.LogError($"Failed to create agent. Status: {response.StatusCode}");
                        //ShowErrorMessage($"Failed to create agent. Status: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"ERROR: HTTP request failed while creating agent: {ex.Message}");
                    _logger.LogError(ex, "HTTP request failed while creating agent");
                    //ShowErrorMessage($"Network error creating agent: {ex.Message}");
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"ERROR: JSON serialization failed while creating agent: {ex.Message}");
                    _logger.LogError(ex, "JSON serialization failed while creating agent");
                    //ShowErrorMessage($"Data format error creating agent: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in CreateAgent_Click: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in create agent click handler");
            //ShowErrorMessage($"Error creating agent: {ex.Message}");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine("INFO: Settings dialog requested");
            MessageBox.Show("Settings dialog would be implemented here", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in Settings_Click: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in settings click handler");
            //ShowErrorMessage($"Error opening settings: {ex.Message}");
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine("INFO: About dialog requested");
            MessageBox.Show("MiniBrain Desktop v1.0\n\nAgentic AI Assistant with Claude integration\nBuilt with C#, WPF, and ASP.NET Core", 
                           "About MiniBrain", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in About_Click: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in about click handler");
            //ShowErrorMessage($"Error opening about dialog: {ex.Message}");
        }
    }

    private void ShowErrorMessage(string message)
    {
        try
        {
            _logger.LogError("ERROR: {ErrorMessage}", message);
            Console.WriteLine($"ERROR: {message}");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL ERROR: Could not show error message '{message}'. Exception: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            try
            {
                if (_logger != null)
                {
                    _logger.LogCritical(ex, "Failed to show error message: {OriginalMessage}", message);
                }
            }
            catch
            {
                Console.WriteLine("CRITICAL ERROR: Logger also failed when trying to log error message failure");
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            Console.WriteLine("INFO: MainWindow closing, disposing resources");
            _httpClient?.Dispose();
            base.OnClosed(e);
            Console.WriteLine("INFO: MainWindow closed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR during OnClosed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            try
            {
                _logger?.LogError(ex, "Error during window close");
            }
            catch
            {
                Console.WriteLine("Could not log close error");
            }
        }
    }

    private void CopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (Keyboard.FocusedElement is TextBox textBox && !string.IsNullOrEmpty(textBox.SelectedText))
            {
                Clipboard.SetText(textBox.SelectedText);
                Console.WriteLine("INFO: Text copied to clipboard (selected text)");
            }
            else if (Keyboard.FocusedElement is TextBox tb && string.IsNullOrEmpty(tb.SelectedText))
            {
                Clipboard.SetText(tb.Text);
                Console.WriteLine("INFO: Text copied to clipboard (full text)");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in CopyExecuted: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in copy command execution");
            //ShowErrorMessage($"Error copying text: {ex.Message}");
        }
    }
    
    private void PasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            if (Keyboard.FocusedElement is TextBox textBox)
            {
                var clipboardText = Clipboard.GetText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    textBox.SelectedText = clipboardText;
                    Console.WriteLine("INFO: Text pasted from clipboard");
                }
                else
                {
                    Console.WriteLine("INFO: Clipboard is empty, nothing to paste");
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"ERROR in PasteExecuted: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in paste command execution");
            //ShowErrorMessage($"Error pasting text: {ex.Message}");
        }
    }
}