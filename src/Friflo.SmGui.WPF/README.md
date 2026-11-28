# Friflo.SmGui.WPF


SmGui backend for WPF using Skia: https://github.com/dotnet/wpf


## Example

MainWindow.xaml
```xml
<Window x:Class="WPFApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:smGui="clr-namespace:Friflo.SmGui.WPF;assembly=Friflo.SmGui.WPF"
        mc:Ignorable="d"
        Title="MainWindow" Height="450" Width="800">
    <smGui:SkiaGuiElement x:Name="GuiElement"/>
</Window>
```

MainWindow.xaml.cs
```csharp
public partial class MainWindow : Window
{
    public MainWindow() {
        InitializeComponent();
        GuiElement.CreateGuiApp(backend => new MyGuiApp(backend), new DefaultGuiAssets());
    }
}
```