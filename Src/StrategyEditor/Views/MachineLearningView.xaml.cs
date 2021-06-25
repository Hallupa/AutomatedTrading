using System.Windows.Controls;
using StrategyEditor.ViewModels;

namespace StrategyEditor.Views
{
    /// <summary>
    /// Interaction logic for MachineLearningView.xaml
    /// </summary>
    public partial class MachineLearningView : Page
    {
        public MachineLearningView()
        {
            InitializeComponent();

            DataContext = new MachineLearningViewModel();
        }
    }
}