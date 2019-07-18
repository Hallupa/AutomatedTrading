using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Windows;
using AutomatedTrader.ViewModels;
using Hallupa.Library;

namespace AutomatedTrader.Services
{
    [Export(typeof(UIService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class UIService : DependencyObject, INotifyPropertyChanged
    {
        private DisplayPages _selectedDisplayPage = DisplayPages.RunStrategy;
        private Subject<UIService> _viewTradeObservable;
        private Subject<UIService> _viewTradeSetupObservable;

        public UIService()
        {
            ViewTradeCommand = new DelegateCommand(o => ViewTrade());
            ViewTradeSetupCommand = new DelegateCommand(o => ViewTradeSetup());
        }

        public DelegateCommand ViewTradeCommand { get; private set; }

        public DelegateCommand ViewTradeSetupCommand { get; private set; }

        public static readonly DependencyProperty IsViewTradeEnabledProperty = DependencyProperty.Register(
            "IsViewTradeEnabled", typeof(bool), typeof(UIService), new PropertyMetadata(true));

        public bool IsViewTradeEnabled
        {
            get { return (bool) GetValue(IsViewTradeEnabledProperty);
            }
            set { SetValue(IsViewTradeEnabledProperty, value);
            }
        }

        public DisplayPages? SelectedDisplayPage
        {
            get => _selectedDisplayPage;
            set
            {
                if (_selectedDisplayPage == value || value == null)
                {
                    return;
                }

                _selectedDisplayPage = value.Value;
                OnPropertyChanged();
            }
        }

        private Subject<UIService> ViewTradeSubject => _viewTradeObservable ?? (_viewTradeObservable = new Subject<UIService>());

        public IObservable<UIService> ViewTradeObservable => ViewTradeSubject.AsObservable();
        private Subject<UIService> ViewTradeSetupSubject => _viewTradeSetupObservable ?? (_viewTradeSetupObservable = new Subject<UIService>());

        public IObservable<UIService> ViewTradeSetupObservable => ViewTradeSetupSubject.AsObservable();

        private void ViewTradeSetup()
        {
            ViewTradeSetupSubject.OnNext(this);
        }

        private void ViewTrade()
        {
            ViewTradeSubject.OnNext(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}