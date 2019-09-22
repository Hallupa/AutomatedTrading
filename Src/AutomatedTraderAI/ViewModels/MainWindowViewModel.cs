using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Hallupa.Library;
using TraderTools.Basics;
using TraderTools.Brokers.FXCM;

namespace TraderTools.AutomatedTraderAI.ViewModels
{
    public enum PageToShow
    {
        Training
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        [Import] private IBrokersService _brokersService;
        private PageToShow _page;
        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            DependencyContainer.ComposeParts(this);

            // Setup brokers
            var brokers = new[]
            {
                Broker = new FxcmBroker()
            };

            _brokersService.AddBrokers(brokers);
        }

        public IBroker Broker { get; }

        public PageToShow Page
        {
            get { return _page; }
            set
            {
                _page = value;
                OnPropertyChanged();
            }
        }
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}