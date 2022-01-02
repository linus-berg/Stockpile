using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Stockpile.UI {
  public class ManagerWindow : Window {
    public ManagerWindow() {
      InitializeComponent();
#if DEBUG
      this.AttachDevTools();
#endif
    }

    private void InitializeComponent() {
      AvaloniaXamlLoader.Load(this);
    }
  }
}