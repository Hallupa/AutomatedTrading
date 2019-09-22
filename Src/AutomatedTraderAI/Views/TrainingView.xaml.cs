using TraderTools.AutomatedTraderAI.ViewModels;

namespace TraderTools.AutomatedTraderAI.Views
{
    /// <summary>
    /// Interaction logic for TrainingView.xaml
    /// </summary>
    public partial class TrainingView
    {
        public TrainingView()
        {
            InitializeComponent();

            DataContext = new TrainingViewModel();
        }
    }
}