using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Windows;
using StrategyEditor.ViewModels;
using Hallupa.Library;

namespace StrategyEditor.Services
{
    [Export(typeof(UIService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class UIService : DependencyObject, INotifyPropertyChanged
    {
        private DisplayPages _selectedDisplayPage = DisplayPages.RunCustomStrategy;
        private Subject<UIService> _viewTradeObservable;
        private Subject<UIService> _viewTradeSetupObservable;
        private List<Action> _f5Actions = new List<Action>();
        private List<Action> _cntrlSActions = new List<Action>();

        public UIService()
        {
            ViewTradeCommand = new DelegateCommand(o => ViewTrade());
        }

        public DelegateCommand ViewTradeCommand { get; private set; }

        public string SelectedStrategyFilename { get; set; }

        public static readonly DependencyProperty IsViewTradeEnabledProperty = DependencyProperty.Register(
            "IsViewTradeEnabled", typeof(bool), typeof(UIService), new PropertyMetadata(true));

        public bool IsViewTradeEnabled
        {
            get { return (bool) GetValue(IsViewTradeEnabledProperty);
            }
            set { SetValue(IsViewTradeEnabledProperty, value);
            }
        }

        public static readonly DependencyProperty UseHeikenAshiProperty = DependencyProperty.Register(
            "UseHeikenAshi", typeof(bool), typeof(UIService), new PropertyMetadata(false, UseHeikenAshiChanged));

        private static void UseHeikenAshiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((UIService)d).ViewTrade();
        }

        public bool UseHeikenAshi
        {
            get { return (bool) GetValue(UseHeikenAshiProperty); }
            set { SetValue(UseHeikenAshiProperty, value); }
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

        public void RegisterF5Action(Action action, bool placeFirst = false)
        {
            if (placeFirst)
            {
                _f5Actions.Insert(0, action);
            }
            else
            {
                _f5Actions.Add(action);
            }
        }

        public void RaiseF5Pressed()
        {
            foreach (var action in _f5Actions)
            {
                action();
            }
        }

        private Subject<UIService> ViewTradeSubject => _viewTradeObservable ?? (_viewTradeObservable = new Subject<UIService>());

        public IObservable<UIService> ViewTradeObservable => ViewTradeSubject.AsObservable();

        public string SelectedCodeText { get; set; }

        private void ViewTrade()
        {
            ViewTradeSubject.OnNext(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RegisterControlSAction(Action action, bool placeFirst = false)
        {
            if (placeFirst)
            {
                _cntrlSActions.Insert(0, action);
            }
            else
            {
                _cntrlSActions.Add(action);
            }
        }

        public void RaiseControlSPressed()
        {
            foreach (var action in _cntrlSActions)
            {
                action();
            }
        }
    }
}