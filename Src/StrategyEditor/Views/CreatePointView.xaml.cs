using System.Windows;
using StrategyEditor.ViewModels;

namespace StrategyEditor.Views
{
    /// <summary>
    /// Interaction logic for CreatePointView.xaml
    /// </summary>
    public partial class CreatePointView : Window
    {
        public CreatePointView()
        {
            InitializeComponent();

            Model = new CreatePointViewModel(Close);
            DataContext = Model;
        }

        public CreatePointViewModel Model { get; private set; }
    }
}