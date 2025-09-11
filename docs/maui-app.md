# CDC MAUI Application Documentation

## Overview

The `cdc-maui` project is a cross-platform desktop and mobile application built with .NET Multi-platform App UI (.NET MAUI). It provides a graphical user interface for managing CDC operations, offering an alternative to the command-line interface for users who prefer visual interaction.

## Project Structure

```
cdc-maui/
├── Platforms/                    # Platform-specific implementations
│   ├── Android/                  # Android-specific code
│   ├── iOS/                      # iOS-specific code
│   ├── MacCatalyst/              # macOS-specific code
│   ├── Tizen/                    # Tizen-specific code (optional)
│   └── Windows/                  # Windows-specific code
├── Resources/                    # Application resources
│   ├── AppIcon/                  # Application icons
│   ├── Fonts/                    # Custom fonts
│   ├── Images/                   # Image assets
│   ├── Raw/                      # Raw assets
│   ├── Splash/                   # Splash screen
│   └── Styles/                   # XAML styles and themes
├── App.xaml                      # Application-level resources
├── App.xaml.cs                   # Application lifecycle code
├── AppShell.xaml                 # Shell navigation structure
├── AppShell.xaml.cs              # Shell code-behind
├── MainPage.xaml                 # Main page UI definition
├── MainPage.xaml.cs              # Main page logic
├── MauiProgram.cs                # Application configuration
└── cdc-maui.csproj              # Project configuration
```

## Target Platforms

The application is configured to support multiple platforms:

### Supported Platforms

- **Android** (API 21+) - Android 5.0 and later
- **iOS** (14.2+) - iPhone and iPad
- **macOS** (14.0+) - via Mac Catalyst
- **Windows** (10.0.17763.0+) - Windows 10 version 1809 and later

### Optional Platforms

- **Tizen** (6.5+) - Samsung smart devices (commented out by default)

## Application Configuration

### MauiProgram.cs

The application entry point configures the MAUI app:

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
```

### Dependencies

The application references the core CDC library:

```xml
<ProjectReference Include="..\cdc-lib\cdc-lib.csproj" />
```

## User Interface

### Application Shell

The app uses a simplified shell structure with flyout disabled:

```xml
<Shell x:Class="cdc_maui.AppShell"
       Shell.FlyoutBehavior="Disabled">
    <ShellContent
        Title="Home"
        ContentTemplate="{DataTemplate local:MainPage}"
        Route="MainPage" />
</Shell>
```

### Main Page Layout

The main page provides a clean, centered interface for CDC operations:

#### UI Components

**System Selection Picker:**

```xml
<Picker x:Name="selectedDatabase"
        Title="Select a system"
        HorizontalOptions="Start"
        WidthRequest="200">
    <Picker.ItemsSource>
        <x:Array Type="{x:Type x:String}">
            <x:String>Local Docker V3</x:String>
            <x:String>Local Docker CRM</x:String>
            <x:String>Remote Docker CRM</x:String>
            <x:String>Test System</x:String>
        </x:Array>
    </Picker.ItemsSource>
</Picker>
```

**Profile Name Entry:**

```xml
<HorizontalStackLayout WidthRequest="200" HorizontalOptions="StartAndExpand">
    <Label Text="Name"
           Padding="0,0,10,0"
           SemanticProperties.HeadingLevel="Level1"
           FontSize="26"
           HorizontalOptions="Center" />
    <Entry AutomationId="profileEntry" WidthRequest="300"></Entry>
</HorizontalStackLayout>
```

**Record Button:**

```xml
<Button x:Name="recordButton"
        AutomationId="recordButton"
        Text="Record"
        SemanticProperties.Hint="Turns on change data capture"
        Clicked="OnRecordClicked"
        HorizontalOptions="Start" />
```

### Current Functionality

#### System Selection

The application provides predefined system options:

- **Local Docker V3** - Local Docker container version 3
- **Local Docker CRM** - Local Docker CRM system
- **Remote Docker CRM** - Remote Docker CRM system
- **Test System** - Generic test system

#### Profile Management

- **Profile Name Entry** - Users can specify a name for their CDC profile
- **Record Button** - Initiates CDC recording (currently placeholder)

#### Event Handling

```csharp
private void OnRecordClicked(object sender, EventArgs e)
{
    // Currently empty - planned for CDC operations
}
```

## Planned Features

### Enhanced UI Components

#### Status Dashboard

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <!-- Status Indicators -->
    <StackLayout Grid.Row="0" Orientation="Horizontal">
        <Label Text="CDC Status:" />
        <Label x:Name="cdcStatusLabel" Text="Disabled" TextColor="Red" />
        <Label Text="Container:" />
        <Label x:Name="containerStatusLabel" Text="Stopped" TextColor="Red" />
    </StackLayout>

    <!-- Operation Controls -->
    <StackLayout Grid.Row="1" Orientation="Horizontal">
        <Button Text="Initialize CDC" Clicked="OnInitializeCdc" />
        <Button Text="Reset Database" Clicked="OnResetDatabase" />
        <Button Text="Generate Profile" Clicked="OnGenerateProfile" />
    </StackLayout>

    <!-- Results Area -->
    <ScrollView Grid.Row="2">
        <Label x:Name="resultsLabel" Text="Ready..." />
    </ScrollView>
</Grid>
```

#### Profile Comparison View

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <!-- Left Profile -->
    <StackLayout Grid.Column="0">
        <Label Text="Baseline Profile" FontSize="18" />
        <Button Text="Load Profile" Clicked="OnLoadLeftProfile" />
        <ListView x:Name="leftProfileList" />
    </StackLayout>

    <!-- Right Profile -->
    <StackLayout Grid.Column="1">
        <Label Text="Comparison Profile" FontSize="18" />
        <Button Text="Load Profile" Clicked="OnLoadRightProfile" />
        <ListView x:Name="rightProfileList" />
    </StackLayout>
</Grid>
```

### Enhanced Code-Behind

#### CDC Operations Integration

```csharp
public partial class MainPage : ContentPage
{
    private SimpleDac _dac;
    private ILogger _logger;

    public MainPage()
    {
        InitializeComponent();
        InitializeCdcServices();
    }

    private void InitializeCdcServices()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<MainPage>();

        // Connection string from selected system
        var connectionString = GetConnectionStringForSelectedSystem();
        _dac = new SimpleDac(connectionString, _logger);
    }

    private async void OnInitializeCdc(object sender, EventArgs e)
    {
        try
        {
            recordButton.IsEnabled = false;
            recordButton.Text = "Initializing...";

            await Task.Run(() =>
            {
                CdcDataUtilities.EnableCdcOnDatabase(_dac);
                var tables = CdcDataUtilities.GetTables(_dac);
                CdcDataUtilities.EnableTableCdc(_dac, tables, _logger);
            });

            await DisplayAlert("Success", "CDC initialized successfully", "OK");
            UpdateCdcStatus(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to initialize CDC: {ex.Message}", "OK");
        }
        finally
        {
            recordButton.IsEnabled = true;
            recordButton.Text = "Record";
        }
    }

    private async void OnGenerateProfile(object sender, EventArgs e)
    {
        try
        {
            var profileName = profileEntry.Text;
            if (string.IsNullOrWhiteSpace(profileName))
            {
                await DisplayAlert("Error", "Please enter a profile name", "OK");
                return;
            }

            var tables = CdcDataUtilities.GetTables(_dac);
            var profile = await Task.Run(() =>
                CdcDataUtilities.BuildNetProfile(_dac, tables, _logger));

            var json = profile.ToJson(true);
            var fileName = $"{profileName}-{DateTime.Now:yyyyMMdd-HHmmss}.json";

            // Save to app data directory
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);

            await DisplayAlert("Success", $"Profile saved as {fileName}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate profile: {ex.Message}", "OK");
        }
    }

    private string GetConnectionStringForSelectedSystem()
    {
        return selectedDatabase.SelectedItem?.ToString() switch
        {
            "Local Docker V3" => "Server=localhost,1433;Database=TestDB_V3;User Id=sa;Password=YourPassword;",
            "Local Docker CRM" => "Server=localhost,1434;Database=CRM;User Id=sa;Password=YourPassword;",
            "Remote Docker CRM" => "Server=remote-server,1433;Database=CRM;User Id=sa;Password=YourPassword;",
            "Test System" => "Server=test-server;Database=TestDB;Integrated Security=true;",
            _ => throw new InvalidOperationException("Please select a system")
        };
    }

    private void UpdateCdcStatus(bool enabled)
    {
        cdcStatusLabel.Text = enabled ? "Enabled" : "Disabled";
        cdcStatusLabel.TextColor = enabled ? Colors.Green : Colors.Red;
    }
}
```

## Styling and Theming

### Resource Structure

The application uses XAML resource dictionaries for consistent styling:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
            <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

### Custom Styles

**Colors.xaml** - Application color palette
**Styles.xaml** - Control styles and templates

### Accessibility Features

- **SemanticProperties.HeadingLevel** - Screen reader support
- **SemanticProperties.Hint** - Control descriptions
- **AutomationId** - UI automation support

## Platform-Specific Features

### Windows

- Native Windows 10/11 integration
- File system access for profile storage
- System notifications

### macOS (Mac Catalyst)

- Native macOS appearance
- Menu bar integration
- Dock icon support

### Android

- Material Design components
- Android file picker integration
- Background service support

### iOS

- iOS design guidelines compliance
- Files app integration
- iOS-specific permissions

## Development Setup

### Prerequisites

- .NET 6.0 or later
- Visual Studio 2022 or Visual Studio Code with MAUI extension
- Platform-specific SDKs:
  - Android SDK (for Android development)
  - Xcode (for iOS/macOS development)
  - Windows SDK (for Windows development)

### Building the Application

**All Platforms:**

```bash
cd cdc-maui
dotnet build
```

**Specific Platform:**

```bash
dotnet build -f net6.0-android
dotnet build -f net6.0-ios
dotnet build -f net6.0-maccatalyst
dotnet build -f net6.0-windows10.0.19041.0
```

### Running the Application

**Development:**

```bash
dotnet run
```

**Platform-Specific:**

```bash
dotnet run --framework net6.0-windows10.0.19041.0
```

## Deployment

### Android

```bash
dotnet publish -f net6.0-android -c Release
```

Generates APK in `bin/Release/net6.0-android/publish/`

### iOS

```bash
dotnet publish -f net6.0-ios -c Release
```

Requires Apple Developer account for distribution

### Windows

```bash
dotnet publish -f net6.0-windows10.0.19041.0 -c Release
```

Creates MSIX package for Microsoft Store or sideloading

### macOS

```bash
dotnet publish -f net6.0-maccatalyst -c Release
```

Generates .app bundle for macOS distribution

## Testing

### Unit Testing

```csharp
[Test]
public void MainPage_Initialization_ShouldSetupControls()
{
    // Arrange & Act
    var mainPage = new MainPage();

    // Assert
    Assert.IsNotNull(mainPage.selectedDatabase);
    Assert.IsNotNull(mainPage.recordButton);
}
```

### UI Testing

```csharp
[Test]
public void RecordButton_Click_ShouldTriggerCdcOperation()
{
    // Arrange
    var app = AppInitializer.StartApp();

    // Act
    app.Tap("recordButton");

    // Assert
    app.WaitForElement("Success");
}
```

## Performance Considerations

### Memory Management

- Dispose of database connections properly
- Use weak references for event handlers
- Implement proper page lifecycle management

### Background Operations

- Use `Task.Run()` for CPU-intensive operations
- Implement progress indicators for long-running tasks
- Handle cancellation tokens for user-initiated cancellations

### Platform Optimization

- Use platform-specific optimizations where available
- Implement lazy loading for large data sets
- Cache frequently accessed data

## Security Considerations

### Data Protection

- Store connection strings securely using platform keychain/credential manager
- Encrypt sensitive profile data
- Implement secure file storage

### Network Security

- Use HTTPS for remote connections
- Validate SSL certificates
- Implement connection timeout handling

## Future Enhancements

### Advanced Features

1. **Real-time CDC Monitoring** - Live updates of CDC changes
2. **Profile Visualization** - Charts and graphs for change analysis
3. **Batch Operations** - Multiple profile generation and comparison
4. **Export Options** - PDF, Excel, CSV export formats
5. **Settings Management** - User preferences and configuration
6. **Multi-language Support** - Localization for different languages

### Integration Features

1. **Cloud Storage** - Azure Blob Storage, AWS S3 integration
2. **Team Collaboration** - Share profiles and results
3. **CI/CD Integration** - Automated testing workflows
4. **Notification System** - Email, push notifications for operations
5. **Plugin Architecture** - Custom CDC analyzers and exporters

### Platform-Specific Enhancements

1. **Windows** - Windows Terminal integration, PowerShell cmdlets
2. **macOS** - Shortcuts app integration, Apple Script support
3. **Android** - Widget support, Android Auto integration
4. **iOS** - Siri Shortcuts, Apple Watch companion app

## Troubleshooting

### Common Issues

#### Build Errors

- Ensure all platform SDKs are installed
- Check target framework compatibility
- Verify NuGet package versions

#### Runtime Errors

- Check database connectivity
- Verify CDC permissions
- Monitor memory usage on mobile devices

#### Platform-Specific Issues

- **Android**: Check API level compatibility
- **iOS**: Verify provisioning profiles
- **Windows**: Check Windows version compatibility
- **macOS**: Verify Mac Catalyst requirements

### Debug Configuration

Enable detailed logging for troubleshooting:

```csharp
#if DEBUG
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif
```

## Contributing

### Code Style

- Follow Microsoft C# coding conventions
- Use XAML formatting guidelines
- Implement proper error handling
- Add XML documentation for public APIs

### Testing Requirements

- Unit tests for business logic
- UI tests for critical user flows
- Platform-specific testing on target devices
- Performance testing for large datasets
